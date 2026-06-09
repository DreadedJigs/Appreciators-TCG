import express from "express";
import cors from "cors";
import { getPrototypeCards } from "./cardRepository.js";
import { upsertProfile } from "./profileStore.js";

export function createApp() {
  const app = express();

  app.use(cors());
  app.use(express.json({ limit: "256kb" }));

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

  app.post("/api/wallet/verify", (req, res) => {
    res.json({
      verified: false,
      mock: true,
      walletAddress: req.body?.walletAddress || null,
      phase: "Phase 4",
      message: "Wallet verification is mocked in Phase 1."
    });
  });

  app.post("/api/nft/sync", (req, res) => {
    res.json({
      synced: false,
      mock: true,
      walletAddress: req.body?.walletAddress || null,
      originals: [],
      companions: [],
      cosmetics: [],
      rewards: [],
      phase: "Phase 4",
      message: "NFT ownership sync is mocked in Phase 1."
    });
  });

  app.use((req, res) => {
    res.status(404).json({
      error: "Not Found",
      path: req.path
    });
  });

  app.use((error, _req, res, _next) => {
    console.error(error);
    res.status(500).json({
      error: "Internal Server Error",
      message: "Unexpected mock backend error."
    });
  });

  return app;
}
