import test from "node:test";
import assert from "node:assert/strict";
import { createApp } from "../src/createApp.js";
import { clearInviteRoomsForTests } from "../src/inviteRoomStore.js";
import { resetMockMintForTests } from "../src/web3MockStore.js";

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
    assert.equal(body.cards.length, 23);
  } finally {
    server.close();
  }
});

test("card meta routes expose the validated 6,666 identity system", async () => {
  const server = await listen(createApp());
  try {
    const summary = await request(server, "/api/card-meta/summary");
    assert.equal(summary.response.status, 200);
    assert.equal(summary.body.cards, 6666);
    assert.equal(summary.body.abilities, 432);
    assert.equal(summary.body.seasons, 22);
    assert.equal(summary.body.crowns, 22);
    assert.equal(summary.body.rarityCounts.Common, 3102);
    assert.equal(summary.body.rarityCounts.Crown, 22);

    const season = await request(server, "/api/card-meta/cards?season=1&limit=303");
    assert.equal(season.response.status, 200);
    assert.equal(season.body.total, 303);
    assert.equal(season.body.items.length, 303);
    assert.equal(season.body.items.filter((card) => card.physicalRarity === "Crown").length, 1);

    const card = await request(server, "/api/card-meta/cards/1");
    assert.equal(card.body.card.cardName, "Appreciators Original #1");
    assert.equal(card.body.card.metadataStatus, "Metadata pending");

    const abilities = await request(server, "/api/card-meta/abilities?type=Board&domain=Art&pillar=Learn&limit=303");
    assert.equal(abilities.response.status, 200);
    assert.ok(abilities.body.items.length > 0);
    assert.ok(abilities.body.items.every((ability) => ability.completeRulesText));
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
    assert.equal(body.cards.length, 23);
    assert.equal(body.cards[0].expectedFile.endsWith(".png"), true);
  } finally {
    server.close();
  }
});

test("release plan publishes the complete 6,666-card seasonal structure", async () => {
  const server = await listen(createApp());
  try {
    const { response, body } = await request(server, "/api/releases/plan");
    assert.equal(response.status, 200);
    assert.equal(body.universeSize, 6666);
    assert.equal(body.seasonCount, 22);
    assert.equal(body.cardsPerSeason, 303);
    assert.equal(Object.values(body.rarityPerSeason).reduce((sum, count) => sum + count, 0), 303);
    assert.equal(Object.values(body.setOneDistribution).reduce((sum, count) => sum + count, 0), 303);
    assert.equal(body.gameplay.deckSize, 30);
    assert.equal(body.gameplay.victoryGrowth, 200);
    assert.equal(body.crownCompetitiveParity.required, true);
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

test("invite matchmaking creates, joins, reports, and starts 1v1 rooms", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const created = await request(server, "/api/matchmaking/invite", {
      method: "POST",
      body: JSON.stringify({
        username: "Host",
        deckIds: ["regular_body", "no_head_body"]
      })
    });

    assert.equal(created.response.status, 201);
    assert.equal(created.body.room.mode, "Invite 1v1");
    assert.equal(created.body.room.status, "waiting");
    assert.equal(created.body.room.host.username, "Host");
    assert.equal(created.body.room.host.deckSize, 2);
    assert.match(created.body.room.inviteCode, /^[A-Z2-9]{6}$/);

    const inviteCode = created.body.room.inviteCode;
    const joined = await request(server, `/api/matchmaking/invite/${inviteCode}/join`, {
      method: "POST",
      body: JSON.stringify({
        username: "Guest",
        deckIds: ["purple_skin"]
      })
    });

    assert.equal(joined.response.status, 200);
    assert.equal(joined.body.room.status, "ready");
    assert.equal(joined.body.room.guest.username, "Guest");

    const status = await request(server, `/api/matchmaking/invite/${inviteCode}`);
    assert.equal(status.response.status, 200);
    assert.equal(status.body.room.players.length, 2);

    const started = await request(server, `/api/matchmaking/invite/${inviteCode}/start`, {
      method: "POST",
      body: JSON.stringify({
        username: "Host",
        playerId: created.body.player.id
      })
    });

    assert.equal(started.response.status, 200);
    assert.equal(started.body.room.status, "started");
    assert.equal(started.body.assignment.mode, "Invite 1v1");
    assert.equal(started.body.assignment.players.length, 2);
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite link routes support WebGL-friendly create, join, and start", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const created = await request(server, "/api/matchmaking/invite/new?username=LinkHost&deckIds=regular_body,no_head_body");

    assert.equal(created.response.status, 201);
    assert.equal(created.body.room.status, "waiting");
    assert.equal(created.body.room.host.username, "LinkHost");
    assert.equal(created.body.room.host.deckSize, 2);

    const inviteCode = created.body.room.inviteCode;
    const joined = await request(server, `/api/matchmaking/invite/${inviteCode}/join-link?username=LinkGuest&deckIds=purple_skin`);

    assert.equal(joined.response.status, 200);
    assert.equal(joined.body.room.status, "ready");
    assert.equal(joined.body.room.guest.username, "LinkGuest");

    const guestReconnect = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/reconnect-link?username=LinkGuest&role=guest`
    );
    assert.equal(guestReconnect.response.status, 200);
    assert.equal(guestReconnect.body.player.id, joined.body.player.id);
    assert.equal(guestReconnect.body.player.connected, true);

    const hostReconnect = await request(server, `/api/matchmaking/invite/${inviteCode}/reconnect`, {
      method: "POST",
      body: JSON.stringify({
        playerId: created.body.player.id
      })
    });
    assert.equal(hostReconnect.response.status, 200);
    assert.equal(hostReconnect.body.player.id, created.body.player.id);

    const started = await request(server, `/api/matchmaking/invite/${inviteCode}/start-link?username=LinkGuest&playerId=${joined.body.player.id}`);

    assert.equal(started.response.status, 200);
    assert.equal(started.body.room.status, "started");
    assert.equal(started.body.assignment.inviteCode, inviteCode);
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite lobby exposes available players and direct challenges", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const hostPresence = await request(
      server,
      "/api/matchmaking/invite-lobby/announce?username=Host&playerId=host_local&deckIds=regular_body"
    );
    assert.equal(hostPresence.response.status, 200);
    assert.equal(hostPresence.body.playerId, "host_local");

    const guestPresence = await request(
      server,
      "/api/matchmaking/invite-lobby/announce?username=Guest&playerId=guest_local&deckIds=no_head_body"
    );
    assert.equal(guestPresence.response.status, 200);
    assert.equal(guestPresence.body.players.some((player) => player.id === "host_local"), true);

    const hostLobby = await request(server, "/api/matchmaking/invite-lobby?username=Host&playerId=host_local");
    assert.equal(hostLobby.response.status, 200);
    assert.equal(hostLobby.body.players.some((player) => player.id === "guest_local"), true);

    const challenged = await request(
      server,
      "/api/matchmaking/invite-lobby/challenge?username=Host&playerId=host_local&targetPlayerId=guest_local&deckIds=regular_body"
    );
    assert.equal(challenged.response.status, 201);
    assert.equal(challenged.body.room.status, "waiting");
    assert.equal(challenged.body.room.challengeTargetId, "guest_local");
    assert.equal(challenged.body.room.host.id, "host_local");

    const guestLobby = await request(server, "/api/matchmaking/invite-lobby?username=Guest&playerId=guest_local");
    assert.equal(guestLobby.response.status, 200);
    assert.equal(guestLobby.body.challenges.length, 1);
    assert.equal(guestLobby.body.challenges[0].inviteCode, challenged.body.room.inviteCode);

    const joined = await request(
      server,
      `/api/matchmaking/invite/${challenged.body.room.inviteCode}/join-link?username=Guest&playerId=guest_local&deckIds=no_head_body`
    );
    assert.equal(joined.response.status, 200);
    assert.equal(joined.body.room.status, "ready");
    assert.equal(joined.body.room.guest.id, "guest_local");
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite match action log records and returns synced play actions", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const created = await request(server, "/api/matchmaking/invite/new?username=ActionHost&deckIds=regular_body");
    const inviteCode = created.body.room.inviteCode;
    const joined = await request(server, `/api/matchmaking/invite/${inviteCode}/join-link?username=ActionGuest&deckIds=no_head_body`);
    await request(server, `/api/matchmaking/invite/${inviteCode}/start-link?username=ActionHost&playerId=${created.body.player.id}`);

    const logged = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${joined.body.player.id}&actionId=test-1&type=play-card&cardId=regular_body&lane=Art&turn=1`
    );

    assert.equal(logged.response.status, 200);
    assert.equal(logged.body.action.sequence, 1);
    assert.equal(logged.body.action.cardId, "regular_body");
    assert.equal(logged.body.action.lane, "Art");
    const artLane = logged.body.room.matchState.lanes.find((lane) => lane.lane === "Art");
    assert.equal(artLane.guest.length, 1);
    assert.equal(artLane.guestPower, 4);

    const duplicate = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${joined.body.player.id}&actionId=test-1&type=play-card&cardId=regular_body&lane=Blockchain&turn=1`
    );

    assert.equal(duplicate.body.action.sequence, 1);
    assert.equal(duplicate.body.action.lane, "Art");

    const actions = await request(server, `/api/matchmaking/invite/${inviteCode}/actions?after=0`);
    assert.equal(actions.response.status, 200);
    assert.equal(actions.body.actions.length, 1);
    assert.equal(actions.body.actions[0].playerId, joined.body.player.id);
    assert.equal(actions.body.room.matchState.lanes.find((lane) => lane.lane === "Art").guestPower, 4);

    const blocked = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=not-a-player&actionId=test-2&type=play-card&cardId=regular_body&lane=Art&turn=1`
    );
    assert.equal(blocked.response.status, 403);
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite matches accept the public discard-card event", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const created = await request(server, "/api/matchmaking/invite/new?username=ActionHost&deckIds=regular_body");
    const inviteCode = created.body.room.inviteCode;
    await request(server, `/api/matchmaking/invite/${inviteCode}/join-link?username=ActionGuest&deckIds=no_head_body`);
    await request(server, `/api/matchmaking/invite/${inviteCode}/start-link?username=ActionHost&playerId=${created.body.player.id}`);

    const acted = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${created.body.player.id}&actionId=discard-1&type=discard-card&cardId=regular_body&turn=1`
    );

    assert.equal(acted.response.status, 200);
    assert.equal(acted.body.action.type, "discard-card");
    assert.equal(acted.body.action.cardId, "regular_body");
    assert.match(acted.body.room.matchState.message, /revealed and discarded/);
    assert.equal(acted.body.room.matchState.maxTurn, 11);
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite Community resources spend, reset, and regenerate authoritatively", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const created = await request(server, "/api/matchmaking/invite/new?username=ShardHost&deckIds=regular_body");
    const inviteCode = created.body.room.inviteCode;
    const joined = await request(server, `/api/matchmaking/invite/${inviteCode}/join-link?username=ShardGuest&deckIds=no_head_body`);
    await request(server, `/api/matchmaking/invite/${inviteCode}/start-link?username=ShardHost&playerId=${created.body.player.id}`);

    const defense = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${created.body.player.id}&actionId=host-defense&type=spend-community-defense&lane=Community&turn=1`
    );
    assert.equal(defense.response.status, 200);
    assert.deepEqual(defense.body.room.matchState.resources.host, {
      art: 0,
      blockchain: 1,
      shield: 1,
      rally: 0
    });

    const rally = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${created.body.player.id}&actionId=host-rally&type=spend-community-rally&lane=Community&turn=1`
    );
    assert.equal(rally.response.status, 200);
    assert.deepEqual(rally.body.room.matchState.resources.host, {
      art: 0,
      blockchain: 0,
      shield: 1,
      rally: 1
    });

    const overspend = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${created.body.player.id}&actionId=host-defense-2&type=spend-community-defense&lane=Community&turn=1`
    );
    assert.equal(overspend.response.status, 409);

    await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${created.body.player.id}&actionId=host-end-shards&type=end-turn&turn=1`
    );
    const advanced = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${joined.body.player.id}&actionId=guest-end-shards&type=end-turn&turn=1`
    );
    assert.equal(advanced.body.room.matchState.currentTurn, 2);
    assert.deepEqual(advanced.body.room.matchState.resources.host, {
      art: 1,
      blockchain: 1,
      shield: 0,
      rally: 0
    });
    assert.deepEqual(advanced.body.room.matchState.resources.guest, {
      art: 2,
      blockchain: 2,
      shield: 0,
      rally: 0
    });
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite shard salvage accepts only configured cards and matching lanes", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const created = await request(server, "/api/matchmaking/invite/new?username=SalvageHost&deckIds=tropical_background");
    const inviteCode = created.body.room.inviteCode;
    const joined = await request(server, `/api/matchmaking/invite/${inviteCode}/join-link?username=SalvageGuest&deckIds=regular_body`);
    await request(server, `/api/matchmaking/invite/${inviteCode}/start-link?username=SalvageHost&playerId=${created.body.player.id}`);

    const salvaged = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${created.body.player.id}&actionId=host-salvage&type=discard-card-for-shard&cardId=tropical_background&lane=Art&turn=1`
    );
    assert.equal(salvaged.response.status, 200);
    assert.equal(salvaged.body.room.matchState.resources.host.art, 2);

    const wrongLane = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${joined.body.player.id}&actionId=guest-salvage&type=discard-card-for-shard&cardId=tropical_background&lane=Blockchain&turn=1`
    );
    assert.equal(wrongLane.response.status, 400);

    const ineligible = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${joined.body.player.id}&actionId=guest-salvage-2&type=discard-card-for-shard&cardId=regular_body&lane=Art&turn=1`
    );
    assert.equal(ineligible.response.status, 400);
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite match state advances turns after both players end", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const created = await request(server, "/api/matchmaking/invite/new?username=TurnHost&deckIds=regular_body");
    const inviteCode = created.body.room.inviteCode;
    const joined = await request(server, `/api/matchmaking/invite/${inviteCode}/join-link?username=TurnGuest&deckIds=no_head_body`);
    await request(server, `/api/matchmaking/invite/${inviteCode}/start-link?username=TurnHost&playerId=${created.body.player.id}`);

    const hostEnded = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${created.body.player.id}&actionId=host-end-1&type=end-turn&turn=1`
    );

    assert.equal(hostEnded.body.room.matchState.currentTurn, 1);
    assert.equal(hostEnded.body.room.matchState.endedTurn.host, true);
    assert.equal(hostEnded.body.room.matchState.endedTurn.guest, false);

    const doubleEnd = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${created.body.player.id}&actionId=host-end-1b&type=end-turn&turn=1`
    );
    assert.equal(doubleEnd.response.status, 409);

    const guestEnded = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${joined.body.player.id}&actionId=guest-end-1&type=end-turn&turn=1`
    );

    assert.equal(guestEnded.body.room.matchState.currentTurn, 2);
    assert.equal(guestEnded.body.room.matchState.energy.host, 2);
    assert.equal(guestEnded.body.room.matchState.energy.guest, 2);
    assert.equal(guestEnded.body.room.matchState.endedTurn.host, false);
    assert.equal(guestEnded.body.room.matchState.endedTurn.guest, false);

    const stalePlay = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/action?playerId=${joined.body.player.id}&actionId=guest-stale&type=play-card&cardId=no_head_body&lane=Art&turn=1`
    );
    assert.equal(stalePlay.response.status, 409);

    const state = await request(server, `/api/matchmaking/invite/${inviteCode}/state`);
    assert.equal(state.response.status, 200);
    assert.equal(state.body.matchState.currentTurn, 2);
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite match requires both players to agree before termination", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const created = await request(server, "/api/matchmaking/invite/new?username=ExitHost&deckIds=regular_body");
    const inviteCode = created.body.room.inviteCode;
    const joined = await request(server, `/api/matchmaking/invite/${inviteCode}/join-link?username=ExitGuest&deckIds=no_head_body`);
    await request(server, `/api/matchmaking/invite/${inviteCode}/start-link?username=ExitHost&playerId=${created.body.player.id}`);

    const requested = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/termination-link?playerId=${created.body.player.id}&decision=request`
    );
    assert.equal(requested.response.status, 200);
    assert.equal(requested.body.room.status, "started");
    assert.equal(requested.body.matchState.status, "active");
    assert.equal(requested.body.matchState.termination.status, "pending");
    assert.equal(requested.body.matchState.termination.hostAccepted, true);
    assert.equal(requested.body.matchState.termination.guestAccepted, false);

    const declined = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/termination-link?playerId=${joined.body.player.id}&decision=decline`
    );
    assert.equal(declined.body.room.status, "started");
    assert.equal(declined.body.matchState.termination.status, "declined");

    await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/termination-link?playerId=${created.body.player.id}&decision=request`
    );
    const accepted = await request(
      server,
      `/api/matchmaking/invite/${inviteCode}/termination-link?playerId=${joined.body.player.id}&decision=accept`
    );
    assert.equal(accepted.response.status, 200);
    assert.equal(accepted.body.room.status, "terminated");
    assert.equal(accepted.body.matchState.status, "terminated");
    assert.equal(accepted.body.matchState.termination.status, "agreed");
    assert.equal(accepted.body.matchState.termination.hostAccepted, true);
    assert.equal(accepted.body.matchState.termination.guestAccepted, true);
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("invite rooms reject missing or unauthorized starts", async () => {
  clearInviteRoomsForTests();
  const server = await listen(createApp());
  try {
    const missing = await request(server, "/api/matchmaking/invite/NOPE42");
    assert.equal(missing.response.status, 404);

    const created = await request(server, "/api/matchmaking/invite", {
      method: "POST",
      body: JSON.stringify({ username: "Host" })
    });

    const blocked = await request(server, `/api/matchmaking/invite/${created.body.room.inviteCode}/start`, {
      method: "POST",
      body: JSON.stringify({ username: "Guest" })
    });

    assert.equal(blocked.response.status, 403);
  } finally {
    server.close();
    clearInviteRoomsForTests();
  }
});

test("web3 routes stay explicitly mocked", async () => {
  resetMockMintForTests();
  const server = await listen(createApp());
  try {
    const wallet = await request(server, "/api/wallet/verify", {
      method: "POST",
      body: JSON.stringify({ walletAddress: "0xMock" })
    });
    assert.equal(wallet.body.mock, true);
    assert.equal(wallet.body.verified, true);
    assert.equal(wallet.body.realSignatureVerified, false);
    assert.equal(wallet.body.cosmetics.length > 0, true);

    const walletLink = await request(server, "/api/wallet/verify-link?walletAddress=0xMock&username=Tester");
    assert.equal(walletLink.body.mock, true);
    assert.equal(walletLink.body.username, "Tester");

    const nft = await request(server, "/api/nft/sync", {
      method: "POST",
      body: JSON.stringify({ walletAddress: "0xMock" })
    });
    assert.equal(nft.body.mock, true);
    assert.equal(nft.body.synced, true);
    assert.equal(nft.body.realOwnershipSynced, false);
    assert.equal(nft.body.originals[0].cosmeticOnly, true);

    const nftLink = await request(server, "/api/nft/sync-link?walletAddress=0xMock");
    assert.equal(nftLink.body.cosmetics.length > 0, true);

    const mint = await request(server, "/api/mint/simulate", {
      method: "POST",
      body: JSON.stringify({ walletAddress: "0xMock", quantity: 2 })
    });
    assert.equal(mint.body.mock, true);
    assert.equal(mint.body.minted, true);
    assert.equal(mint.body.realTransactionSubmitted, false);
    assert.equal(mint.body.mintedQuantity, 2);
    assert.equal(mint.body.tokens.length, 2);
    assert.equal(mint.body.supplyCap, 6666);
    assert.equal(mint.body.remainingSupply, 663);
    assert.equal(mint.body.selectionMode, "random-non-sequential");
    assert.equal(typeof mint.body.tokens[0].tokenNumber, "number");
    assert.equal(typeof mint.body.tokens[0].rarity, "string");
    assert.equal(typeof mint.body.tokens[0].warPower, "number");
    assert.equal(new Set(mint.body.tokens.map((token) => token.tokenId)).size, 2);
    assert.notEqual(Math.abs(mint.body.tokens[0].tokenNumber - mint.body.tokens[1].tokenNumber), 1);

    const mintLink = await request(server, "/api/mint/simulate-link?walletAddress=0xMock&quantity=99");
    assert.equal(mintLink.body.requestedQuantity, 5);
    assert.equal(mintLink.body.mintedQuantity, 5);
    assert.equal(mintLink.body.totalMintedByWallet, 7);

    const war = await request(server, `/api/mint/war-link?walletAddress=0xMock&tokenId=${mint.body.tokens[0].tokenId}`);
    assert.equal(war.response.status, 200);
    assert.equal(war.body.mock, true);
    assert.equal(war.body.mode, "I Declare War vs AI");
    assert.ok(["win", "loss", "draw"].includes(war.body.result));
    assert.equal(typeof war.body.playerScore, "number");
    assert.equal(typeof war.body.aiScore, "number");
    assert.equal(war.body.leaderboard.length > 0, true);

    const leaderboard = await request(server, "/api/mint/leaderboard-link");
    assert.equal(leaderboard.response.status, 200);
    assert.equal(leaderboard.body.mode, "Community Leaderboard");
    assert.equal(leaderboard.body.leaderboard[0].displayAddress, "0xMock");
    assert.equal(leaderboard.body.leaderboard[0].mintedCount, 7);
  } finally {
    server.close();
    resetMockMintForTests();
  }
});
