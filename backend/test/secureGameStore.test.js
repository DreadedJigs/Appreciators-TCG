import assert from "node:assert/strict";
import { existsSync, rmSync } from "node:fs";
import test from "node:test";
import { SecureGameStore } from "../src/secureGameStore.js";
import { verifyAuthoritativeMatchIntegrity } from "../src/authoritativeMatchEngine.js";

function createStore(name) {
  return new SecureGameStore({
    filePath: `data/runtime/${name}-${process.pid}.json`,
    isProduction: false
  });
}

function removeStore(store) {
  if (existsSync(store.filePath)) rmSync(store.filePath, { force: true });
}

test("secure accounts hash passwords, issue opaque sessions, and revoke them", async () => {
  const store = createStore("secure-account");
  try {
    const registered = await store.registerAccount({
      username: "Secure Player",
      password: "AReallyStrongPass123"
    });
    assert.match(registered.accessToken, /^ses_[a-f0-9]{32}\.[A-Za-z0-9_-]+$/);
    assert.equal(registered.account.username, "Secure Player");

    const identity = await store.verifySession(registered.accessToken);
    assert.equal(identity.account.id, registered.account.id);

    const relogin = await store.loginAccount({ username: "Secure Player", password: "AReallyStrongPass123" });
    assert.equal(relogin.account.id, registered.account.id);
    const sessions = await store.listSessions(registered.accessToken);
    assert.equal(sessions.length, 2);
    const revoked = await store.revokeOtherSessions(registered.accessToken);
    assert.equal(revoked.revoked, 1);
    await assert.rejects(store.verifySession(relogin.accessToken), { errorCode: "SESSION_EXPIRED" });
    await assert.rejects(
      store.loginAccount({ username: "Secure Player", password: "definitely wrong password" }),
      { errorCode: "INVALID_CREDENTIALS" }
    );

    await store.revokeSession(registered.accessToken);
    await assert.rejects(store.verifySession(registered.accessToken), { errorCode: "SESSION_EXPIRED" });
  } finally {
    removeStore(store);
  }
});

test("cloud saves are sanitized and reject stale overwrites", async () => {
  const store = createStore("secure-cloud");
  try {
    const account = await store.registerAccount({ username: "Cloud Player", password: "CloudSafePassword123" });
    const saved = await store.saveCloudSave(account.account.id, {
      expectedVersion: 0,
      snapshot: {
        schemaVersion: 8,
        settings: { theme: "Light", musicVolume: 9, reducedMotion: true },
        deckIds: ["regular_body", "regular_body", "not valid!"],
        tutorial: { step: 5, completed: false },
        appreciationShards: 99999999
      }
    });
    assert.equal(saved.version, 1);
    assert.equal(saved.snapshot.settings.musicVolume, 1);
    assert.deepEqual(saved.snapshot.deckIds, ["regular_body"]);
    assert.equal(Object.hasOwn(saved.snapshot, "appreciationShards"), false);

    await assert.rejects(
      store.saveCloudSave(account.account.id, { expectedVersion: 0, snapshot: {} }),
      { errorCode: "CLOUD_SAVE_CONFLICT" }
    );
  } finally {
    removeStore(store);
  }
});

test("online matches enforce membership, phase order, turn ownership, and revisions", async () => {
  const store = createStore("secure-match");
  try {
    const host = await store.registerAccount({ username: "Match Host", password: "MatchHostPassword123" });
    const guest = await store.registerAccount({ username: "Match Guest", password: "MatchGuestPassword123" });
    const firstQueue = await store.queueMatch(host.account.id, { mode: "Casual", deckIds: ["regular_body"] });
    assert.equal(firstQueue.status, "queued");
    const secondQueue = await store.queueMatch(guest.account.id, { mode: "Casual", deckIds: ["no_head_body"] });
    assert.equal(secondQueue.status, "matched");
    const matchId = secondQueue.match.id;

    await assert.rejects(
      store.applyMatchAction(guest.account.id, matchId, { actionId: "guest-draw-001", type: "draw", expectedVersion: 1 }),
      { errorCode: "NOT_ACTIVE_PLAYER" }
    );
    const drew = await store.applyMatchAction(host.account.id, matchId, { actionId: "host-draw-0001", type: "draw", expectedVersion: 1 });
    assert.equal(drew.match.phase, "learn");
    const hostState = drew.match.players.find((player) => player.side === "host");
    const guestState = drew.match.players.find((player) => player.side === "guest");
    assert.equal(hostState.hand.length, 2);
    assert.equal(guestState.hand, undefined);
    const rawMatch = store.data.matches.find((match) => match.id === matchId);
    rawMatch.state.boardLimit = 0;
    await assert.rejects(
      store.applyMatchAction(host.account.id, matchId, {
        actionId: "host-build-board-full",
        type: "build",
        cardId: hostState.hand[0].id,
        expectedVersion: 2
      }),
      { errorCode: "BOARD_FULL" }
    );
    assert.equal(rawMatch.state.players.host.hand.length, 2);
    assert.equal(rawMatch.phase, "learn");
    rawMatch.state.boardLimit = 3;
    const built = await store.applyMatchAction(host.account.id, matchId, {
      actionId: "host-build-0001",
      type: "build",
      cardId: hostState.hand[0].id,
      lane: "Art",
      expectedVersion: 2
    });
    assert.equal(built.match.phase, "battle");
    const resolved = await store.applyMatchAction(host.account.id, matchId, {
      actionId: "host-battle-001",
      type: "resolve-battle",
      expectedVersion: 3
    });
    assert.equal(resolved.match.phase, "grow");
    const ended = await store.applyMatchAction(host.account.id, matchId, { actionId: "host-end-00001", type: "end-round", expectedVersion: 4 });
    assert.equal(ended.match.activeSide, "guest");
    assert.equal(ended.match.phase, "draw");
    await assert.rejects(
      store.applyMatchAction(guest.account.id, matchId, { actionId: "guest-stale-01", type: "draw", expectedVersion: 1 }),
      { errorCode: "MATCH_VERSION_CONFLICT" }
    );
    assert.equal(verifyAuthoritativeMatchIntegrity(rawMatch), true);
    rawMatch.events[0].payload.tampered = true;
    assert.equal(verifyAuthoritativeMatchIntegrity(rawMatch), false);
  } finally {
    removeStore(store);
  }
});
