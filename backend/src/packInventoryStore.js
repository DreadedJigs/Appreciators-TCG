import crypto from "node:crypto";
import { mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import {
  ATTUNEMENT_SHARD_COST,
  generatePackReward,
  normalizePackAttunement
} from "./packRewardService.js";
import { getPackDefinition } from "./packRepository.js";

const players = new Map();
const bossPools = new Map();
const persistenceEnabled =
  process.env.PACK_INVENTORY_PERSISTENCE === "true" ||
  (process.env.NODE_ENV === "production" && process.env.PACK_INVENTORY_PERSISTENCE !== "false");
const persistencePath = resolve(process.env.PACK_INVENTORY_STORE_PATH || "data/runtime/pack-inventory.json");
export const STARTER_PACK_GRANT_COUNT = 3;
export const MATCH_WIN_SHARD_REWARD = 69;
export const RANKED_LOSS_SHARD_PENALTY = 5;
export const BOSS_BATTLE_UNLOCK_COST = 2000;
const STARTER_GRANT_VERSION = 1;
const TEST_PACK_MODE =
  process.env.PACK_TEST_MODE_ALWAYS_STOCKED !== "false" &&
  (process.env.NODE_ENV !== "production" || process.env.PACK_TEST_GRANTS_ENABLED === "true");
let loaded = false;

export function getPackInventory(playerId) {
  return publicInventory(getOrCreatePlayer(playerId));
}

export function grantTestPack(payload = {}) {
  assertPackTestToolsAccess(payload);

  const pack = getPackDefinition(payload.packId);
  if (!pack) {
    throw Object.assign(new Error("Unknown pack definition."), { statusCode: 404 });
  }

  const player = getOrCreatePlayer(payload.playerId);
  const before = clonePlayer(player);
  const count = Math.max(1, Math.min(10, Number.parseInt(payload.count, 10) || 1));
  try {
    player.packs[pack.id] = (player.packs[pack.id] || 0) + count;
    player.updatedAt = new Date().toISOString();
    persist({ required: true });
  } catch (error) {
    players.set(player.playerId, before);
    throw error;
  }

  return {
    alphaOnly: true,
    grantedPackId: pack.id,
    grantedCount: count,
    inventory: publicInventory(player)
  };
}

export function resetTestPackInventory(payload = {}) {
  assertPackTestToolsAccess(payload);

  const playerId = safePlayerId(payload.playerId);
  if (!playerId) {
    throw Object.assign(new Error("playerId is required."), { statusCode: 400 });
  }

  loadIfNeeded();
  const previous = players.has(playerId) ? clonePlayer(players.get(playerId)) : null;
  const player = createPlayer(playerId);
  players.set(playerId, player);
  try {
    persist({ required: true });
  } catch (error) {
    if (previous) {
      players.set(playerId, previous);
    } else {
      players.delete(playerId);
    }
    throw error;
  }
  return {
    alphaOnly: true,
    inventory: publicInventory(player)
  };
}

export function openOwnedPack(payload = {}) {
  const pack = getPackDefinition(payload.packId);
  if (!pack) {
    throw Object.assign(new Error("Unknown pack definition."), { statusCode: 404 });
  }

  const player = getOrCreatePlayer(payload.playerId);
  const requestId = safeRequestId(payload.requestId);
  if (!requestId) {
    throw Object.assign(new Error("requestId is required for idempotent pack opening."), { statusCode: 400 });
  }

  player.openRequests = player.openRequests || {};
  const attunement = normalizePackAttunement(pack, payload.attunement);
  if (player.openRequests[requestId]) {
    const existing = player.openRequests[requestId];
    if (existing.reward?.packId !== pack.id || existing.reward?.attunement !== attunement) {
      throw Object.assign(new Error("requestId was already used for a different pack opening request."), {
        statusCode: 409,
        errorCode: "REQUEST_ID_CONFLICT"
      });
    }

    return {
      ...existing,
      idempotentReplay: true,
      inventory: publicInventory(player)
    };
  }

  if ((player.packs[pack.id] || 0) < 1) {
    throw Object.assign(new Error("Player does not own this pack."), { statusCode: 409 });
  }

  const attunementCost = attunement === "Neutral" ? 0 : ATTUNEMENT_SHARD_COST;
  if (player.appreciationShards < attunementCost) {
    throw Object.assign(
      new Error(`Lane attunement costs ${attunementCost} Appreciation Shards; balance is ${player.appreciationShards}.`),
      { statusCode: 409, errorCode: "INSUFFICIENT_SHARDS" }
    );
  }

  const reward = generatePackReward({
    packId: pack.id,
    attunement,
    ownedCardIds: Object.keys(player.cards)
  });
  validateGeneratedReward(reward, pack.id);
  const envelope = signReward(player.playerId, requestId, reward);

  const before = clonePlayer(player);
  let finalizedResponse;
  try {
    player.packs[pack.id] -= 1;
    player.appreciationShards -= attunementCost;
    applyReward(player, reward);
    ensureThreeTestPacks(player);
    player.updatedAt = new Date().toISOString();
    const inventory = publicInventory(player);
    finalizedResponse = {
      ...envelope,
      success: true,
      requestId,
      packId: pack.id,
      attunement,
      attunementChancePercent: reward.attunementChancePercent,
      attunementSucceeded: reward.attunementSucceeded,
      attunementShardsSpent: reward.attunementShardsSpent,
      packShardsAwarded: reward.packShardsAwarded,
      rewards: reward.cards,
      totalShardsAwarded: reward.totalShardsAwarded,
      netShardChange: reward.totalShardsAwarded - reward.attunementShardsSpent,
      remainingPackCount: inventory.packs.find((entry) => entry.packId === pack.id)?.count || 0,
      totalShardBalance: inventory.appreciationShards,
      openedAt: reward.openedAtUtc,
      reward
    };
    player.openRequests[requestId] = finalizedResponse;
    persist({ required: true });
  } catch (error) {
    players.set(player.playerId, before);
    throw error;
  }

  return {
    ...finalizedResponse,
    idempotentReplay: false,
    inventory: publicInventory(player)
  };
}

export function purchasePack(payload = {}) {
  const pack = getPackDefinition(payload.packId);
  if (!pack || pack.purchasable !== true || Number(pack.shardCost) <= 0) {
    throw Object.assign(new Error("This pack is not available for shard purchase."), {
      statusCode: 409,
      errorCode: "PACK_NOT_PURCHASABLE"
    });
  }

  const player = getOrCreatePlayer(payload.playerId);
  const requestId = safeRequestId(payload.requestId);
  if (!requestId) {
    throw Object.assign(new Error("requestId is required for idempotent pack purchase."), { statusCode: 400 });
  }

  player.purchaseRequests = player.purchaseRequests || {};
  if (player.purchaseRequests[requestId]) {
    const existing = player.purchaseRequests[requestId];
    if (existing.packId !== pack.id) {
      throw Object.assign(new Error("requestId was already used for a different pack purchase."), {
        statusCode: 409,
        errorCode: "REQUEST_ID_CONFLICT"
      });
    }
    return { ...existing, idempotentReplay: true, inventory: publicInventory(player) };
  }

  const shardCost = Number.parseInt(pack.shardCost, 10);
  if (player.appreciationShards < shardCost) {
    throw Object.assign(
      new Error(`${pack.displayName || pack.name} costs ${shardCost} Appreciation Shards; balance is ${player.appreciationShards}.`),
      { statusCode: 409, errorCode: "INSUFFICIENT_SHARDS" }
    );
  }

  const before = clonePlayer(player);
  let response;
  try {
    player.appreciationShards -= shardCost;
    player.packs[pack.id] = (player.packs[pack.id] || 0) + 1;
    player.updatedAt = new Date().toISOString();
    response = {
      success: true,
      requestId,
      packId: pack.id,
      packName: pack.displayName || pack.name,
      shardCost,
      remainingShards: player.appreciationShards,
      quantityOwned: player.packs[pack.id]
    };
    player.purchaseRequests[requestId] = response;
    persist({ required: true });
  } catch (error) {
    players.set(player.playerId, before);
    throw error;
  }

  return { ...response, idempotentReplay: false, inventory: publicInventory(player) };
}

export function awardMatchResultShards(payload = {}) {
  const player = getOrCreatePlayer(payload.playerId);
  const matchId = safeRequestId(payload.matchId);
  if (!matchId) {
    throw Object.assign(new Error("matchId is required for a match reward."), { statusCode: 400 });
  }
  const result = String(payload.result || "").trim();
  const mode = String(payload.mode || "Casual").trim() || "Casual";
  if (!["Victory", "Defeat", "Draw"].includes(result)) {
    throw Object.assign(new Error("result must be Victory, Defeat, or Draw."), { statusCode: 400 });
  }

  player.matchWinRewards = player.matchWinRewards || {};
  if (player.matchWinRewards[matchId]) {
    return {
      ...player.matchWinRewards[matchId],
      totalShardBalance: player.appreciationShards,
      idempotentReplay: true,
      inventory: publicInventory(player)
    };
  }

  const before = clonePlayer(player);
  let response;
  try {
    const requestedChange = result === "Victory"
      ? MATCH_WIN_SHARD_REWARD
      : result === "Defeat" && mode.toLowerCase() === "ranked"
        ? -RANKED_LOSS_SHARD_PENALTY
        : 0;
    const startingBalance = player.appreciationShards;
    player.appreciationShards = Math.max(0, startingBalance + requestedChange);
    const shardsChanged = player.appreciationShards - startingBalance;
    player.updatedAt = new Date().toISOString();
    response = {
      success: true,
      matchId,
      result,
      mode,
      shardsAwarded: Math.max(0, shardsChanged),
      shardsChanged,
      rankedLossPenalty: Math.max(0, -shardsChanged),
      totalShardBalance: player.appreciationShards
    };
    player.matchWinRewards[matchId] = response;
    persist({ required: true });
  } catch (error) {
    players.set(player.playerId, before);
    throw error;
  }

  return { ...response, idempotentReplay: false, inventory: publicInventory(player) };
}

export function awardMatchWinShards(payload = {}) {
  if (String(payload.result || "") !== "Victory") {
    throw Object.assign(new Error("Only a Victory result can receive the legacy match-win reward."), {
      statusCode: 409,
      errorCode: "MATCH_NOT_WON"
    });
  }
  return awardMatchResultShards({ ...payload, result: "Victory", mode: payload.mode || "Casual" });
}

export function getBossPoolStatus(rawPoolId = "alpha_boss") {
  loadIfNeeded();
  return publicBossPool(getOrCreateBossPool(rawPoolId));
}

export function contributeBossShards(payload = {}) {
  const player = getOrCreatePlayer(payload.playerId);
  const pool = getOrCreateBossPool(payload.poolId);
  const requestId = safeRequestId(payload.requestId);
  if (!requestId) {
    throw Object.assign(new Error("requestId is required for an idempotent boss contribution."), { statusCode: 400 });
  }

  player.bossContributionRequests = player.bossContributionRequests || {};
  if (player.bossContributionRequests[requestId]) {
    return {
      ...player.bossContributionRequests[requestId],
      idempotentReplay: true,
      inventory: publicInventory(player),
      pool: publicBossPool(pool)
    };
  }
  if (pool.unlocked) {
    throw Object.assign(new Error("This boss vault is already unlocked."), {
      statusCode: 409,
      errorCode: "BOSS_ALREADY_UNLOCKED"
    });
  }

  const remaining = Math.max(0, BOSS_BATTLE_UNLOCK_COST - pool.totalShards);
  const requested = Number.parseInt(payload.amount, 10);
  const amount = Math.min(remaining, Math.max(1, Number.isFinite(requested) ? requested : 100));
  if (player.appreciationShards < amount) {
    throw Object.assign(
      new Error(`Boss contribution requires ${amount} Appreciation Shards; balance is ${player.appreciationShards}.`),
      { statusCode: 409, errorCode: "INSUFFICIENT_SHARDS" }
    );
  }

  const beforePlayer = clonePlayer(player);
  const beforePool = JSON.parse(JSON.stringify(pool));
  let response;
  try {
    player.appreciationShards -= amount;
    pool.totalShards += amount;
    pool.contributions[player.playerId] = (pool.contributions[player.playerId] || 0) + amount;
    pool.unlocked = pool.totalShards >= BOSS_BATTLE_UNLOCK_COST;
    pool.updatedAt = new Date().toISOString();
    player.updatedAt = pool.updatedAt;
    response = {
      success: true,
      requestId,
      amountContributed: amount,
      totalShardBalance: player.appreciationShards,
      unlocked: pool.unlocked
    };
    player.bossContributionRequests[requestId] = response;
    persist({ required: true });
  } catch (error) {
    players.set(player.playerId, beforePlayer);
    bossPools.set(pool.poolId, beforePool);
    throw error;
  }

  return {
    ...response,
    idempotentReplay: false,
    inventory: publicInventory(player),
    pool: publicBossPool(pool)
  };
}

export function verifySignedReward(payloadBase64, signature) {
  const expected = signPayload(payloadBase64);
  const supplied = Buffer.from(String(signature || ""), "hex");
  const expectedBuffer = Buffer.from(expected, "hex");
  return supplied.length === expectedBuffer.length && crypto.timingSafeEqual(supplied, expectedBuffer);
}

export function resetPackInventoryForTests() {
  players.clear();
  bossPools.clear();
  loaded = true;
}

function getOrCreatePlayer(rawPlayerId) {
  loadIfNeeded();
  const playerId = safePlayerId(rawPlayerId);
  if (!playerId) {
    throw Object.assign(new Error("playerId is required."), { statusCode: 400 });
  }

  let player = players.get(playerId);
  if (!player) {
    player = createPlayer(playerId);
    players.set(playerId, player);
    try {
      persist({ required: true });
    } catch (error) {
      players.delete(playerId);
      throw error;
    }
  }

  player.openRequests = player.openRequests || {};
  player.purchaseRequests = player.purchaseRequests || {};
  player.matchWinRewards = player.matchWinRewards || {};
  player.bossContributionRequests = player.bossContributionRequests || {};
  player.packs = player.packs || {};
  player.cards = player.cards || {};
  player.appreciationShards = Number.isFinite(player.appreciationShards) ? player.appreciationShards : 0;
  if (player.starterGrantVersion !== STARTER_GRANT_VERSION) {
    player.packs.starter_appreciation_pack = Math.max(
      Number(player.packs.starter_appreciation_pack) || 0,
      STARTER_PACK_GRANT_COUNT
    );
    player.starterGrantVersion = STARTER_GRANT_VERSION;
    player.updatedAt = new Date().toISOString();
    persist({ required: true });
  }
  ensureThreeTestPacks(player);

  return player;
}

function ensureThreeTestPacks(player) {
  if (!TEST_PACK_MODE || !player) {
    return;
  }

  player.packs = player.packs || {};
  player.packs.starter_appreciation_pack = Math.max(
    STARTER_PACK_GRANT_COUNT,
    Number(player.packs.starter_appreciation_pack) || 0
  );
}

function validateGeneratedReward(reward, expectedPackId) {
  if (!reward || reward.packId !== expectedPackId || !Array.isArray(reward.cards) || reward.cards.length !== 5) {
    throw Object.assign(new Error("[PackOpening] Generated reward failed pack identity or slot-count validation."), { statusCode: 500 });
  }

  for (const item of reward.cards) {
    if (!item?.card?.id || !item.card.name || !item.card.rarityLabel || !item.card.laneLabel) {
      throw Object.assign(new Error("[PackOpening] Generated reward contains missing card data."), { statusCode: 500 });
    }
  }
}

function applyReward(player, reward) {
  player.appreciationShards += reward.packShardsAwarded;
  for (const item of reward.cards) {
    const existing = player.cards[item.card.id];
    if (existing) {
      existing.ownedCount += 1;
      existing.duplicateCount += 1;
      existing.lastAcquiredUtc = reward.openedAtUtc;
    } else {
      player.cards[item.card.id] = {
        cardId: item.card.id,
        ownedCount: 1,
        duplicateCount: 0,
        firstAcquiredUtc: reward.openedAtUtc,
        lastAcquiredUtc: reward.openedAtUtc
      };
    }

    player.appreciationShards += item.shardsAwarded;
  }
}

function signReward(playerId, requestId, reward) {
  const signedPayload = {
    version: "pack-reward-v1",
    playerId,
    requestId,
    reward
  };
  const payloadBase64 = Buffer.from(JSON.stringify(signedPayload), "utf8").toString("base64url");
  return {
    version: signedPayload.version,
    algorithm: "HMAC-SHA256",
    keyId: process.env.PACK_REWARD_SIGNING_KEY_ID || "render-alpha-v1",
    payloadBase64,
    signature: signPayload(payloadBase64)
  };
}

function signPayload(payloadBase64) {
  const configured = String(process.env.PACK_REWARD_SIGNING_SECRET || "");
  if (!configured && process.env.NODE_ENV === "production") {
    throw Object.assign(new Error("PACK_REWARD_SIGNING_SECRET is not configured."), { statusCode: 503 });
  }

  const secret = configured || "appreciators-local-dev-pack-signing-secret";
  return crypto.createHmac("sha256", secret).update(payloadBase64).digest("hex");
}

function publicInventory(player) {
  const updatedAt = player.updatedAt;
  return {
    playerId: player.playerId,
    appreciationShards: player.appreciationShards,
    starterPacksGranted: STARTER_PACK_GRANT_COUNT,
    matchWinsRewarded: Object.values(player.matchWinRewards || {}).filter(entry => entry?.result === "Victory" || Number(entry?.shardsAwarded) > 0).length,
    ownedCardCount: Object.keys(player.cards).length,
    currency: {
      playerId: player.playerId,
      appreciationShards: player.appreciationShards,
      updatedAt
    },
    packs: Object.entries(player.packs).map(([packId, count]) => ({
      playerId: player.playerId,
      packId,
      count,
      quantityOwned: count,
      updatedAt
    })),
    cards: Object.values(player.cards).map((entry) => ({
      ...entry,
      playerId: player.playerId,
      quantityOwned: entry.ownedCount,
      firstAcquiredAt: entry.firstAcquiredUtc,
      lastAcquiredAt: entry.lastAcquiredUtc
    })),
    updatedAt
  };
}

function createPlayer(playerId) {
  const now = new Date().toISOString();
  return {
    playerId,
    appreciationShards: 0,
    packs: { starter_appreciation_pack: STARTER_PACK_GRANT_COUNT },
    cards: {},
    openRequests: {},
    purchaseRequests: {},
    matchWinRewards: {},
    bossContributionRequests: {},
    starterGrantVersion: STARTER_GRANT_VERSION,
    createdAt: now,
    updatedAt: now
  };
}

function getOrCreateBossPool(rawPoolId) {
  loadIfNeeded();
  const poolId = String(rawPoolId || "alpha_boss").trim().replace(/[^a-zA-Z0-9_-]/g, "").slice(0, 64) || "alpha_boss";
  let pool = bossPools.get(poolId);
  if (!pool) {
    pool = {
      poolId,
      targetShards: BOSS_BATTLE_UNLOCK_COST,
      totalShards: 0,
      unlocked: false,
      contributions: {},
      updatedAt: new Date().toISOString()
    };
    bossPools.set(poolId, pool);
  }
  return pool;
}

function publicBossPool(pool) {
  return {
    poolId: pool.poolId,
    targetShards: BOSS_BATTLE_UNLOCK_COST,
    totalShards: pool.totalShards,
    remainingShards: Math.max(0, BOSS_BATTLE_UNLOCK_COST - pool.totalShards),
    unlocked: Boolean(pool.unlocked),
    contributors: Object.keys(pool.contributions || {}).length,
    updatedAt: pool.updatedAt
  };
}

function clonePlayer(player) {
  return JSON.parse(JSON.stringify(player));
}

export function assertPackTestToolsAccess(payload) {
  const isProduction = process.env.NODE_ENV === "production";
  const explicitlyEnabled = process.env.PACK_TEST_GRANTS_ENABLED === "true";
  if (!isProduction && process.env.PACK_TEST_GRANTS_ENABLED !== "false") {
    return;
  }

  const expectedKey = String(process.env.PACK_TEST_ADMIN_KEY || "");
  const suppliedKey = String(payload?._testKey || "");
  const keyMatches = expectedKey.length > 0 && suppliedKey.length === expectedKey.length &&
    crypto.timingSafeEqual(Buffer.from(suppliedKey), Buffer.from(expectedKey));
  if (!explicitlyEnabled || !keyMatches) {
    throw Object.assign(new Error("Alpha pack test tools are disabled or require valid admin test access."), {
      statusCode: 403,
      errorCode: "PACK_TEST_TOOLS_FORBIDDEN"
    });
  }
}

function safePlayerId(value) {
  return String(value || "").trim().replace(/[^a-zA-Z0-9_-]/g, "").slice(0, 64);
}

function safeRequestId(value) {
  return String(value || "").trim().replace(/[^a-zA-Z0-9_-]/g, "").slice(0, 96);
}

function loadIfNeeded() {
  if (loaded) {
    return;
  }

  loaded = true;
  if (!persistenceEnabled) {
    return;
  }

  try {
    const stored = JSON.parse(readFileSync(persistencePath, "utf8"));
    for (const player of stored.players || []) {
      if (player?.playerId) {
        players.set(player.playerId, player);
      }
    }
    for (const pool of stored.bossPools || []) {
      if (pool?.poolId) {
        bossPools.set(pool.poolId, pool);
      }
    }
  } catch (error) {
    if (error.code !== "ENOENT") {
      console.warn(`Pack inventory persistence could not load ${persistencePath}: ${error.message}`);
    }
  }
}

function persist({ required = false } = {}) {
  if (!persistenceEnabled) {
    return true;
  }

  const temporaryPath = `${persistencePath}.${process.pid}.tmp`;
  try {
    mkdirSync(dirname(persistencePath), { recursive: true });
    writeFileSync(temporaryPath, JSON.stringify({
      savedAt: new Date().toISOString(),
      players: [...players.values()],
      bossPools: [...bossPools.values()]
    }, null, 2));
    renameSync(temporaryPath, persistencePath);
    return true;
  } catch (error) {
    try {
      rmSync(temporaryPath, { force: true });
    } catch {
      // Best-effort cleanup only.
    }

    const persistenceError = Object.assign(
      new Error(`Pack inventory persistence could not save ${persistencePath}: ${error.message}`),
      { statusCode: 503, errorCode: "PACK_PERSISTENCE_FAILED" }
    );
    if (required) {
      throw persistenceError;
    }

    console.error(persistenceError.message);
    return false;
  }
}
