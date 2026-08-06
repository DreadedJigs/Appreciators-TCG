import { mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { getBossPoolStatus } from "./packInventoryStore.js";

export const BOSS_MINIMUM_PARTY_SIZE = 2;
export const BOSS_NOMINAL_PARTY_SIZE = 3;
export const BOSS_MAXIMUM_PARTY_SIZE = 3;

const battles = new Map();
const wallets = new Map();
const persistenceEnabled =
  process.env.BOSS_BATTLE_PERSISTENCE === "true" ||
  (process.env.NODE_ENV === "production" && process.env.BOSS_BATTLE_PERSISTENCE !== "false");
const persistencePath = resolve(process.env.BOSS_BATTLE_STORE_PATH || "data/runtime/boss-battles.json");
let loaded = false;

export function getBossBattleState(payload = {}) {
  const battle = getOrCreateBattle(payload.poolId);
  return publicBattle(battle, payload.playerId);
}

export function joinBossParty(payload = {}) {
  const battle = getOrCreateBattle(payload.poolId);
  requireSummoned(battle);
  const playerId = requirePlayerId(payload.playerId);
  if (battle.boss.playerId === playerId) {
    throw requestError("The active 1-of-1 boss cannot also join the member party.", 409, "BOSS_CANNOT_JOIN_PARTY");
  }

  const existing = battle.party[playerId];
  if (!existing && Object.keys(battle.party).length >= BOSS_MAXIMUM_PARTY_SIZE) {
    throw requestError("This boss party is full. Three members is the nominal party size.", 409, "BOSS_PARTY_FULL");
  }

  battle.party[playerId] = {
    playerId,
    displayName: safeDisplayName(payload.displayName),
    ready: existing?.ready || false,
    joinedAt: existing?.joinedAt || new Date().toISOString()
  };
  battle.status = "forming-party";
  battle.updatedAt = new Date().toISOString();
  persist({ required: true });
  return { success: true, battle: publicBattle(battle, playerId) };
}

export function leaveBossParty(payload = {}) {
  const battle = getOrCreateBattle(payload.poolId);
  const playerId = requirePlayerId(payload.playerId);
  delete battle.party[playerId];
  battle.status = Object.keys(battle.party).length > 0 ? "forming-party" : "summoned";
  battle.updatedAt = new Date().toISOString();
  persist({ required: true });
  return { success: true, battle: publicBattle(battle, playerId) };
}

export function setBossPartyReady(payload = {}) {
  const battle = getOrCreateBattle(payload.poolId);
  requireSummoned(battle);
  const playerId = requirePlayerId(payload.playerId);
  const member = battle.party[playerId];
  if (!member) {
    throw requestError("Join the member party before readying for the boss battle.", 409, "BOSS_PARTY_MEMBERSHIP_REQUIRED");
  }

  member.ready = payload.ready !== false && String(payload.ready).toLowerCase() !== "false";
  battle.status = "forming-party";
  battle.updatedAt = new Date().toISOString();
  persist({ required: true });
  return { success: true, battle: publicBattle(battle, playerId) };
}

export function claimBossRole(payload = {}) {
  const battle = getOrCreateBattle(payload.poolId);
  requireSummoned(battle);
  const playerId = requirePlayerId(payload.playerId);
  const wallet = wallets.get(playerId);
  if (!wallet?.oneOfOneEligible || !wallet.ownershipVerified) {
    throw requestError(
      "A server-verified 1-of-1 holder wallet is required to lead the boss side.",
      403,
      "ONE_OF_ONE_HOLDER_REQUIRED"
    );
  }

  delete battle.party[playerId];
  battle.boss = {
    playerId,
    displayName: safeDisplayName(payload.displayName || wallet.displayAddress || "1-of-1 Holder"),
    walletDisplay: wallet.displayAddress,
    verifiedOneOfOne: true,
    controlMode: "holder"
  };
  battle.status = Object.keys(battle.party).length > 0 ? "forming-party" : "summoned";
  battle.updatedAt = new Date().toISOString();
  persist({ required: true });
  return { success: true, battle: publicBattle(battle, playerId) };
}

export function releaseBossRole(payload = {}) {
  const battle = getOrCreateBattle(payload.poolId);
  const playerId = requirePlayerId(payload.playerId);
  if (battle.boss.playerId && battle.boss.playerId !== playerId) {
    throw requestError("Only the active 1-of-1 holder can release the boss role.", 403, "BOSS_ROLE_OWNER_REQUIRED");
  }
  battle.boss = provisionalBoss();
  battle.updatedAt = new Date().toISOString();
  persist({ required: true });
  return { success: true, battle: publicBattle(battle, playerId) };
}

export function startBossBattle(payload = {}) {
  const battle = getOrCreateBattle(payload.poolId);
  requireSummoned(battle);
  const playerId = requirePlayerId(payload.playerId);
  const participants = Object.values(battle.party);
  const isBoss = battle.boss.playerId === playerId;
  const isMember = Boolean(battle.party[playerId]);
  if (!isBoss && !isMember) {
    throw requestError("Join the party or claim the verified boss role before challenging.", 403, "BOSS_BATTLE_ACCESS_REQUIRED");
  }
  if (participants.length === 0) {
    throw requestError("At least one member must enter the arena before a challenge can begin.", 409, "BOSS_PARTY_EMPTY");
  }
  if (participants.length > 1 && participants.some((member) => !member.ready)) {
    throw requestError("Every member must be ready before a two- or three-player challenge begins.", 409, "BOSS_PARTY_NOT_READY");
  }

  const partySize = participants.length;
  const partyPower = partySize * 55;
  const bossPower = 100 + Math.max(0, partySize - BOSS_MINIMUM_PARTY_SIZE) * 35;
  const victory = partySize >= BOSS_MINIMUM_PARTY_SIZE && partyPower >= bossPower;
  const now = new Date().toISOString();
  battle.battleNumber += 1;
  battle.lastBattle = {
    battleId: `${battle.poolId}_battle_${battle.battleNumber}`,
    initiatedBy: isBoss ? "boss" : "members",
    partySize,
    partyPower,
    bossPower,
    result: victory ? "member-victory" : "boss-victory",
    difficulty: partySize === 1 ? "impossible-solo" : partySize === 2 ? "hard" : "nominal",
    summary: partySize === 1
      ? "A lone member cannot defeat a 1-of-1 boss. Recruit at least one ally."
      : partySize === 2
        ? "Two coordinated members defeated the boss at hard difficulty."
        : "Three members completed the intended boss encounter.",
    resolvedAt: now
  };
  battle.status = "resolved";
  for (const member of participants) member.ready = false;
  battle.updatedAt = now;
  persist({ required: true });
  return { success: true, battle: publicBattle(battle, playerId) };
}

// A private practice encounter for verified 1-of-1 holders.  It deliberately
// does not alter the shared party, vault, or raid matchmaking state.
export function startBossPractice(payload = {}) {
  const battle = getOrCreateBattle(payload.poolId);
  const playerId = requirePlayerId(payload.playerId);
  const wallet = wallets.get(playerId);
  if (!wallet?.oneOfOneEligible || !wallet.ownershipVerified) {
    throw requestError(
      "Verify ownership of a supported 1-of-1 wallet to unlock Boss vs AI Practice.",
      403,
      "ONE_OF_ONE_HOLDER_REQUIRED"
    );
  }

  const now = new Date().toISOString();
  battle.battleNumber += 1;
  battle.lastBattle = {
    battleId: `${battle.poolId}_practice_${battle.battleNumber}`,
    initiatedBy: "verified-boss-holder",
    partySize: 3,
    partyPower: 165,
    bossPower: 180,
    result: "boss-victory",
    difficulty: "standard-ai-practice",
    summary: "Practice complete: your verified 1-of-1 Boss defeated the Standard AI party (180 HP, 3 AP).",
    resolvedAt: now,
    practice: true,
    bossHp: 180,
    actionPoints: 3
  };
  battle.updatedAt = now;
  persist({ required: true });
  return { success: true, battle: publicBattle(battle, playerId) };
}

export function linkWalletAccount(payload = {}) {
  loadIfNeeded();
  const playerId = requirePlayerId(payload.playerId);
  const walletAddress = normalizeWalletAddress(payload.walletAddress);
  const allowlisted = oneOfOneAllowlist().has(walletAddress.toLowerCase());
  const now = new Date().toISOString();
  const wallet = {
    playerId,
    walletAddress,
    displayAddress: shortWallet(walletAddress),
    network: "ApeChain",
    connectionState: "preview-linked",
    signatureVerified: false,
    ownershipVerified: allowlisted,
    oneOfOneEligible: allowlisted,
    holderRole: allowlisted ? "1-of-1 Boss" : "Member",
    eligibilitySource: allowlisted ? "server allowlist" : "not verified",
    updatedAt: now
  };
  wallets.set(playerId, wallet);
  persist({ required: true });
  return {
    success: true,
    wallet: publicWallet(wallet),
    message: allowlisted
      ? "Wallet linked in preview mode. Server allowlist confirms 1-of-1 boss eligibility; live signature verification is still pending."
      : "Wallet linked in preview mode. This account remains a member until production signature and ownership verification are connected."
  };
}

export function linkVerifiedWalletAccount(payload = {}) {
  loadIfNeeded();
  const playerId = requirePlayerId(payload.playerId);
  const walletAddress = normalizeWalletAddress(payload.walletAddress);
  if (payload.signatureVerified !== true) {
    throw requestError("A verified wallet signature is required.", 401, "WALLET_SIGNATURE_REQUIRED");
  }
  const now = new Date().toISOString();
  const assets = Array.isArray(payload.assets) ? payload.assets.slice(0, 12) : [];
  const wallet = {
    playerId,
    walletAddress,
    displayAddress: shortWallet(walletAddress),
    network: payload.network || "ApeChain",
    chainId: Number(payload.chainId) || 33139,
    contractAddress: payload.contractAddress || "",
    connectionState: "wallet-connected",
    signatureVerified: true,
    ownershipVerified: payload.ownershipVerified === true,
    oneOfOneEligible: payload.oneOfOneEligible === true && payload.ownershipVerified === true,
    holderRole: payload.oneOfOneEligible === true && payload.ownershipVerified === true ? "1-of-1 Boss" : "Member",
    eligibilitySource: payload.eligibilitySource || "ApeChain",
    originalsBalance: Math.max(0, Number(payload.originalsBalance) || 0),
    assets,
    verificationError: String(payload.verificationError || "").slice(0, 240),
    updatedAt: now
  };
  wallets.set(playerId, wallet);
  persist({ required: true });
  return {
    success: true,
    wallet: publicWallet(wallet),
    message: wallet.oneOfOneEligible
      ? "Wallet signature and 1-of-1 ownership verified on ApeChain. Boss Mode is unlocked."
      : wallet.ownershipVerified
        ? `Wallet verified with ${wallet.originalsBalance} Appreciators Original${wallet.originalsBalance === 1 ? "" : "s"}.`
        : wallet.verificationError || "Wallet signature verified, but ownership verification is temporarily unavailable."
  };
}

export function getWalletAccount(payload = {}) {
  loadIfNeeded();
  const playerId = requirePlayerId(payload.playerId);
  const wallet = publicWallet(wallets.get(playerId) || emptyWallet(playerId));
  return {
    success: true,
    wallet,
    verificationBoundary: wallet.signatureVerified
      ? "Wallet control and ApeChain ownership are verified server-side. Boss Mode is granted only while a supported 1-of-1 token is owned by this wallet."
      : "Preview links never grant production boss eligibility. Connect and sign the one-time wallet challenge for server-side ApeChain ownership verification."
  };
}

export function disconnectWalletAccount(payload = {}) {
  loadIfNeeded();
  const playerId = requirePlayerId(payload.playerId);
  wallets.delete(playerId);
  for (const battle of battles.values()) {
    if (battle.boss.playerId === playerId) battle.boss = provisionalBoss();
  }
  persist({ required: true });
  return { success: true, wallet: publicWallet(emptyWallet(playerId)) };
}

export function resetBossBattlesForTests() {
  battles.clear();
  wallets.clear();
  loaded = true;
}

function getOrCreateBattle(rawPoolId) {
  loadIfNeeded();
  const poolId = safePoolId(rawPoolId);
  let battle = battles.get(poolId);
  if (!battle) {
    const now = new Date().toISOString();
    battle = {
      poolId,
      status: "funding",
      boss: provisionalBoss(),
      party: {},
      battleNumber: 0,
      lastBattle: null,
      createdAt: now,
      updatedAt: now
    };
    battles.set(poolId, battle);
  }
  const pool = getBossPoolStatus(poolId);
  if (pool.unlocked && battle.status === "funding") battle.status = "summoned";
  return battle;
}

function publicBattle(battle, rawPlayerId) {
  const pool = getBossPoolStatus(battle.poolId);
  const playerId = safePlayerId(rawPlayerId);
  const party = Object.values(battle.party || {}).sort((left, right) => left.joinedAt.localeCompare(right.joinedAt));
  const partySize = party.length;
  const currentWallet = playerId ? wallets.get(playerId) : null;
  return {
    pool,
    poolId: battle.poolId,
    status: pool.unlocked ? battle.status : "funding",
    rules: {
      soloAlwaysLoses: true,
      verifiedHolderPracticeAvailable: true,
      minimumPartySize: BOSS_MINIMUM_PARTY_SIZE,
      nominalPartySize: BOSS_NOMINAL_PARTY_SIZE,
      maximumPartySize: BOSS_MAXIMUM_PARTY_SIZE
    },
    boss: { ...battle.boss },
    party,
    partySize,
    readyCount: party.filter((member) => member.ready).length,
    currentPlayer: {
      playerId,
      inParty: Boolean(playerId && battle.party[playerId]),
      ready: Boolean(playerId && battle.party[playerId]?.ready),
      isBoss: Boolean(playerId && battle.boss.playerId === playerId),
      oneOfOneEligible: Boolean(currentWallet?.oneOfOneEligible && currentWallet?.ownershipVerified)
    },
    canStart: partySize > 0 && (partySize === 1 || party.every((member) => member.ready)),
    lastBattle: battle.lastBattle,
    updatedAt: battle.updatedAt
  };
}

function provisionalBoss() {
  return {
    playerId: "",
    displayName: "Summoned 1-of-1 Boss",
    walletDisplay: "Holder seat open",
    verifiedOneOfOne: false,
    controlMode: "provisional-ai"
  };
}

function publicWallet(wallet) {
  return { ...wallet };
}

function emptyWallet(playerId) {
  return {
    playerId,
    walletAddress: "",
    displayAddress: "Not connected",
    network: "ApeChain",
    connectionState: "disconnected",
    signatureVerified: false,
    ownershipVerified: false,
    oneOfOneEligible: false,
    holderRole: "Member",
    eligibilitySource: "not connected",
    originalsBalance: 0,
    assets: [],
    verificationError: "",
    updatedAt: ""
  };
}

function oneOfOneAllowlist() {
  const configured = String(process.env.BOSS_ONE_OF_ONE_WALLETS || "")
    .split(",")
    .map((value) => value.trim().toLowerCase())
    .filter(Boolean);
  if (process.env.NODE_ENV !== "production") {
    configured.push("0x1111111111111111111111111111111111110001");
  }
  return new Set(configured);
}

function requireSummoned(battle) {
  if (!getBossPoolStatus(battle.poolId).unlocked) {
    throw requestError("Members must finish pooling Appreciation Shards before the 1-of-1 boss can be summoned.", 409, "BOSS_NOT_SUMMONED");
  }
}

function normalizeWalletAddress(value) {
  const walletAddress = String(value || "").trim();
  if (!/^0x[a-fA-F0-9]{40}$/.test(walletAddress)) {
    throw requestError("Enter a valid 42-character EVM wallet address.", 400, "INVALID_WALLET_ADDRESS");
  }
  return walletAddress;
}

function shortWallet(walletAddress) {
  return `${walletAddress.slice(0, 6)}...${walletAddress.slice(-4)}`;
}

function requirePlayerId(value) {
  const playerId = safePlayerId(value);
  if (!playerId) throw requestError("playerId is required.", 400, "PLAYER_ID_REQUIRED");
  return playerId;
}

function safePlayerId(value) {
  return String(value || "").trim().replace(/[^a-zA-Z0-9_-]/g, "").slice(0, 64);
}

function safePoolId(value) {
  return String(value || "alpha_boss").trim().replace(/[^a-zA-Z0-9_-]/g, "").slice(0, 64) || "alpha_boss";
}

function safeDisplayName(value) {
  return String(value || "Member").trim().replace(/[<>]/g, "").slice(0, 24) || "Member";
}

function requestError(message, statusCode, errorCode) {
  return Object.assign(new Error(message), { statusCode, errorCode });
}

function loadIfNeeded() {
  if (loaded) return;
  loaded = true;
  if (!persistenceEnabled) return;
  try {
    const stored = JSON.parse(readFileSync(persistencePath, "utf8"));
    for (const battle of stored.battles || []) if (battle?.poolId) battles.set(battle.poolId, battle);
    for (const wallet of stored.wallets || []) if (wallet?.playerId) wallets.set(wallet.playerId, wallet);
  } catch (error) {
    if (error.code !== "ENOENT") console.warn(`Boss battle persistence could not load ${persistencePath}: ${error.message}`);
  }
}

function persist({ required = false } = {}) {
  if (!persistenceEnabled) return true;
  const temporaryPath = `${persistencePath}.${process.pid}.tmp`;
  try {
    mkdirSync(dirname(persistencePath), { recursive: true });
    writeFileSync(temporaryPath, JSON.stringify({
      savedAt: new Date().toISOString(),
      battles: [...battles.values()],
      wallets: [...wallets.values()]
    }, null, 2));
    renameSync(temporaryPath, persistencePath);
    return true;
  } catch (error) {
    try { rmSync(temporaryPath, { force: true }); } catch { /* best effort */ }
    if (required) throw requestError(`Boss battle persistence failed: ${error.message}`, 503, "BOSS_PERSISTENCE_FAILED");
    return false;
  }
}
