import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import express from "express";
import cors from "cors";
import { getAssetManifest, getPrototypeCards } from "./cardRepository.js";
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
  startInviteRoom
} from "./inviteRoomStore.js";
import { upsertProfile } from "./profileStore.js";
import { simulateMockMint, syncMockNftOwnership, verifyMockWallet } from "./web3MockStore.js";

const publicDir = join(dirname(fileURLToPath(import.meta.url)), "..", "public");

export function createApp() {
  const app = express();

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
      message: "Mock profile saved. Replace this store with a database in a later phase."
    });
  });

  app.get("/api/cards", async (_req, res, next) => {
    try {
      const cards = await getPrototypeCards();
      res.json(cards);
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
      message: statusCode === 500 ? "Unexpected mock backend error." : error.message
    });
  });

  return app;
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
