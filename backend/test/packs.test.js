import test from "node:test";
import assert from "node:assert/strict";
import { createApp } from "../src/createApp.js";
import {
  resetPackInventoryForTests,
  verifySignedReward
} from "../src/packInventoryStore.js";
import {
  generatePackReward,
  getMysteryOdds
} from "../src/packRewardService.js";

function listen(app) {
  return new Promise((resolve) => {
    const server = app.listen(0, () => resolve(server));
  });
}

async function request(server, path, options = {}) {
  const address = server.address();
  const response = await fetch(`http://127.0.0.1:${address.port}${path}`, {
    headers: { "content-type": "application/json" },
    ...options
  });
  return { response, body: await response.json() };
}

test("mystery profiles expose the required transparent odds", () => {
  assert.deepEqual(getMysteryOdds("standard"), {
    Common: 50,
    Uncommon: 30,
    Rare: 15,
    Epic: 4,
    Legendary: 1
  });
  assert.deepEqual(getMysteryOdds("starter"), {
    Rare: 85,
    Epic: 13,
    Legendary: 2
  });
  assert.deepEqual(getMysteryOdds("event"), {
    Common: 45,
    Uncommon: 30,
    Rare: 18,
    Epic: 6,
    Legendary: 1
  });
  assert.deepEqual(getMysteryOdds("guaranteed_uncommon"), {
    Uncommon: 70,
    Rare: 20,
    Epic: 8,
    Legendary: 2
  });
  assert.deepEqual(getMysteryOdds("guaranteed_legendary"), { Legendary: 100 });
});

test("pack catalog publishes all mystery odds profiles", async () => {
  const server = await listen(createApp());
  try {
    const catalog = await request(server, "/api/packs/catalog");
    assert.equal(catalog.response.status, 200);
    assert.equal(catalog.body.mysteryOdds.standard.Legendary, 1);
    assert.equal(catalog.body.mysteryOdds.starter.Legendary, 2);
    assert.equal(catalog.body.mysteryOdds.event.Epic, 6);
    assert.equal(catalog.body.mysteryOdds.guaranteed_epic.Legendary, 10);
    assert.equal(catalog.body.shardEconomy.starterPackGrantCount, 3);
    assert.equal(catalog.body.shardEconomy.matchWinReward, 69);
    assert.equal(catalog.body.shardEconomy.bossBattleUnlockCost, 2000);
    const prices = Object.fromEntries(catalog.body.packs.map((pack) => [pack.id, pack.shardCost]));
    assert.equal(prices.random_appreciation_pack, 300);
    assert.equal(prices.uncommon_guaranteed_pack, 900);
    assert.equal(prices.rare_guaranteed_pack, 1200);
    assert.equal(prices.mythic_guaranteed_pack, 1500);
    assert.equal(prices.legendary_guaranteed_pack, 1800);
  } finally {
    server.close();
  }
});

test("server reward generation returns five cards and handles duplicates", () => {
  const result = generatePackReward({
    packId: "starter_appreciation_pack",
    attunement: "Art",
    ownedCardIds: [],
    random: () => 0
  });

  assert.equal(result.cards.length, 5);
  assert.equal(result.cards[4].isMysterySlot, true);
  assert.equal(result.attunement, "Neutral");
  assert.equal(result.attunementSucceeded, false);
  assert.equal(result.attunementChancePercent, 0);
  assert.equal(result.attunementShardsSpent, 0);
  assert.equal(result.packShardsAwarded, 100);
  assert.match(result.cards[4].card.rarityLabel, /Rare|Epic|Legendary/);
  assert.equal(result.cards.some((card) => card.isDuplicate), true);
  assert.equal(result.totalDuplicateShards > 0, true);
  assert.equal(result.totalShardsAwarded, result.totalDuplicateShards + result.packShardsAwarded);
});

test("odds endpoint exposes slot distributions, starter guarantee, and Neutral-only opening", async () => {
  const server = await listen(createApp());
  try {
    const odds = await request(server, "/api/packs/odds/starter_appreciation_pack");
    assert.equal(odds.response.status, 200);
    assert.equal(odds.body.starterRareOrBetterGuarantee, true);
    assert.match(odds.body.attunementExplanation, /open Neutral/i);
    assert.equal(odds.body.attunementEnabled, false);
    assert.deepEqual(odds.body.validAttunements, ["Neutral"]);
    assert.equal(odds.body.attunementShardCost, 0);
    assert.equal(odds.body.attunementChancePercent, 0);
    assert.equal(odds.body.attunementAffectsSlot, 0);
    assert.equal(odds.body.slots.some((slot) => slot.isLaneAttuned), false);
    assert.equal(odds.body.slots[4].isAttunementEligible, false);
    assert.deepEqual(odds.body.packShardOdds, [
      { shards: 100, percent: 40 },
      { shards: 125, percent: 35 },
      { shards: 150, percent: 20 },
      { shards: 300, percent: 5 }
    ]);
    assert.deepEqual(odds.body.slots[3].rarityOdds, [
      { rarityLabel: "Common", percent: 60 },
      { rarityLabel: "Uncommon", percent: 32 },
      { rarityLabel: "Rare", percent: 8 }
    ]);
    assert.deepEqual(odds.body.slots[4].rarityOdds, [
      { rarityLabel: "Rare", percent: 85 },
      { rarityLabel: "Epic", percent: 13 },
      { rarityLabel: "Legendary", percent: 2 }
    ]);
  } finally {
    server.close();
  }
});

test("open-pack route validates ownership and returns a signed authoritative reward", async () => {
  process.env.PACK_REWARD_SIGNING_SECRET = "test-pack-signing-secret";
  resetPackInventoryForTests();
  const server = await listen(createApp());

  try {
    const inventory = await request(server, "/api/packs/inventory?playerId=test_player");
    assert.equal(inventory.response.status, 200);
    assert.equal(inventory.body.inventory.appreciationShards, 0);
    assert.equal(inventory.body.inventory.packs.find((pack) => pack.packId === "starter_appreciation_pack").count, 3);

    const opened = await request(server, "/api/packs/open", {
      method: "POST",
      body: JSON.stringify({
        requestId: "open-test-1",
        playerId: "test_player",
        packId: "starter_appreciation_pack",
        attunement: "Neutral"
      })
    });

    assert.equal(opened.response.status, 200);
    assert.equal(opened.body.success, true);
    assert.equal(opened.body.requestId, "open-test-1");
    assert.equal(opened.body.remainingPackCount, 3, "Alpha test mode keeps three starter packs ready after every ritual.");
    assert.equal(opened.body.totalShardBalance, opened.body.inventory.appreciationShards);
    assert.equal(opened.body.algorithm, "HMAC-SHA256");
    assert.equal(opened.body.reward.cards.length, 5);
    assert.equal(opened.body.reward.attunement, "Neutral");
    assert.equal(opened.body.reward.packShardsAwarded >= 10, true);
    assert.equal(opened.body.totalShardBalance >= opened.body.reward.packShardsAwarded, true);
    assert.equal(opened.body.reward.attunementShardsSpent, 0);
    assert.equal(verifySignedReward(opened.body.payloadBase64, opened.body.signature), true);
    const signedPayload = JSON.parse(Buffer.from(opened.body.payloadBase64, "base64url").toString("utf8"));
    assert.equal(signedPayload.playerId, "test_player");
    assert.equal(signedPayload.requestId, "open-test-1");
    assert.equal(opened.body.inventory.packs.find((pack) => pack.packId === "starter_appreciation_pack").count, 3);

    const replayed = await request(server, "/api/packs/open", {
      method: "POST",
      body: JSON.stringify({
        requestId: "open-test-1",
        playerId: "test_player",
        packId: "starter_appreciation_pack",
        attunement: "Neutral"
      })
    });
    assert.equal(replayed.response.status, 200);
    assert.equal(replayed.body.idempotentReplay, true);
    assert.equal(replayed.body.reward.rewardId, opened.body.reward.rewardId);

    const conflictingReplay = await request(server, "/api/packs/open", {
      method: "POST",
      body: JSON.stringify({
        requestId: "open-test-1",
        playerId: "test_player",
        packId: "random_appreciation_pack",
        attunement: "Neutral"
      })
    });
    assert.equal(conflictingReplay.response.status, 409);
    assert.equal(conflictingReplay.body.errorCode, "REQUEST_ID_CONFLICT");

    for (let index = 2; index <= 3; index += 1) {
      const remaining = await request(server, "/api/packs/open", {
        method: "POST",
        body: JSON.stringify({
          requestId: `open-test-${index}`,
          playerId: "test_player",
          packId: "starter_appreciation_pack",
          attunement: "Neutral"
        })
      });
      assert.equal(remaining.response.status, 200);
    }

    const continuouslyStocked = await request(server, "/api/packs/open", {
      method: "POST",
      body: JSON.stringify({
        requestId: "open-test-4",
        playerId: "test_player",
        packId: "starter_appreciation_pack",
        attunement: "Neutral"
      })
    });
    assert.equal(continuouslyStocked.response.status, 200);
    assert.equal(continuouslyStocked.body.remainingPackCount, 3);
  } finally {
    server.close();
    resetPackInventoryForTests();
  }
});

test("pack opening stays available when a Render signing secret has not been provisioned", async () => {
  const previousSecret = process.env.PACK_REWARD_SIGNING_SECRET;
  delete process.env.PACK_REWARD_SIGNING_SECRET;
  resetPackInventoryForTests();
  const server = await listen(createApp());

  try {
    const opened = await request(server, "/api/packs/open", {
      method: "POST",
      body: JSON.stringify({
        requestId: "open-runtime-signer-1",
        playerId: "runtime_signer_player",
        packId: "starter_appreciation_pack",
        attunement: "Neutral"
      })
    });

    assert.equal(opened.response.status, 200);
    assert.equal(opened.body.success, true);
    assert.equal(opened.body.reward.cards.length, 5);
    assert.equal(typeof opened.body.signature, "string");
    assert.equal(opened.body.signature.length, 64);
    assert.equal(verifySignedReward(opened.body.payloadBase64, opened.body.signature), true);
  } finally {
    server.close();
    if (previousSecret === undefined) delete process.env.PACK_REWARD_SIGNING_SECRET;
    else process.env.PACK_REWARD_SIGNING_SECRET = previousSecret;
  }
});

test("unique match wins award 69 shards and fund idempotent pack purchases", async () => {
  resetPackInventoryForTests();
  const server = await listen(createApp());
  const playerId = "economy_player";

  try {
    for (let index = 0; index < 5; index += 1) {
      const reward = await request(server, "/api/economy/match-win", {
        method: "POST",
        body: JSON.stringify({
          playerId,
          matchId: `match-${index}`,
          result: "Victory"
        })
      });
      assert.equal(reward.response.status, 200);
      assert.equal(reward.body.shardsAwarded, 69);
    }

    const before = await request(server, `/api/packs/inventory?playerId=${playerId}`);
    assert.equal(before.body.inventory.appreciationShards, 345);

    const replay = await request(server, "/api/economy/match-win", {
      method: "POST",
      body: JSON.stringify({
        playerId,
        matchId: "match-0",
        result: "Victory"
      })
    });
    assert.equal(replay.body.idempotentReplay, true);
    assert.equal(replay.body.totalShardBalance, 345);

    const purchase = await request(server, "/api/packs/purchase", {
      method: "POST",
      body: JSON.stringify({ requestId: "purchase-1", playerId, packId: "random_appreciation_pack" })
    });
    assert.equal(purchase.response.status, 200);
    assert.equal(purchase.body.shardCost, 300);
    assert.equal(purchase.body.remainingShards, 45);
    assert.equal(purchase.body.quantityOwned, 1);

    const purchaseReplay = await request(server, "/api/packs/purchase", {
      method: "POST",
      body: JSON.stringify({ requestId: "purchase-1", playerId, packId: "random_appreciation_pack" })
    });
    assert.equal(purchaseReplay.body.idempotentReplay, true);
    assert.equal(purchaseReplay.body.inventory.appreciationShards, 45);

    const tooExpensive = await request(server, "/api/packs/purchase", {
      method: "POST",
      body: JSON.stringify({ requestId: "purchase-2", playerId, packId: "uncommon_guaranteed_pack" })
    });
    assert.equal(tooExpensive.response.status, 409);
    assert.equal(tooExpensive.body.errorCode, "INSUFFICIENT_SHARDS");
  } finally {
    server.close();
    resetPackInventoryForTests();
  }
});

test("account login restores inventory and ranked losses remove five Appreciation Shards", async () => {
  resetPackInventoryForTests();
  const server = await listen(createApp());
  const playerId = "account_1234567890abcdef";

  try {
    const login = await request(server, "/api/session/login", {
      method: "POST",
      body: JSON.stringify({ username: "Cross Network Player", playerId })
    });
    assert.equal(login.response.status, 200);
    assert.equal(login.body.profile.id, playerId);
    assert.equal(login.body.inventory.packs[0].count, 3);

    const victory = await request(server, "/api/economy/match-result", {
      method: "POST",
      body: JSON.stringify({ playerId, matchId: "ranked-win", result: "Victory", mode: "Ranked" })
    });
    assert.equal(victory.response.status, 200);
    assert.equal(victory.body.shardsChanged, 69);
    assert.equal(victory.body.totalShardBalance, 69);

    const loss = await request(server, "/api/economy/match-result", {
      method: "POST",
      body: JSON.stringify({ playerId, matchId: "ranked-loss", result: "Defeat", mode: "Ranked" })
    });
    assert.equal(loss.response.status, 200);
    assert.equal(loss.body.shardsChanged, -5);
    assert.equal(loss.body.rankedLossPenalty, 5);
    assert.equal(loss.body.totalShardBalance, 64);

    const replay = await request(server, "/api/economy/match-result", {
      method: "POST",
      body: JSON.stringify({ playerId, matchId: "ranked-loss", result: "Defeat", mode: "Ranked" })
    });
    assert.equal(replay.body.idempotentReplay, true);
    assert.equal(replay.body.totalShardBalance, 64);

    const restored = await request(server, "/api/session/login", {
      method: "POST",
      body: JSON.stringify({ username: "Cross Network Player", playerId })
    });
    assert.equal(restored.body.inventory.appreciationShards, 64);
  } finally {
    server.close();
    resetPackInventoryForTests();
  }
});

test("players can pool shards or fund the remainder to unlock the 2000-shard Boss Vault", async () => {
  resetPackInventoryForTests();
  const server = await listen(createApp());

  try {
    for (let index = 0; index < 2; index += 1) {
      const reward = await request(server, "/api/economy/match-win", {
        method: "POST",
        body: JSON.stringify({ playerId: "boss_helper", matchId: `helper-${index}`, result: "Victory" })
      });
      assert.equal(reward.response.status, 200);
    }
    const firstContribution = await request(server, "/api/economy/boss-contribute", {
      method: "POST",
      body: JSON.stringify({ requestId: "boss-help-1", playerId: "boss_helper", poolId: "alpha_boss", amount: 100 })
    });
    assert.equal(firstContribution.body.pool.totalShards, 100);
    assert.equal(firstContribution.body.pool.unlocked, false);

    for (let index = 0; index < 28; index += 1) {
      const reward = await request(server, "/api/economy/match-win", {
        method: "POST",
        body: JSON.stringify({ playerId: "boss_funder", matchId: `funder-${index}`, result: "Victory" })
      });
      assert.equal(reward.response.status, 200);
    }
    const finalContribution = await request(server, "/api/economy/boss-contribute", {
      method: "POST",
      body: JSON.stringify({ requestId: "boss-fund-1", playerId: "boss_funder", poolId: "alpha_boss", amount: 1900 })
    });
    assert.equal(finalContribution.response.status, 200);
    assert.equal(finalContribution.body.pool.totalShards, 2000);
    assert.equal(finalContribution.body.pool.remainingShards, 0);
    assert.equal(finalContribution.body.pool.unlocked, true);
    assert.equal(finalContribution.body.pool.contributors, 2);

    const status = await request(server, "/api/economy/boss-pool?poolId=alpha_boss");
    assert.equal(status.body.pool.unlocked, true);
  } finally {
    server.close();
    resetPackInventoryForTests();
  }
});

test("alpha grant and simulation routes are backend-controlled", async () => {
  resetPackInventoryForTests();
  const server = await listen(createApp());

  try {
    const granted = await request(server, "/api/packs/grant-test-pack", {
      method: "POST",
      body: JSON.stringify({ playerId: "grant_player", packId: "random_appreciation_pack", count: 1 })
    });
    assert.equal(granted.response.status, 201);
    assert.equal(granted.body.inventory.packs.find((pack) => pack.packId === "random_appreciation_pack").count, 1);

    const simulated = await request(server, "/api/packs/simulate", {
      method: "POST",
      body: JSON.stringify({ packId: "random_appreciation_pack", attunement: "Neutral", count: 100 })
    });
    assert.equal(simulated.response.status, 200);
    assert.equal(simulated.body.cardsOpened, 500);
    assert.equal(Object.values(simulated.body.distribution).reduce((sum, count) => sum + count, 0), 500);
    assert.equal(Object.values(simulated.body.laneDistribution).reduce((sum, count) => sum + count, 0), 500);
    assert.equal(simulated.body.duplicateCount > 0, true);
    assert.equal(simulated.body.totalShardsAwarded > 0, true);

    const reset = await request(server, "/api/packs/reset-test-inventory", {
      method: "POST",
      body: JSON.stringify({ playerId: "grant_player" })
    });
    assert.equal(reset.response.status, 200);
    assert.equal(reset.body.inventory.ownedCardCount, 0);
    assert.equal(reset.body.inventory.appreciationShards, 0);
  } finally {
    server.close();
    resetPackInventoryForTests();
  }
});

test("production test-pack endpoints are disabled without admin test access", async () => {
  const previousNodeEnv = process.env.NODE_ENV;
  const previousEnabled = process.env.PACK_TEST_GRANTS_ENABLED;
  const previousKey = process.env.PACK_TEST_ADMIN_KEY;
  process.env.NODE_ENV = "production";
  process.env.PACK_TEST_GRANTS_ENABLED = "false";
  process.env.PACK_TEST_ADMIN_KEY = "test-only-key";
  const server = await listen(createApp());

  try {
    const grant = await request(server, "/api/packs/grant-test-pack", {
      method: "POST",
      body: JSON.stringify({ playerId: "blocked_player", packId: "starter_appreciation_pack", count: 1 })
    });
    assert.equal(grant.response.status, 403);
    assert.equal(grant.body.errorCode, "PACK_TEST_TOOLS_FORBIDDEN");

    const simulate = await request(server, "/api/packs/simulate", {
      method: "POST",
      body: JSON.stringify({ packId: "starter_appreciation_pack", attunement: "Art", count: 100 })
    });
    assert.equal(simulate.response.status, 403);

    process.env.PACK_TEST_GRANTS_ENABLED = "true";
    const authorizedGrant = await request(server, "/api/packs/grant-test-pack", {
      method: "POST",
      headers: {
        "content-type": "application/json",
        "x-pack-test-key": "test-only-key"
      },
      body: JSON.stringify({ playerId: "authorized_player", packId: "starter_appreciation_pack", count: 1 })
    });
    assert.equal(authorizedGrant.response.status, 201);
  } finally {
    server.close();
    restoreEnvironment("NODE_ENV", previousNodeEnv);
    restoreEnvironment("PACK_TEST_GRANTS_ENABLED", previousEnabled);
    restoreEnvironment("PACK_TEST_ADMIN_KEY", previousKey);
  }
});

function restoreEnvironment(name, value) {
  if (value === undefined) {
    delete process.env[name];
  } else {
    process.env[name] = value;
  }
}
