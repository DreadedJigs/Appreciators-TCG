import {
  createHash,
  randomBytes,
  randomUUID,
  scrypt as scryptCallback,
  timingSafeEqual
} from "node:crypto";
import { EventEmitter } from "node:events";
import { mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { promisify } from "node:util";
import { Pool } from "pg";
import {
  applyAuthoritativeMatchAction,
  createAuthoritativeMatch,
  isAuthoritativeMatch,
  publicAuthoritativeEvents,
  publicAuthoritativeMatch,
  verifyAuthoritativeMatchIntegrity
} from "./authoritativeMatchEngine.js";

const scrypt = promisify(scryptCallback);
const MAX_SNAPSHOT_BYTES = 192 * 1024;
const MAX_MATCH_EVENT_PAYLOAD_BYTES = 8 * 1024;
const SESSION_TTL_DAYS = 7;
const MATCH_QUEUE_TTL_MS = 2 * 60 * 1000;
const MATCH_EVENT_LIMIT = 250;
const VALID_MATCH_MODES = new Set(["Casual", "Ranked"]);
const VALID_THEMES = new Set(["Dark", "Light"]);
const VALID_ACTIONS = new Set(["draw", "build", "discard", "end-round", "concede", "emote"]);

/**
 * Security boundary for accounts, cloud saves, and the authoritative online
 * match protocol. The file driver exists for local development and a mounted
 * single-instance volume only. Production starts in a fail-closed state unless
 * APP_DATA_DIR and APP_ALLOW_FILE_PERSISTENCE=true are deliberately supplied.
 */
export class SecureGameStore {
  constructor(options = {}) {
    this.isProduction = options.isProduction ?? process.env.NODE_ENV === "production";
    this.allowFilePersistence = options.allowFilePersistence ?? (
      !this.isProduction || process.env.APP_ALLOW_FILE_PERSISTENCE === "true"
    );
    this.databaseUrl = String(options.databaseUrl ?? process.env.DATABASE_URL ?? "").trim();
    this.databaseSsl = options.databaseSsl ?? process.env.APP_DATABASE_SSL === "true";
    this.usingPostgres = Boolean(this.databaseUrl);
    this.postgresTable = "appreciators_secure_state";
    this.postgresStoreKey = "primary";
    this.pg = null;
    this.postgresSchemaReady = false;
    const configuredDirectory = options.dataDirectory || process.env.APP_DATA_DIR || "data/runtime";
    this.filePath = resolve(options.filePath || process.env.APP_SECURE_STORE_PATH || `${configuredDirectory}/secure-game-store.json`);
    this.usingExplicitDataDirectory = Boolean(options.dataDirectory || process.env.APP_DATA_DIR);
    this.sessionTtlMs = Math.max(
      60 * 60 * 1000,
      Math.min(30 * 24 * 60 * 60 * 1000, Number(options.sessionTtlMs) || SESSION_TTL_DAYS * 24 * 60 * 60 * 1000)
    );
    this.loaded = false;
    this.writeQueue = Promise.resolve();
    this.events = new EventEmitter();
    this.events.setMaxListeners(0);
    this.data = createEmptyData();
  }

  getStatus() {
    if (this.usingPostgres) {
      return {
        driver: "postgresql",
        configured: true,
        durable: true,
        storagePathConfigured: true,
        reason: "ready"
      };
    }
    return {
      driver: "file",
      configured: this.allowFilePersistence,
      durable: this.allowFilePersistence && (!this.isProduction || this.usingExplicitDataDirectory),
      storagePathConfigured: this.usingExplicitDataDirectory,
      reason: this.allowFilePersistence
        ? (this.isProduction && !this.usingExplicitDataDirectory
          ? "Production file persistence requires a mounted APP_DATA_DIR."
          : "ready")
        : "Set APP_DATA_DIR to a persistent volume and APP_ALLOW_FILE_PERSISTENCE=true, or configure the database adapter before enabling production accounts."
    };
  }

  async checkReadiness() {
    await this.ensureReady();
    if (this.usingPostgres) {
      try {
        await this.pg.query("SELECT 1");
      } catch (error) {
        throw requestError("PostgreSQL readiness check failed: " + error.message, 503, "PERSISTENCE_UNAVAILABLE");
      }
    }
    return this.getStatus();
  }

  async registerAccount(payload = {}) {
    await this.ensureReady();
    const username = normalizeUsername(payload.username);
    const password = validatePassword(payload.password);
    const usernameKey = username.toLowerCase();
    const passwordHash = await createPasswordRecord(password);

    return this.mutate(() => {
      if (this.data.accounts.some((account) => account.usernameKey === usernameKey)) {
        throw requestError("That display name is already registered.", 409, "USERNAME_UNAVAILABLE");
      }

      const account = {
        id: `acct_${randomUUID().replace(/-/g, "")}`,
        username,
        usernameKey,
        password: passwordHash,
        createdAt: nowIso(),
        updatedAt: nowIso(),
        disabledAt: null
      };
      this.data.accounts.push(account);
      return this.createSessionForAccount(account, payload.deviceName);
    });
  }

  async loginAccount(payload = {}) {
    await this.ensureReady();
    const usernameKey = normalizeUsername(payload.username).toLowerCase();
    const password = String(payload.password || "");
    const account = this.data.accounts.find((entry) => entry.usernameKey === usernameKey);
    if (!account || account.disabledAt || !(await passwordMatches(password, account.password))) {
      throw requestError("The account name or password is incorrect.", 401, "INVALID_CREDENTIALS");
    }
    return this.mutate(() => this.createSessionForAccount(account, payload.deviceName));
  }

  async createWalletSession(walletAddress, displayName = "") {
    await this.ensureReady();
    const normalizedWallet = normalizeWalletAddress(walletAddress).toLowerCase();
    let account = this.data.accounts.find((entry) => entry.walletAddress === normalizedWallet);
    return this.mutate(() => {
      if (!account) {
        const suggestedName = normalizeWalletDisplayName(displayName || `Holder ${normalizedWallet.slice(2, 8)}`);
        const username = allocateWalletUsername(this.data.accounts, suggestedName);
        account = {
          id: `acct_${randomUUID().replace(/-/g, "")}`,
          username,
          usernameKey: username.toLowerCase(),
          password: null,
          walletAddress: normalizedWallet,
          createdAt: nowIso(),
          updatedAt: nowIso(),
          disabledAt: null
        };
        this.data.accounts.push(account);
      }
      account.walletAddress = normalizedWallet;
      account.updatedAt = nowIso();
      return this.createSessionForAccount(account, "wallet");
    });
  }

  async verifySession(rawToken) {
    await this.ensureReady();
    const parsed = parseSessionToken(rawToken);
    if (!parsed) {
      throw requestError("Sign in is required.", 401, "AUTH_REQUIRED");
    }
    const session = this.data.sessions.find((entry) => entry.id === parsed.id);
    if (!session || session.revokedAt || Date.parse(session.expiresAt) <= Date.now()) {
      throw requestError("Your session has expired. Sign in again.", 401, "SESSION_EXPIRED");
    }
    const actualHash = hashToken(parsed.secret);
    const expectedHash = Buffer.from(session.tokenHash, "hex");
    if (actualHash.length !== expectedHash.length || !timingSafeEqual(actualHash, expectedHash)) {
      throw requestError("Your session is not valid.", 401, "SESSION_INVALID");
    }
    const account = this.data.accounts.find((entry) => entry.id === session.accountId && !entry.disabledAt);
    if (!account) {
      throw requestError("This account is no longer active.", 401, "ACCOUNT_INACTIVE");
    }
    return { account: publicAccount(account), session: publicSession(session) };
  }

  async refreshSession(rawToken) {
    const identity = await this.verifySession(rawToken);
    return this.mutate(() => {
      const session = this.data.sessions.find((entry) => entry.id === identity.session.id);
      session.revokedAt = nowIso();
      return this.createSessionForAccount(this.accountById(identity.account.id), session.deviceName);
    });
  }

  async revokeSession(rawToken) {
    const identity = await this.verifySession(rawToken);
    return this.mutate(() => {
      const session = this.data.sessions.find((entry) => entry.id === identity.session.id);
      session.revokedAt = nowIso();
      return { success: true };
    });
  }

  async listSessions(rawToken) {
    const identity = await this.verifySession(rawToken);
    const now = Date.now();
    return this.data.sessions
      .filter((session) => session.accountId === identity.account.id && !session.revokedAt && Date.parse(session.expiresAt) > now)
      .map((session) => ({ ...publicSession(session), current: session.id === identity.session.id }));
  }

  async revokeOtherSessions(rawToken) {
    const identity = await this.verifySession(rawToken);
    return this.mutate(() => {
      let revoked = 0;
      for (const session of this.data.sessions) {
        if (session.accountId !== identity.account.id || session.id === identity.session.id || session.revokedAt) continue;
        session.revokedAt = nowIso();
        revoked += 1;
      }
      return { success: true, revoked };
    });
  }

  async getCloudSave(accountId) {
    await this.ensureReady();
    this.requireAccount(accountId);
    const entry = this.data.cloudSaves[accountId];
    return entry
      ? { version: entry.version, updatedAt: entry.updatedAt, snapshot: structuredClone(entry.snapshot) }
      : { version: 0, updatedAt: null, snapshot: defaultSnapshot() };
  }

  async saveCloudSave(accountId, payload = {}) {
    await this.ensureReady();
    this.requireAccount(accountId);
    const snapshot = sanitizeCloudSnapshot(payload.snapshot);
    const expectedVersion = optionalPositiveInteger(payload.expectedVersion);
    return this.mutate(() => {
      const existing = this.data.cloudSaves[accountId];
      const currentVersion = existing?.version || 0;
      if (expectedVersion !== null && expectedVersion !== currentVersion) {
        throw requestError("This save changed on another device. Download the latest cloud save before overwriting it.", 409, "CLOUD_SAVE_CONFLICT", {
          currentVersion,
          updatedAt: existing?.updatedAt || null
        });
      }
      const entry = {
        version: currentVersion + 1,
        updatedAt: nowIso(),
        snapshot
      };
      this.data.cloudSaves[accountId] = entry;
      return { version: entry.version, updatedAt: entry.updatedAt, snapshot: structuredClone(snapshot) };
    });
  }

  async queueMatch(accountId, payload = {}) {
    await this.ensureReady();
    const account = this.requireAccount(accountId);
    const mode = normalizeMode(payload.mode);
    const deckIds = normalizeDeckIds(payload.deckIds);
    return this.mutate(() => {
      pruneQueues(this.data.matchQueues);
      const existingMatch = this.data.matches.find((match) => match.status !== "complete" && match.players.some((player) => player.accountId === account.id));
      if (existingMatch) return { status: "matched", match: publicMatch(existingMatch, account.id) };

      const existingQueue = this.data.matchQueues.find((ticket) => ticket.accountId === account.id && ticket.mode === mode);
      if (existingQueue) return { status: "queued", ticket: publicTicket(existingQueue) };

      const opponentTicketIndex = this.data.matchQueues.findIndex((ticket) => ticket.mode === mode && ticket.accountId !== account.id);
      if (opponentTicketIndex === -1) {
        const ticket = {
          id: `queue_${randomUUID().replace(/-/g, "")}`,
          accountId: account.id,
          deckIds,
          mode,
          queuedAt: nowIso(),
          expiresAt: new Date(Date.now() + MATCH_QUEUE_TTL_MS).toISOString()
        };
        this.data.matchQueues.push(ticket);
        return { status: "queued", ticket: publicTicket(ticket) };
      }

      const opponentTicket = this.data.matchQueues.splice(opponentTicketIndex, 1)[0];
      const opponent = this.requireAccount(opponentTicket.accountId);
      const match = createMatch({ mode, host: opponent, hostDeckIds: opponentTicket.deckIds, guest: account, guestDeckIds: deckIds });
      this.data.matches.push(match);
      this.emitMatch(match);
      return { status: "matched", match: publicMatch(match, account.id) };
    });
  }

  async cancelQueue(accountId, ticketId = "") {
    await this.ensureReady();
    return this.mutate(() => {
      const before = this.data.matchQueues.length;
      this.data.matchQueues = this.data.matchQueues.filter((ticket) => ticket.accountId !== accountId || (ticketId && ticket.id !== ticketId));
      return { success: before !== this.data.matchQueues.length };
    });
  }

  async getMatch(accountId, matchId) {
    await this.ensureReady();
    const match = this.matchForMember(accountId, matchId);
    return publicMatch(match, accountId);
  }

  async getMatchEvents(accountId, matchId, afterSequence = 0) {
    await this.ensureReady();
    const match = this.matchForMember(accountId, matchId);
    if (isAuthoritativeMatch(match)) {
      return publicAuthoritativeEvents(match, accountId, afterSequence);
    }
    return publicMatchEvents(match, afterSequence);
  }

  async getMatchReplay(accountId, matchId) {
    await this.ensureReady();
    const match = this.matchForMember(accountId, matchId);
    if (!isAuthoritativeMatch(match)) {
      throw requestError("Replays are available for authoritative matches only.", 409, "MATCH_RULES_UPGRADE_REQUIRED");
    }
    return {
      ...publicAuthoritativeEvents(match, accountId, 0),
      integrityVerified: verifyAuthoritativeMatchIntegrity(match)
    };
  }

  async waitForMatchEvents(accountId, matchId, afterSequence = 0, waitMs = 20_000) {
    const initial = await this.getMatchEvents(accountId, matchId, afterSequence);
    if (initial.events.length > 0 || waitMs <= 0) return initial;
    const boundedWaitMs = Math.max(0, Math.min(25_000, Number(waitMs) || 0));
    return new Promise((resolve) => {
      const eventName = `match:${matchId}`;
      const finish = async () => {
        clearTimeout(timer);
        this.events.removeListener(eventName, finish);
        resolve(await this.getMatchEvents(accountId, matchId, afterSequence));
      };
      const timer = setTimeout(finish, boundedWaitMs);
      this.events.once(eventName, finish);
    });
  }

  async applyMatchAction(accountId, matchId, payload = {}) {
    await this.ensureReady();
    return this.mutate(() => {
      const match = this.matchForMember(accountId, matchId);
      if (match.status === "complete") throw requestError("This match is complete.", 409, "MATCH_COMPLETE");
      const expectedVersion = optionalPositiveInteger(payload.expectedVersion);
      if (expectedVersion !== null && expectedVersion !== match.version) {
        throw requestError("This match changed. Synchronize before submitting another action.", 409, "MATCH_VERSION_CONFLICT", {
          currentVersion: match.version
        });
      }
      if (isAuthoritativeMatch(match)) {
        const result = applyAuthoritativeMatchAction(match, accountId, payload);
        this.emitMatch(match);
        return result;
      }
      throw requestError("This legacy match cannot be resumed. Start a new authoritative match.", 409, "MATCH_RULES_UPGRADE_REQUIRED");
    });
  }

  async ensureReady() {
    if (this.usingPostgres) {
      if (!this.pg) {
        this.pg = new Pool({
          connectionString: this.databaseUrl,
          ssl: this.databaseSsl ? { rejectUnauthorized: false } : undefined,
          max: 8,
          idleTimeoutMillis: 30_000,
          connectionTimeoutMillis: 8_000
        });
      }
      await this.ensurePostgresSchema();
      await this.refreshPostgresState();
      this.loaded = true;
      return;
    }
    if (this.loaded) return;
    if (!this.allowFilePersistence) {
      throw requestError(this.getStatus().reason, 503, "PERSISTENCE_NOT_CONFIGURED");
    }
    this.loaded = true;
    try {
      const parsed = JSON.parse(readFileSync(this.filePath, "utf8"));
      this.data = normalizeStoredData(parsed);
    } catch (error) {
      if (error.code !== "ENOENT") {
        this.loaded = false;
        throw requestError(`Secure storage could not be read: ${error.message}`, 503, "PERSISTENCE_UNAVAILABLE");
      }
    }
  }

  async mutate(work) {
    const run = async () => {
      if (this.usingPostgres) return this.mutatePostgres(work);
      const result = await work();
      this.persist();
      return result;
    };
    const next = this.writeQueue.then(run, run);
    this.writeQueue = next.catch(() => undefined);
    return next;
  }

  async ensurePostgresSchema() {
    if (this.postgresSchemaReady) return;
    try {
      await this.pg.query(
        "CREATE TABLE IF NOT EXISTS appreciators_secure_state (" +
          "store_key text PRIMARY KEY, " +
          "document jsonb NOT NULL, " +
          "updated_at timestamptz NOT NULL DEFAULT now()" +
        ")"
      );
      await this.pg.query(
        "INSERT INTO appreciators_secure_state (store_key, document) VALUES ($1, $2::jsonb) ON CONFLICT (store_key) DO NOTHING",
        [this.postgresStoreKey, JSON.stringify(createEmptyData())]
      );
      this.postgresSchemaReady = true;
    } catch (error) {
      throw requestError("PostgreSQL persistence could not be initialized: " + error.message, 503, "PERSISTENCE_UNAVAILABLE");
    }
  }

  async refreshPostgresState() {
    try {
      const result = await this.pg.query(
        "SELECT document FROM appreciators_secure_state WHERE store_key = $1",
        [this.postgresStoreKey]
      );
      if (result.rowCount !== 1) throw new Error("the primary secure-state document is missing");
      this.data = normalizeStoredData(result.rows[0].document);
    } catch (error) {
      throw requestError("PostgreSQL persistence could not be read: " + error.message, 503, "PERSISTENCE_UNAVAILABLE");
    }
  }

  async mutatePostgres(work) {
    let client;
    try {
      client = await this.pg.connect();
      await client.query("BEGIN");
      const selected = await client.query(
        "SELECT document FROM appreciators_secure_state WHERE store_key = $1 FOR UPDATE",
        [this.postgresStoreKey]
      );
      if (selected.rowCount !== 1) throw new Error("the primary secure-state document is missing");
      this.data = normalizeStoredData(selected.rows[0].document);
      const result = await work();
      await client.query(
        "UPDATE appreciators_secure_state SET document = $2::jsonb, updated_at = now() WHERE store_key = $1",
        [this.postgresStoreKey, JSON.stringify(this.data)]
      );
      await client.query("COMMIT");
      return result;
    } catch (error) {
      try { if (client) await client.query("ROLLBACK"); } catch { /* best effort */ }
      if (error?.errorCode) throw error;
      throw requestError("PostgreSQL persistence could not be written: " + error.message, 503, "PERSISTENCE_UNAVAILABLE");
    } finally {
      if (client) client.release();
    }
  }

  persist() {
    const temporaryPath = `${this.filePath}.${process.pid}.${randomBytes(4).toString("hex")}.tmp`;
    try {
      mkdirSync(dirname(this.filePath), { recursive: true });
      writeFileSync(temporaryPath, JSON.stringify(this.data, null, 2), { encoding: "utf8", mode: 0o600 });
      renameSync(temporaryPath, this.filePath);
    } catch (error) {
      try { rmSync(temporaryPath, { force: true }); } catch { /* best effort */ }
      throw requestError(`Secure storage could not be written: ${error.message}`, 503, "PERSISTENCE_UNAVAILABLE");
    }
  }

  createSessionForAccount(account, deviceName = "") {
    const id = `ses_${randomUUID().replace(/-/g, "")}`;
    const secret = randomBytes(32).toString("base64url");
    const expiresAt = new Date(Date.now() + this.sessionTtlMs).toISOString();
    const session = {
      id,
      accountId: account.id,
      tokenHash: hashToken(secret).toString("hex"),
      deviceName: String(deviceName || "").trim().slice(0, 64),
      createdAt: nowIso(),
      expiresAt,
      revokedAt: null
    };
    this.data.sessions.push(session);
    this.data.sessions = this.data.sessions.filter((entry) => !entry.revokedAt && Date.parse(entry.expiresAt) > Date.now()).slice(-32);
    return {
      account: publicAccount(account),
      session: publicSession(session),
      accessToken: `${id}.${secret}`
    };
  }

  accountById(accountId) {
    return this.data.accounts.find((account) => account.id === accountId);
  }

  requireAccount(accountId) {
    const account = this.accountById(accountId);
    if (!account || account.disabledAt) throw requestError("This account is no longer active.", 401, "ACCOUNT_INACTIVE");
    return account;
  }

  matchForMember(accountId, rawMatchId) {
    const matchId = safeIdentifier(rawMatchId, "matchId");
    const match = this.data.matches.find((entry) => entry.id === matchId);
    if (!match) throw requestError("Match not found.", 404, "MATCH_NOT_FOUND");
    if (!match.players.some((player) => player.accountId === accountId)) {
      throw requestError("You are not a participant in this match.", 403, "MATCH_MEMBERSHIP_REQUIRED");
    }
    return match;
  }

  emitMatch(match) {
    this.events.emit(`match:${match.id}`);
  }
}

let defaultStore;

export function getSecureGameStore() {
  defaultStore ||= new SecureGameStore();
  return defaultStore;
}

export function resetSecureGameStoreForTests() {
  defaultStore = undefined;
}

function createEmptyData() {
  return { schemaVersion: 1, accounts: [], sessions: [], cloudSaves: {}, matchQueues: [], matches: [] };
}

function normalizeStoredData(stored) {
  const empty = createEmptyData();
  return {
    ...empty,
    ...(stored && typeof stored === "object" ? stored : {}),
    accounts: Array.isArray(stored?.accounts) ? stored.accounts : [],
    sessions: Array.isArray(stored?.sessions) ? stored.sessions : [],
    cloudSaves: stored?.cloudSaves && typeof stored.cloudSaves === "object" ? stored.cloudSaves : {},
    matchQueues: Array.isArray(stored?.matchQueues) ? stored.matchQueues : [],
    matches: Array.isArray(stored?.matches) ? stored.matches : []
  };
}

function publicAccount(account) {
  return {
    id: account.id,
    username: account.username,
    displayName: account.username,
    walletLinked: Boolean(account.walletAddress),
    createdAt: account.createdAt,
    updatedAt: account.updatedAt
  };
}

function publicSession(session) {
  return { id: session.id, expiresAt: session.expiresAt };
}

function defaultSnapshot() {
  return {
    schemaVersion: 1,
    settings: { theme: "Dark", reducedMotion: false, musicVolume: 0.62, musicRepeat: true },
    tutorial: { step: 0, coreDemonstrated: false, completed: false },
    deckIds: [],
    namedDecks: [],
    selectedBossTokenId: ""
  };
}

function sanitizeCloudSnapshot(value) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw requestError("Cloud saves must be an object.", 400, "INVALID_CLOUD_SAVE");
  }
  const raw = JSON.stringify(value);
  if (Buffer.byteLength(raw, "utf8") > MAX_SNAPSHOT_BYTES) {
    throw requestError("Cloud save exceeds the 192 KB limit.", 413, "CLOUD_SAVE_TOO_LARGE");
  }
  const settings = value.settings || {};
  const tutorial = value.tutorial || {};
  return {
    schemaVersion: Math.max(1, Math.min(100, Number.parseInt(value.schemaVersion, 10) || 1)),
    settings: {
      theme: VALID_THEMES.has(settings.theme) ? settings.theme : "Dark",
      reducedMotion: settings.reducedMotion === true,
      musicVolume: clampNumber(settings.musicVolume, 0, 1, 0.62),
      musicRepeat: settings.musicRepeat !== false
    },
    tutorial: {
      step: Math.max(0, Math.min(1000, Number.parseInt(tutorial.step, 10) || 0)),
      coreDemonstrated: tutorial.coreDemonstrated === true,
      completed: tutorial.completed === true
    },
    deckIds: normalizeDeckIds(value.deckIds),
    namedDecks: normalizeNamedDecks(value.namedDecks),
    selectedBossTokenId: String(value.selectedBossTokenId || "").replace(/[^0-9]/g, "").slice(0, 12)
  };
}

function normalizeNamedDecks(value) {
  if (!Array.isArray(value)) return [];
  return value.slice(0, 12).map((deck, index) => ({
    name: String(deck?.name || `Deck ${index + 1}`).replace(/[<>]/g, "").trim().slice(0, 32) || `Deck ${index + 1}`,
    cardIds: normalizeDeckIds(deck?.cardIds)
  }));
}

function normalizeDeckIds(value) {
  if (!Array.isArray(value)) return [];
  return [...new Set(value.map((id) => String(id || "").trim()).filter((id) => /^[a-z0-9_-]{1,64}$/i.test(id)))].slice(0, 30);
}

function normalizeUsername(value) {
  const username = String(value || "").trim().replace(/\s+/g, " ");
  if (!/^[A-Za-z0-9][A-Za-z0-9 _-]{2,23}$/.test(username)) {
    throw requestError("Use 3–24 letters, numbers, spaces, hyphens, or underscores for your account name.", 400, "INVALID_USERNAME");
  }
  return username;
}

function normalizeWalletDisplayName(value) {
  const normalized = String(value || "Holder").trim().replace(/[^A-Za-z0-9 _-]/g, "").slice(0, 24);
  return /^[A-Za-z0-9]/.test(normalized) ? normalized : "Holder";
}

function allocateWalletUsername(accounts, suggested) {
  const base = normalizeWalletDisplayName(suggested).slice(0, 18);
  for (let suffix = 0; suffix < 1000; suffix += 1) {
    const candidate = suffix ? `${base}-${suffix}` : base;
    if (candidate.length < 3) continue;
    if (!accounts.some((account) => account.usernameKey === candidate.toLowerCase())) return candidate;
  }
  return `Holder-${randomBytes(4).toString("hex")}`;
}

function validatePassword(value) {
  const password = String(value || "");
  if (password.length < 12 || password.length > 128) {
    throw requestError("Passwords must be 12–128 characters.", 400, "WEAK_PASSWORD");
  }
  if (!/[a-z]/i.test(password) || !/[0-9]/.test(password)) {
    throw requestError("Passwords must include at least one letter and one number.", 400, "WEAK_PASSWORD");
  }
  return password;
}

async function createPasswordRecord(password) {
  const salt = randomBytes(16).toString("hex");
  const hash = Buffer.from(await scrypt(password, salt, 64)).toString("hex");
  return { salt, hash };
}

async function passwordMatches(password, record) {
  if (!record?.hash || !record?.salt) return false;
  const actual = Buffer.from(await scrypt(String(password || ""), record.salt, 64));
  const expected = Buffer.from(record.hash, "hex");
  return actual.length === expected.length && timingSafeEqual(actual, expected);
}

function hashToken(token) {
  return createHash("sha256").update(token).digest();
}

function parseSessionToken(value) {
  const raw = String(value || "").trim().replace(/^Bearer\s+/i, "");
  const match = /^(ses_[a-f0-9]{32})\.([A-Za-z0-9_-]{32,})$/.exec(raw);
  return match ? { id: match[1], secret: match[2] } : null;
}

function normalizeMode(value) {
  const mode = String(value || "Casual").trim();
  if (!VALID_MATCH_MODES.has(mode)) throw requestError("Online mode must be Casual or Ranked.", 400, "INVALID_MATCH_MODE");
  return mode;
}

function createMatch({ mode, host, hostDeckIds, guest, guestDeckIds }) {
  return createAuthoritativeMatch({ mode, host, hostDeckIds, guest, guestDeckIds });
}

function publicMatch(match, accountId) {
  if (isAuthoritativeMatch(match)) return publicAuthoritativeMatch(match, accountId);
  const player = match.players.find((entry) => entry.accountId === accountId);
  return {
    id: match.id,
    mode: match.mode,
    status: match.status,
    version: match.version,
    round: match.round,
    phase: match.phase,
    activeSide: match.activeSide,
    yourSide: player?.side || "",
    players: match.players.map((entry) => ({ accountId: entry.accountId, displayName: entry.displayName, side: entry.side, deckSize: entry.deckIds.length })),
    result: match.result,
    updatedAt: match.updatedAt,
    rulesVersion: "online-v1"
  };
}

function publicMatchEvents(match, afterSequence) {
  if (isAuthoritativeMatch(match)) return publicAuthoritativeEvents(match, "", afterSequence);
  const after = Math.max(0, Number.parseInt(afterSequence, 10) || 0);
  return { match: publicMatch(match, ""), events: match.events.filter((event) => event.sequence > after), latestSequence: match.events.at(-1)?.sequence || 0 };
}

function publicTicket(ticket) {
  return { id: ticket.id, mode: ticket.mode, queuedAt: ticket.queuedAt, expiresAt: ticket.expiresAt };
}

function pruneQueues(queues) {
  const now = Date.now();
  for (let index = queues.length - 1; index >= 0; index -= 1) {
    if (Date.parse(queues[index].expiresAt) <= now) queues.splice(index, 1);
  }
}

function normalizeMatchAction(payload) {
  const type = String(payload.type || "").trim();
  if (!VALID_ACTIONS.has(type)) throw requestError("That online action is not supported.", 400, "INVALID_MATCH_ACTION");
  const actionId = String(payload.actionId || "").trim();
  if (!/^[A-Za-z0-9_-]{8,80}$/.test(actionId)) throw requestError("A unique actionId is required.", 400, "INVALID_ACTION_ID");
  const sanitizedPayload = {
    cardId: String(payload.cardId || "").trim().slice(0, 64),
    lane: String(payload.lane || "").trim().slice(0, 32),
    message: String(payload.message || "").replace(/[<>]/g, "").trim().slice(0, 160)
  };
  if ((type === "build" || type === "discard") && !/^[A-Za-z0-9_-]{1,64}$/.test(sanitizedPayload.cardId)) {
    throw requestError("A valid cardId is required.", 400, "INVALID_CARD_ID");
  }
  if (Buffer.byteLength(JSON.stringify(sanitizedPayload), "utf8") > MAX_MATCH_EVENT_PAYLOAD_BYTES) {
    throw requestError("Match action payload is too large.", 413, "MATCH_ACTION_TOO_LARGE");
  }
  return { type, actionId, payload: sanitizedPayload };
}

function applyAuthoritativeAction(match, actor, action) {
  if (action.type === "emote") return;
  if (actor.side !== match.activeSide) throw requestError("It is not your turn.", 409, "NOT_ACTIVE_PLAYER");
  if (action.type === "concede") {
    match.status = "complete";
    match.result = { winnerSide: actor.side === "host" ? "guest" : "host", reason: "concession", completedAt: nowIso() };
    return;
  }
  if (action.type === "draw") {
    if (match.phase !== "draw") throw requestError("The draw action is not available in this phase.", 409, "INVALID_PHASE");
    match.phase = "learn";
    return;
  }
  if (action.type === "build" || action.type === "discard") {
    if (match.phase !== "learn") throw requestError("Choose Build or Discard during Learn.", 409, "INVALID_PHASE");
    match.phase = "grow";
    return;
  }
  if (action.type === "end-round") {
    if (match.phase !== "grow") throw requestError("Finish your Grow phase before ending the round.", 409, "INVALID_PHASE");
    match.activeSide = actor.side === "host" ? "guest" : "host";
    if (match.activeSide === "host") match.round += 1;
    match.phase = "draw";
  }
}

function safeIdentifier(value, label) {
  const id = String(value || "").trim();
  if (!/^[A-Za-z0-9_-]{8,80}$/.test(id)) throw requestError(`${label} is invalid.`, 400, "INVALID_IDENTIFIER");
  return id;
}

function normalizeWalletAddress(value) {
  const wallet = String(value || "").trim();
  if (!/^0x[a-fA-F0-9]{40}$/.test(wallet)) throw requestError("Enter a valid EVM wallet address.", 400, "INVALID_WALLET_ADDRESS");
  return wallet;
}

function optionalPositiveInteger(value) {
  if (value === undefined || value === null || value === "") return null;
  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed < 0) throw requestError("expectedVersion must be a non-negative integer.", 400, "INVALID_VERSION");
  return parsed;
}

function clampNumber(value, minimum, maximum, fallback) {
  const number = Number(value);
  return Number.isFinite(number) ? Math.max(minimum, Math.min(maximum, number)) : fallback;
}

function nowIso() {
  return new Date().toISOString();
}

function requestError(message, statusCode, errorCode, extras = {}) {
  return Object.assign(new Error(message), { statusCode, errorCode, ...extras });
}
