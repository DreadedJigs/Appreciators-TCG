import test from "node:test";
import assert from "node:assert/strict";
import { createApp } from "../src/createApp.js";

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

  const body = await response.json();
  return { response, body };
}

test("health route reports prototype service status", async () => {
  const server = await listen(createApp());
  try {
    const { response, body } = await request(server, "/health");
    assert.equal(response.status, 200);
    assert.equal(body.status, "ok");
    assert.equal(body.phase, "1-prototype");
  } finally {
    server.close();
  }
});

test("cards route serves the prototype card list", async () => {
  const server = await listen(createApp());
  try {
    const { response, body } = await request(server, "/api/cards");
    assert.equal(response.status, 200);
    assert.equal(body.cards.length, 29);
  } finally {
    server.close();
  }
});

test("asset manifest exposes expected card art slots", async () => {
  const server = await listen(createApp());
  try {
    const { response, body } = await request(server, "/api/assets/manifest");
    assert.equal(response.status, 200);
    assert.equal(body.version, "phase-1");
    assert.equal(body.cards.length, 29);
    assert.equal(body.cards[0].expectedFile.endsWith(".png"), true);
  } finally {
    server.close();
  }
});

test("mock profile and matchmaking routes return playable local data", async () => {
  const server = await listen(createApp());
  try {
    const profile = await request(server, "/api/profile", {
      method: "POST",
      body: JSON.stringify({ username: "Tester" })
    });
    assert.equal(profile.response.status, 201);
    assert.equal(profile.body.profile.username, "Tester");
    assert.equal(profile.body.profile.mockAccount, true);

    const match = await request(server, "/api/matchmaking/casual", {
      method: "POST",
      body: JSON.stringify({ username: "Tester" })
    });
    assert.equal(match.response.status, 200);
    assert.equal(match.body.mode, "Casual");
    assert.equal(match.body.opponent.id, "ai_phase_1");
  } finally {
    server.close();
  }
});

test("web3 routes stay explicitly mocked", async () => {
  const server = await listen(createApp());
  try {
    const wallet = await request(server, "/api/wallet/verify", {
      method: "POST",
      body: JSON.stringify({ walletAddress: "0xMock" })
    });
    assert.equal(wallet.body.mock, true);
    assert.equal(wallet.body.verified, false);

    const nft = await request(server, "/api/nft/sync", {
      method: "POST",
      body: JSON.stringify({ walletAddress: "0xMock" })
    });
    assert.equal(nft.body.mock, true);
    assert.deepEqual(nft.body.originals, []);
  } finally {
    server.close();
  }
});
