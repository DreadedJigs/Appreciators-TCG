import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import express from "express";
import cors from "cors";
import { getAssetManifest, getPrototypeCards } from "./cardRepository.js";
import {
  getCardMetaSummary,
  getMetaCard,
  getMetaSeasons,
  queryMetaAbilities,
  queryMetaCards
} from "./cardMetaRepository.js";
import { getReleasePlan } from "./releasePlanRepository.js";
import {
  announceInvitePresence,
  challengeInvitePlayer,
  createInviteRoom,
  getInviteLobby,
  getInviteActions,
  getInviteMatchState,
  getInviteRoom,
  joinInviteRoom,
  recordInviteAction,
  reconnectInviteRoom,
  respondToInviteTermination,
  startInviteRoom
} from "./inviteRoomStore.js";
import { upsertProfile } from "./profileStore.js";
import { getOriginalsTokenMetadata, getOriginalsTraitCatalog } from "./originalsMetadataRepository.js";
import { getPublicPackCatalog } from "./packRepository.js";
import {
  assertPackTestToolsAccess,
  awardMatchResultShards,
  awardMatchWinShards,
  BOSS_BATTLE_UNLOCK_COST,
  contributeBossShards,
  getBossPoolStatus,
  getPackInventory,
  grantTestPack,
  MATCH_WIN_SHARD_REWARD,
  RANKED_LOSS_SHARD_PENALTY,
  openOwnedPack,
  purchasePack,
  resetTestPackInventory
} from "./packInventoryStore.js";
import {
  getPackOdds,
  MYSTERY_ODDS,
  PACK_SHARD_ODDS,
  simulatePackOpenings
} from "./packRewardService.js";
import {
  getMintLeaderboard,
  simulateDeclareWar,
  simulateMockMint,
  syncMockNftOwnership,
  verifyMockWallet
} from "./web3MockStore.js";

const publicDir = join(dirname(fileURLToPath(import.meta.url)), "..", "public");

export function createApp() {
  const app = express();
  const packOpenLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 30, scope: "pack-open" });
  const packTestLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 60, scope: "pack-test" });
  const packReadLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 120, scope: "pack-read" });
  const economyWriteLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 120, scope: "economy-write" });

  app.set("trust proxy", 1);
  app.use((req, res, next) => {
    res.header("Access-Control-Allow-Private-Network", "true");
    next();
  });
  app.use(cors());
  app.use(express.json({ limit: "256kb" }));
  app.use(express.static(publicDir));

  app.get("/", (_req, res) => {
    res.sendFile(join(publicDir, "mock-mint-simulator.html"));
  });

  app.get("/health", (_req, res) => {
    res.json({
      status: "ok",
      service: "appreciators-tcg-backend",
      phase: "1-prototype",
      timestamp: new Date().toISOString()
    });
  });

  app.post("/api/profile", (req, res) => {
    const profile = upsertProfile(req.body);
    res.status(201).json({
      profile,
      inventory: getPackInventory(profile.id),
      message: "Player profile and Appreciation inventory restored."
    });
  });

  app.post("/api/session/login", (req, res, next) => {
    try {
      const profile = upsertProfile(req.body);
      res.json({
        success: true,
        profile,
        inventory: getPackInventory(profile.id),
        message: "Player inventory restored from the shared save service."
      });
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/cards", async (_req, res, next) => {
    try {
      const cards = await getPrototypeCards();
      res.json(cards);
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/card-meta/summary", async (_req, res, next) => {
    try {
      res.json(await getCardMetaSummary());
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/card-meta/cards", async (req, res, next) => {
    try {
      res.json(await queryMetaCards(req.query));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/card-meta/cards/:tokenId", async (req, res, next) => {
    try {
      const card = await getMetaCard(req.params.tokenId);
      if (!card) {
        res.status(404).json({ error: "Card identity not found." });
        return;
      }
      res.json({ card });
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/card-meta/abilities", async (req, res, next) => {
    try {
      res.json(await queryMetaAbilities(req.query));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/card-meta/seasons", async (_req, res, next) => {
    try {
      res.json({ seasons: await getMetaSeasons() });
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/assets/manifest", async (_req, res, next) => {
    try {
      res.json(await getAssetManifest());
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/releases/plan", async (_req, res, next) => {
    try {
      res.json(await getReleasePlan());
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/nft/originals/traits", (_req, res, next) => {
    try {
      res.json(getOriginalsTraitCatalog());
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/nft/originals/token/:tokenId", (req, res, next) => {
    try {
      res.json({ token: getOriginalsTokenMetadata(req.params.tokenId) });
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/packs/catalog", (_req, res) => {
    res.json({
      ...getPublicPackCatalog(),
      mysteryOdds: MYSTERY_ODDS,
      shardEconomy: {
        neutralOpeningsOnly: true,
        starterPackGrantCount: 3,
        matchWinReward: MATCH_WIN_SHARD_REWARD,
        rankedLossPenalty: RANKED_LOSS_SHARD_PENALTY,
        bossBattleUnlockCost: BOSS_BATTLE_UNLOCK_COST,
        packShardOdds: PACK_SHARD_ODDS,
        nftHolderMonthlyDistribution: "TBD"
      }
    });
  });

  app.get("/api/packs/odds/:packId", packReadLimiter, (req, res, next) => {
    try {
      res.json(getPackOdds(req.params.packId));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/packs/inventory", packReadLimiter, (req, res, next) => {
    try {
      res.json({ inventory: getPackInventory(resolvePackPlayerId(req)) });
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/packs/inventory/:playerId", packReadLimiter, (req, res, next) => {
    try {
      res.json({ inventory: getPackInventory(req.params.playerId) });
    } catch (error) {
      next(error);
    }
  });

  const grantTestPackHandler = (req, res, next) => {
    try {
      res.status(201).json(grantTestPack(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  };
  app.post("/api/packs/grant-test", packTestLimiter, grantTestPackHandler);
  app.post("/api/packs/grant-test-pack", packTestLimiter, grantTestPackHandler);

  app.post("/api/packs/open", packOpenLimiter, (req, res, next) => {
    try {
      res.json(openOwnedPack(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/packs/purchase", packOpenLimiter, (req, res, next) => {
    try {
      res.json(purchasePack(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/economy/match-win", economyWriteLimiter, (req, res, next) => {
    try {
      res.json(awardMatchWinShards(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/economy/match-result", economyWriteLimiter, (req, res, next) => {
    try {
      res.json(awardMatchResultShards(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/economy/boss-pool", packReadLimiter, (req, res, next) => {
    try {
      res.json({ success: true, pool: getBossPoolStatus(req.query?.poolId) });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/economy/boss-contribute", economyWriteLimiter, (req, res, next) => {
    try {
      res.json(contributeBossShards(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  });

  const resetTestInventoryHandler = (req, res, next) => {
    try {
      res.json(resetTestPackInventory(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  };
  app.post("/api/packs/reset-test", packTestLimiter, resetTestInventoryHandler);
  app.post("/api/packs/reset-test-inventory", packTestLimiter, resetTestInventoryHandler);

  app.post("/api/packs/simulate", packTestLimiter, (req, res, next) => {
    try {
      const payload = packRequestPayload(req);
      assertPackTestToolsAccess(payload);
      res.json(simulatePackOpenings(payload));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/matchmaking/casual", (req, res) => {
    const username = String(req.body?.username || "Guest").slice(0, 24);

    res.json({
      matchId: `casual_${Date.now()}`,
      mode: "Casual",
      opponent: {
        id: "ai_phase_1",
        displayName: "Prototype AI",
        strategy: "Playable cards with lane-loss preference"
      },
      seed: Math.floor(Math.random() * 1000000),
      player: {
        username
      },
      message: "Mock matchmaking assignment created."
    });
  });

  app.post("/api/matchmaking/invite", (req, res, next) => {
    try {
      res.status(201).json(createInviteRoom(req.body));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/new", (req, res, next) => {
    try {
      res.status(201).json(createInviteRoom(queryInvitePayload(req.query)));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite-lobby/announce", (req, res, next) => {
    try {
      res.json(announceInvitePresence(queryInvitePayload(req.query)));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite-lobby/challenge", (req, res, next) => {
    try {
      res.status(201).json(challengeInvitePlayer(queryInvitePayload(req.query)));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite-lobby", (req, res, next) => {
    try {
      res.json(getInviteLobby(queryInvitePayload(req.query)));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/:inviteCode", (req, res, next) => {
    try {
      res.json({
        room: getInviteRoom(req.params.inviteCode)
      });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/matchmaking/invite/:inviteCode/join", (req, res, next) => {
    try {
      res.json(joinInviteRoom(req.params.inviteCode, req.body));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/:inviteCode/join-link", (req, res, next) => {
    try {
      res.json(joinInviteRoom(req.params.inviteCode, queryInvitePayload(req.query)));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/matchmaking/invite/:inviteCode/reconnect", (req, res, next) => {
    try {
      res.json(reconnectInviteRoom(req.params.inviteCode, req.body));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/:inviteCode/reconnect-link", (req, res, next) => {
    try {
      res.json(reconnectInviteRoom(req.params.inviteCode, queryInvitePayload(req.query)));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/matchmaking/invite/:inviteCode/start", (req, res, next) => {
    try {
      res.json(startInviteRoom(req.params.inviteCode, req.body));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/:inviteCode/start-link", (req, res, next) => {
    try {
      res.json(startInviteRoom(req.params.inviteCode, queryInvitePayload(req.query)));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/:inviteCode/actions", (req, res, next) => {
    try {
      res.json(getInviteActions(req.params.inviteCode, req.query.after));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/:inviteCode/state", (req, res, next) => {
    try {
      res.json(getInviteMatchState(req.params.inviteCode));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/matchmaking/invite/:inviteCode/termination", (req, res, next) => {
    try {
      res.json(respondToInviteTermination(req.params.inviteCode, req.body));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/:inviteCode/termination-link", (req, res, next) => {
    try {
      res.json(respondToInviteTermination(req.params.inviteCode, req.query));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/matchmaking/invite/:inviteCode/action", (req, res, next) => {
    try {
      res.json(recordInviteAction(req.params.inviteCode, queryActionPayload(req.query)));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/wallet/verify", (req, res) => {
    res.json(verifyMockWallet(req.body));
  });

  app.get("/api/wallet/verify-link", (req, res) => {
    res.json(verifyMockWallet(req.query));
  });

  app.post("/api/nft/sync", (req, res) => {
    res.json(syncMockNftOwnership(req.body));
  });

  app.get("/api/nft/sync-link", (req, res) => {
    res.json(syncMockNftOwnership(req.query));
  });

  app.post("/api/mint/simulate", (req, res) => {
    res.json(simulateMockMint(req.body));
  });

  app.get("/api/mint/simulate-link", (req, res) => {
    res.json(simulateMockMint(req.query));
  });

  app.post("/api/mint/war", (req, res, next) => {
    try {
      res.json(simulateDeclareWar(req.body));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/mint/war-link", (req, res, next) => {
    try {
      res.json(simulateDeclareWar(req.query));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/mint/leaderboard", (_req, res) => {
    res.json(getMintLeaderboard());
  });

  app.get("/api/mint/leaderboard-link", (_req, res) => {
    res.json(getMintLeaderboard());
  });

  app.use((req, res) => {
    res.status(404).json({
      error: "Not Found",
      path: req.path
    });
  });

  app.use((error, _req, res, _next) => {
    const statusCode = Number.isInteger(error.statusCode) ? error.statusCode : 500;
    if (statusCode === 500) {
      console.error(error);
    }

    res.status(statusCode).json({
      error: statusCode === 500 ? "Internal Server Error" : "Request Error",
      errorCode: error.errorCode || (statusCode === 500 ? "INTERNAL_ERROR" : "REQUEST_ERROR"),
      message: statusCode === 500 ? "Unexpected mock backend error." : error.message
    });
  });

  return app;
}

function resolvePackPlayerId(req) {
  return req.get("x-player-id") || req.body?.playerId || req.query?.playerId || req.params?.playerId;
}

function packRequestPayload(req) {
  return {
    ...(req.body || {}),
    playerId: resolvePackPlayerId(req),
    _testKey: req.get("x-pack-test-key") || req.body?._testKey
  };
}

function createRateLimiter({ windowMs, maxRequests, scope }) {
  const requests = new Map();
  return (req, _res, next) => {
    const now = Date.now();
    const key = `${scope}:${req.ip || req.socket?.remoteAddress || "unknown"}`;
    const current = requests.get(key);
    if (!current || current.resetAt <= now) {
      requests.set(key, { count: 1, resetAt: now + windowMs });
      next();
      return;
    }

    current.count += 1;
    if (current.count > maxRequests) {
      next(Object.assign(new Error("Too many pack requests. Please wait and retry with the same requestId."), {
        statusCode: 429,
        errorCode: "PACK_RATE_LIMITED"
      }));
      return;
    }

    next();
  };
}

function queryInvitePayload(query) {
  const deckIds = typeof query.deckIds === "string"
    ? query.deckIds.split(",").map((id) => id.trim()).filter(Boolean)
    : [];

  return {
    username: query.username,
    playerId: query.playerId,
    targetPlayerId: query.targetPlayerId,
    role: query.role,
    deckIds
  };
}

function queryActionPayload(query) {
  return {
    playerId: query.playerId,
    actionId: query.actionId,
    type: query.type,
    cardId: query.cardId,
    lane: query.lane,
    turn: query.turn
  };
}
