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
  awardTutorialCompletionShards,
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
import {
  claimBossRole,
  disconnectWalletAccount,
  grantAdminAccess,
  getBossBattleState,
  getWalletAccount,
  joinBossParty,
  leaveBossParty,
  linkWalletAccount,
  linkVerifiedWalletAccount,
  releaseBossRole,
  setBossPartyReady,
  startBossBattle,
  startBossPractice
} from "./bossBattleStore.js";
import { createWalletChallenge, getWalletVerificationStatus, verifyWalletChallenge } from "./walletAuthStore.js";
import { getSecureGameStore } from "./secureGameStore.js";

const publicDir = join(dirname(fileURLToPath(import.meta.url)), "..", "public");

export function createApp() {
  const app = express();
  const secureStore = getSecureGameStore();
  const packOpenLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 30, scope: "pack-open" });
  const packTestLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 60, scope: "pack-test" });
  const packReadLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 120, scope: "pack-read" });
  const economyWriteLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 120, scope: "economy-write" });
  const authenticationLimiter = createRateLimiter({ windowMs: 15 * 60_000, maxRequests: 20, scope: "authentication" });
  const onlineMatchLimiter = createRateLimiter({ windowMs: 60_000, maxRequests: 90, scope: "online-match" });

  app.set("trust proxy", 1);
  app.use((req, res, next) => {
    res.header("Access-Control-Allow-Private-Network", "true");
    res.header("X-Content-Type-Options", "nosniff");
    res.header("Referrer-Policy", "strict-origin-when-cross-origin");
    res.header("Permissions-Policy", "camera=(), microphone=(), geolocation=(), payment=()");
    next();
  });
  app.use(cors(createCorsOptions()));
  app.use(express.json({ limit: "256kb" }));
  app.use(express.static(publicDir));

  app.get("/", (_req, res) => {
    res.sendFile(join(publicDir, "mock-mint-simulator.html"));
  });

  app.get("/health", (_req, res) => {
    const storage = secureStore.getStatus();
    res.json({
      status: storage.configured ? "ok" : "degraded",
      service: "appreciators-tcg-backend",
      phase: "online-security-foundation",
      capabilities: {
        secureAccounts: storage.configured,
        cloudSaves: storage.durable,
        onlineMatches: storage.configured,
        walletVerification: getWalletVerificationStatus().configured
      },
      persistence: storage,
      timestamp: new Date().toISOString()
    });
  });

  const requireSecureAccount = (handler) => async (req, res, next) => {
    try {
      req.auth = await secureStore.verifySession(req.get("authorization"));
      await handler(req, res, next);
    } catch (error) {
      next(error);
    }
  };

  // The alpha client is still supported in local development. Production
  // economy, wallet, and multiplayer writes always derive the actor from the
  // opaque server session, never a playerId supplied by the browser.
  const requireProductionAccount = (handler) => async (req, res, next) => {
    try {
      if (process.env.NODE_ENV === "production") {
        req.auth = await secureStore.verifySession(req.get("authorization"));
      }
      await handler(req, res, next);
    } catch (error) {
      next(error);
    }
  };

  app.post("/api/auth/register", authenticationLimiter, async (req, res, next) => {
    try {
      const result = await secureStore.registerAccount(req.body);
      res.status(201).json({ success: true, ...result, message: "Secure account created. Store the session only on this device." });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/auth/login", authenticationLimiter, async (req, res, next) => {
    try {
      const result = await secureStore.loginAccount(req.body);
      res.json({ success: true, ...result, message: "Signed in securely." });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/auth/refresh", authenticationLimiter, requireSecureAccount(async (req, res) => {
    const result = await secureStore.refreshSession(req.get("authorization"));
    res.json({ success: true, ...result });
  }));

  app.post("/api/auth/logout", requireSecureAccount(async (req, res) => {
    res.json(await secureStore.revokeSession(req.get("authorization")));
  }));

  app.get("/api/account/me", requireSecureAccount(async (req, res) => {
    res.json({ success: true, account: req.auth.account, session: req.auth.session });
  }));

  app.get("/api/cloud-save", requireSecureAccount(async (req, res) => {
    res.json({ success: true, ...(await secureStore.getCloudSave(req.auth.account.id)) });
  }));

  app.put("/api/cloud-save", economyWriteLimiter, requireSecureAccount(async (req, res) => {
    const save = await secureStore.saveCloudSave(req.auth.account.id, req.body);
    res.json({ success: true, ...save });
  }));

  app.post("/api/online-matches/queue", onlineMatchLimiter, requireSecureAccount(async (req, res) => {
    res.json({ success: true, ...(await secureStore.queueMatch(req.auth.account.id, req.body)) });
  }));

  app.post("/api/online-matches/queue/cancel", onlineMatchLimiter, requireSecureAccount(async (req, res) => {
    res.json(await secureStore.cancelQueue(req.auth.account.id, req.body?.ticketId));
  }));

  app.get("/api/online-matches/:matchId", requireSecureAccount(async (req, res) => {
    res.json({ success: true, match: await secureStore.getMatch(req.auth.account.id, req.params.matchId) });
  }));

  app.get("/api/online-matches/:matchId/events", onlineMatchLimiter, requireSecureAccount(async (req, res) => {
    const response = await secureStore.waitForMatchEvents(
      req.auth.account.id,
      req.params.matchId,
      req.query.after,
      req.query.waitMs
    );
    res.json({ success: true, ...response });
  }));

  app.post("/api/online-matches/:matchId/actions", onlineMatchLimiter, requireSecureAccount(async (req, res) => {
    const result = await secureStore.applyMatchAction(req.auth.account.id, req.params.matchId, req.body);
    res.json({ success: true, ...result });
  }));

  app.post("/api/profile", (req, res, next) => {
    try {
      assertLegacyIdentityAllowed();
      const profile = upsertProfile(req.body);
      res.status(201).json({
        profile,
        inventory: getPackInventory(profile.id),
        message: "Player profile and Appreciation inventory restored."
      });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/session/login", (req, res, next) => {
    try {
      assertLegacyIdentityAllowed();
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

  app.get("/api/packs/inventory", packReadLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json({ inventory: getPackInventory(resolvePackPlayerId(req)) });
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/packs/inventory/:playerId", packReadLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json({ inventory: getPackInventory(req.params.playerId) });
    } catch (error) {
      next(error);
    }
  }));

  const grantTestPackHandler = (req, res, next) => {
    try {
      res.status(201).json(grantTestPack(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  };
  app.post("/api/packs/grant-test", packTestLimiter, grantTestPackHandler);
  app.post("/api/packs/grant-test-pack", packTestLimiter, grantTestPackHandler);

  app.post("/api/packs/open", packOpenLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json(openOwnedPack(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/packs/purchase", packOpenLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json(purchasePack(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/economy/match-win", economyWriteLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json(awardMatchWinShards(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/economy/match-result", economyWriteLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json(awardMatchResultShards(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/economy/tutorial-complete", economyWriteLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json(awardTutorialCompletionShards(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/economy/boss-pool", packReadLimiter, (req, res, next) => {
    try {
      res.json({ success: true, pool: getBossPoolStatus(req.query?.poolId) });
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/economy/boss-contribute", economyWriteLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json(contributeBossShards(packRequestPayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/boss-battles/:poolId", packReadLimiter, requireProductionAccount((req, res, next) => {
    try {
      res.json({
        success: true,
        battle: getBossBattleState({ ...req.query, playerId: req.auth?.account?.id || req.query?.playerId, poolId: req.params.poolId })
      });
    } catch (error) {
      next(error);
    }
  }));

  const bossMutation = (handler) => (req, res, next) => {
    try {
      res.json(handler({
        ...(req.body || {}),
        playerId: req.auth?.account?.id || req.body?.playerId,
        poolId: req.params.poolId || req.body?.poolId
      }));
    } catch (error) {
      next(error);
    }
  };
  app.post("/api/boss-battles/:poolId/join", economyWriteLimiter, requireProductionAccount(bossMutation(joinBossParty)));
  app.post("/api/boss-battles/:poolId/leave", economyWriteLimiter, requireProductionAccount(bossMutation(leaveBossParty)));
  app.post("/api/boss-battles/:poolId/ready", economyWriteLimiter, requireProductionAccount(bossMutation(setBossPartyReady)));
  app.post("/api/boss-battles/:poolId/claim-boss", economyWriteLimiter, requireProductionAccount(bossMutation(claimBossRole)));
  app.post("/api/boss-battles/:poolId/release-boss", economyWriteLimiter, requireProductionAccount(bossMutation(releaseBossRole)));
  app.post("/api/boss-battles/:poolId/challenge", economyWriteLimiter, requireProductionAccount(bossMutation(startBossBattle)));
  app.post("/api/boss-battles/:poolId/practice", economyWriteLimiter, requireProductionAccount(bossMutation(startBossPractice)));

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
    assertMockWeb3Allowed();
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

  app.post("/api/matchmaking/invite", requireProductionAccount((req, res, next) => {
    try {
      res.status(201).json(createInviteRoom(invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/new", requireProductionAccount((req, res, next) => {
    try {
      res.status(201).json(createInviteRoom(invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite-lobby/announce", requireProductionAccount((req, res, next) => {
    try {
      res.json(announceInvitePresence(invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite-lobby/challenge", requireProductionAccount((req, res, next) => {
    try {
      res.status(201).json(challengeInvitePlayer(invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite-lobby", requireProductionAccount((req, res, next) => {
    try {
      res.json(getInviteLobby(invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/:inviteCode", requireProductionAccount((req, res, next) => {
    try {
      res.json({
        room: getInviteRoom(req.params.inviteCode, req.auth?.account?.id)
      });
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/matchmaking/invite/:inviteCode/join", requireProductionAccount((req, res, next) => {
    try {
      res.json(joinInviteRoom(req.params.inviteCode, invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/:inviteCode/join-link", requireProductionAccount((req, res, next) => {
    try {
      res.json(joinInviteRoom(req.params.inviteCode, invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/matchmaking/invite/:inviteCode/reconnect", requireProductionAccount((req, res, next) => {
    try {
      res.json(reconnectInviteRoom(req.params.inviteCode, invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/:inviteCode/reconnect-link", requireProductionAccount((req, res, next) => {
    try {
      res.json(reconnectInviteRoom(req.params.inviteCode, invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/matchmaking/invite/:inviteCode/start", requireProductionAccount((req, res, next) => {
    try {
      res.json(startInviteRoom(req.params.inviteCode, invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/:inviteCode/start-link", requireProductionAccount((req, res, next) => {
    try {
      res.json(startInviteRoom(req.params.inviteCode, invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/:inviteCode/actions", requireProductionAccount((req, res, next) => {
    try {
      res.json(getInviteActions(req.params.inviteCode, req.query.after, req.auth?.account?.id));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/:inviteCode/state", requireProductionAccount((req, res, next) => {
    try {
      res.json(getInviteMatchState(req.params.inviteCode, req.auth?.account?.id));
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/matchmaking/invite/:inviteCode/termination", requireProductionAccount((req, res, next) => {
    try {
      res.json(respondToInviteTermination(req.params.inviteCode, invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/:inviteCode/termination-link", requireProductionAccount((req, res, next) => {
    try {
      res.json(respondToInviteTermination(req.params.inviteCode, invitePayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.get("/api/matchmaking/invite/:inviteCode/action", requireProductionAccount((req, res, next) => {
    try {
      res.json(recordInviteAction(req.params.inviteCode, actionPayload(req)));
    } catch (error) {
      next(error);
    }
  }));

  app.post("/api/wallet/verify", (req, res) => {
    assertMockWeb3Allowed();
    res.json(verifyMockWallet(req.body));
  });

  app.get("/api/wallet/verify-link", (req, res) => {
    assertMockWeb3Allowed();
    res.json(verifyMockWallet(req.query));
  });

  app.get("/api/wallet/account", packReadLimiter, requireSecureAccount(async (req, res) => {
    res.json(getWalletAccount({ playerId: req.auth.account.id }));
  }));

  app.post("/api/wallet/account/link", economyWriteLimiter, (req, res, next) => {
    try {
      assertMockWeb3Allowed();
      res.json(linkWalletAccount(req.body));
    } catch (error) {
      next(error);
    }
  });

  app.post("/api/wallet/account/challenge", economyWriteLimiter, requireSecureAccount(async (req, res) => {
    res.json(createWalletChallenge({ ...req.body, playerId: req.auth.account.id }));
  }));

  app.post("/api/wallet/account/verify", economyWriteLimiter, requireSecureAccount(async (req, res) => {
    const verified = await verifyWalletChallenge({ ...req.body, playerId: req.auth.account.id });
    res.json(linkVerifiedWalletAccount(verified));
  }));

  app.post("/api/wallet/account/disconnect", economyWriteLimiter, requireSecureAccount(async (req, res) => {
    res.json(disconnectWalletAccount({ playerId: req.auth.account.id }));
  }));

  app.post("/api/admin/wallets/add", economyWriteLimiter, requireSecureAccount(async (req, res) => {
    res.json(grantAdminAccess({ ...req.body, playerId: req.auth.account.id }));
  }));

  app.post("/api/nft/sync", (req, res) => {
    assertMockWeb3Allowed();
    res.json(syncMockNftOwnership(req.body));
  });

  app.get("/api/nft/sync-link", (req, res) => {
    assertMockWeb3Allowed();
    res.json(syncMockNftOwnership(req.query));
  });

  app.post("/api/mint/simulate", (req, res) => {
    assertMockWeb3Allowed();
    res.json(simulateMockMint(req.body));
  });

  app.get("/api/mint/simulate-link", (req, res) => {
    assertMockWeb3Allowed();
    res.json(simulateMockMint(req.query));
  });

  app.post("/api/mint/war", (req, res, next) => {
    try {
      assertMockWeb3Allowed();
      res.json(simulateDeclareWar(req.body));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/mint/war-link", (req, res, next) => {
    try {
      assertMockWeb3Allowed();
      res.json(simulateDeclareWar(req.query));
    } catch (error) {
      next(error);
    }
  });

  app.get("/api/mint/leaderboard", (_req, res) => {
    assertMockWeb3Allowed();
    res.json(getMintLeaderboard());
  });

  app.get("/api/mint/leaderboard-link", (_req, res) => {
    assertMockWeb3Allowed();
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
      message: statusCode === 500 ? "An unexpected service error occurred." : error.message,
      ...(Number.isInteger(error.currentVersion) ? { currentVersion: error.currentVersion } : {}),
      ...(error.updatedAt ? { updatedAt: error.updatedAt } : {})
    });
  });

  return app;
}

function resolvePackPlayerId(req) {
  return req.auth?.account?.id || req.get("x-player-id") || req.body?.playerId || req.query?.playerId || req.params?.playerId;
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
      next(Object.assign(new Error("Too many requests. Please wait and retry."), {
        statusCode: 429,
        errorCode: "PACK_RATE_LIMITED"
      }));
      return;
    }

    next();
  };
}

function assertLegacyIdentityAllowed() {
  if (process.env.NODE_ENV === "production") {
    throw Object.assign(new Error("A secure account is required for online services."), {
      statusCode: 410,
      errorCode: "LEGACY_IDENTITY_DISABLED"
    });
  }
}

function queryInvitePayload(query) {
  const deckIds = Array.isArray(query.deckIds)
    ? query.deckIds.map((id) => String(id || "").trim()).filter(Boolean)
    : typeof query.deckIds === "string"
      ? query.deckIds.split(",").map((id) => id.trim()).filter(Boolean)
      : [];

  return {
    username: query.username,
    playerId: query.playerId,
    targetPlayerId: query.targetPlayerId,
    role: query.role,
    decision: query.decision,
    deckIds
  };
}

function invitePayload(req) {
  const query = req.method === "GET" ? req.query : req.body || {};
  const payload = queryInvitePayload(query);
  if (req.auth?.account) {
    payload.playerId = req.auth.account.id;
    payload.username = req.auth.account.username;
  }
  return payload;
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

function actionPayload(req) {
  const payload = queryActionPayload(req.query || req.body || {});
  if (req.auth?.account) payload.playerId = req.auth.account.id;
  return payload;
}

function createCorsOptions() {
  const configuredOrigins = String(process.env.APP_ALLOWED_ORIGINS || "")
    .split(",")
    .map((origin) => origin.trim())
    .filter(Boolean);
  const developmentOrigins = ["http://localhost:8091", "http://127.0.0.1:8091", "http://localhost:8088", "http://127.0.0.1:8088"];
  const allowedOrigins = new Set([
    ...configuredOrigins,
    ...(process.env.NODE_ENV === "production" ? [] : developmentOrigins)
  ]);

  return {
    origin(origin, callback) {
      // Non-browser calls do not send Origin. Browser calls must be explicitly
      // allowed; this prevents a third-party page from using a player session.
      if (!origin || allowedOrigins.has(origin)) {
        callback(null, true);
        return;
      }
      callback(Object.assign(new Error("This browser origin is not authorized."), {
        statusCode: 403,
        errorCode: "ORIGIN_NOT_ALLOWED"
      }));
    },
    methods: ["GET", "POST", "PUT", "OPTIONS"],
    allowedHeaders: ["Authorization", "Content-Type", "If-Match", "X-Player-Id", "X-Pack-Test-Key"],
    maxAge: 600
  };
}

function assertMockWeb3Allowed() {
  if (process.env.NODE_ENV === "production") {
    throw Object.assign(new Error("Mock wallet and mint routes are disabled in production. Use the signed wallet verification flow."), {
      statusCode: 410,
      errorCode: "MOCK_WEB3_DISABLED"
    });
  }
}
