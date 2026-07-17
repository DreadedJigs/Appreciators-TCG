import crypto from "node:crypto";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { getPrototypeCardByIdSync } from "./cardRepository.js";

const rooms = new Map();
const lobbyPlayers = new Map();
const CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
const MAX_DECK_IDS = 12;
const LANES = ["Art", "Community", "Blockchain"];
const MAX_TURN = 11;
const MAX_CARDS_PER_LANE = 8;
const LOBBY_PLAYER_TTL_MS = 75 * 1000;
const DEFAULT_ROOM_TTL_MINUTES = 360;
const roomTtlMinutes = Math.max(
  15,
  Number.parseInt(process.env.INVITE_ROOM_TTL_MINUTES, 10) || DEFAULT_ROOM_TTL_MINUTES
);
const roomTtlMs = roomTtlMinutes * 60 * 1000;
const persistenceEnabled =
  process.env.INVITE_ROOM_PERSISTENCE === "true" ||
  (process.env.NODE_ENV === "production" && process.env.INVITE_ROOM_PERSISTENCE !== "false");
const persistencePath = resolve(process.env.INVITE_ROOM_STORE_PATH || "data/runtime/invite-rooms.json");

let roomsLoaded = false;

function loadRoomsIfNeeded() {
  if (roomsLoaded) {
    return;
  }

  roomsLoaded = true;
  if (!persistenceEnabled) {
    return;
  }

  try {
    const stored = JSON.parse(readFileSync(persistencePath, "utf8"));
    if (!Array.isArray(stored?.rooms)) {
      return;
    }

    rooms.clear();
    for (const room of stored.rooms) {
      if (room?.inviteCode) {
        rooms.set(String(room.inviteCode).toUpperCase(), room);
      }
    }

    pruneExpiredRooms();
  } catch (error) {
    if (error.code !== "ENOENT") {
      console.warn(`Invite room persistence could not load ${persistencePath}: ${error.message}`);
    }
  }
}

function saveRoomsIfNeeded() {
  if (!persistenceEnabled) {
    return;
  }

  try {
    mkdirSync(dirname(persistencePath), { recursive: true });
    writeFileSync(
      persistencePath,
      JSON.stringify({
        savedAt: new Date().toISOString(),
        ttlMinutes: roomTtlMinutes,
        rooms: [...rooms.values()]
      }, null, 2)
    );
  } catch (error) {
    console.warn(`Invite room persistence could not save ${persistencePath}: ${error.message}`);
  }
}

function roomExpired(room, nowMs = Date.now()) {
  const updatedMs = Date.parse(room?.updatedAt || room?.createdAt || "");
  return Number.isFinite(updatedMs) && nowMs - updatedMs > roomTtlMs;
}

function pruneExpiredRooms() {
  let removed = false;
  const nowMs = Date.now();
  for (const [code, room] of rooms.entries()) {
    if (roomExpired(room, nowMs)) {
      rooms.delete(code);
      removed = true;
    }
  }

  if (removed) {
    saveRoomsIfNeeded();
  }
}

function safeUsername(value) {
  const username = String(value || "Guest").trim().slice(0, 24);
  return username || "Guest";
}

function safePlayerId(value) {
  const playerId = String(value || "")
    .trim()
    .replace(/[^a-zA-Z0-9_-]/g, "")
    .slice(0, 64);
  return playerId;
}

function safeDeckIds(value) {
  if (!Array.isArray(value)) {
    return [];
  }

  return value
    .map((id) => String(id || "").trim())
    .filter(Boolean)
    .slice(0, MAX_DECK_IDS);
}

function createInviteCode() {
  loadRoomsIfNeeded();
  for (let attempt = 0; attempt < 20; attempt += 1) {
    let code = "";
    for (let i = 0; i < 6; i += 1) {
      code += CODE_ALPHABET[Math.floor(Math.random() * CODE_ALPHABET.length)];
    }

    if (!rooms.has(code)) {
      return code;
    }
  }

  throw Object.assign(new Error("Unable to allocate invite code."), { statusCode: 503 });
}

function createPlayer(input, role) {
  const now = new Date().toISOString();
  return {
    id: safePlayerId(input?.playerId) || crypto.randomUUID(),
    role,
    username: safeUsername(input?.username),
    deckIds: safeDeckIds(input?.deckIds),
    connected: true,
    joinedAt: now,
    lastSeenAt: now
  };
}

function publicPlayer(player) {
  if (!player) {
    return null;
  }

  return {
    id: player.id,
    role: player.role,
    username: player.username,
    deckSize: player.deckIds.length,
    connected: player.connected,
    joinedAt: player.joinedAt,
    lastSeenAt: player.lastSeenAt || player.joinedAt
  };
}

function publicLobbyPlayer(player, selfId = "") {
  if (!player) {
    return null;
  }

  return {
    id: player.id,
    username: player.username,
    deckSize: player.deckIds.length,
    status: player.status,
    firstSeenAt: player.firstSeenAt,
    lastSeenAt: player.lastSeenAt,
    self: Boolean(selfId && player.id === selfId)
  };
}

function publicRoom(room) {
  return {
    inviteCode: room.inviteCode,
    matchId: room.matchId,
    mode: "Invite 1v1",
    status: room.status,
    createdAt: room.createdAt,
    updatedAt: room.updatedAt,
    startedAt: room.startedAt || null,
    host: publicPlayer(room.host),
    guest: publicPlayer(room.guest),
    players: [publicPlayer(room.host), publicPlayer(room.guest)].filter(Boolean),
    maxPlayers: 2,
    challengeTargetId: room.challengeTargetId || "",
    challengeTargetUsername: room.challengeTargetUsername || "",
    matchState: publicMatchState(room.matchState),
    message: room.message
  };
}

function pruneLobbyPlayers(nowMs = Date.now()) {
  for (const [id, player] of lobbyPlayers.entries()) {
    const lastSeenMs = Date.parse(player?.lastSeenAt || "");
    if (!Number.isFinite(lastSeenMs) || nowMs - lastSeenMs > LOBBY_PLAYER_TTL_MS) {
      lobbyPlayers.delete(id);
    }
  }
}

function upsertLobbyPlayer(input = {}) {
  pruneLobbyPlayers();
  const now = new Date().toISOString();
  const id = safePlayerId(input.playerId) || crypto.randomUUID();
  const existing = lobbyPlayers.get(id);
  const player = {
    id,
    username: safeUsername(input.username || existing?.username),
    deckIds: safeDeckIds(input.deckIds || existing?.deckIds),
    status: "available",
    firstSeenAt: existing?.firstSeenAt || now,
    lastSeenAt: now
  };

  lobbyPlayers.set(id, player);
  return player;
}

function publicAction(action) {
  return {
    sequence: action.sequence,
    actionId: action.actionId,
    type: action.type,
    playerId: action.playerId,
    username: action.username,
    role: action.role,
    cardId: action.cardId,
    lane: action.lane,
    turn: action.turn,
    createdAt: action.createdAt
  };
}

function ensureMatchResources(matchState) {
  if (!matchState) {
    return null;
  }

  const startingStock = Math.max(1, Number.parseInt(matchState.currentTurn, 10) || 1);
  matchState.resources ||= {};
  matchState.resources.host ||= { art: startingStock, blockchain: startingStock, shield: 0, rally: 0 };
  matchState.resources.guest ||= { art: startingStock, blockchain: startingStock, shield: 0, rally: 0 };
  return matchState.resources;
}

function createMatchState() {
  return {
    status: "waiting",
    currentTurn: 1,
    maxTurn: MAX_TURN,
    energy: {
      host: 1,
      guest: 1
    },
    resources: {
      host: { art: 1, blockchain: 1, shield: 0, rally: 0 },
      guest: { art: 1, blockchain: 1, shield: 0, rally: 0 }
    },
    endedTurn: {
      host: false,
      guest: false
    },
    lanes: Object.fromEntries(LANES.map((lane) => [
      lane,
      {
        host: [],
        guest: []
      }
    ])),
    termination: createTerminationState(),
    result: null,
    version: 0,
    message: "Waiting for both players."
  };
}

function createTerminationState() {
  return {
    status: "none",
    requestedByPlayerId: "",
    requestedByRole: "",
    requestedByUsername: "",
    hostAccepted: false,
    guestAccepted: false,
    requestedAt: null,
    resolvedAt: null,
    declinedByUsername: ""
  };
}

function ensureTerminationState(matchState) {
  if (!matchState.termination) {
    matchState.termination = createTerminationState();
  }

  return matchState.termination;
}

function publicTerminationState(termination) {
  const state = termination || createTerminationState();
  return {
    status: state.status || "none",
    requestedByPlayerId: state.requestedByPlayerId || "",
    requestedByRole: state.requestedByRole || "",
    requestedByUsername: state.requestedByUsername || "",
    hostAccepted: Boolean(state.hostAccepted),
    guestAccepted: Boolean(state.guestAccepted),
    requestedAt: state.requestedAt || null,
    resolvedAt: state.resolvedAt || null,
    declinedByUsername: state.declinedByUsername || ""
  };
}

function publicCardEntry(entry) {
  const card = getPrototypeCardByIdSync(entry.cardId);
  return {
    sequence: entry.sequence,
    cardId: entry.cardId,
    name: card?.name || entry.cardId,
    power: calculateCardPower(entry.cardId, entry.lane),
    appreciation: Number(card?.appreciation || 0),
    lane: entry.lane,
    playedAt: entry.playedAt
  };
}

function publicLaneState(matchState, lane) {
  const laneState = matchState.lanes[lane];
  const hostCards = laneState.host.map(publicCardEntry);
  const guestCards = laneState.guest.map(publicCardEntry);
  const hostPower = hostCards.reduce((total, card) => total + card.power, 0);
  const guestPower = guestCards.reduce((total, card) => total + card.power, 0);

  let winner = "Tie";
  if (hostPower > guestPower) {
    winner = "host";
  } else if (guestPower > hostPower) {
    winner = "guest";
  }

  return {
    lane,
    host: hostCards,
    guest: guestCards,
    hostPower,
    guestPower,
    winner
  };
}

function publicMatchState(matchState) {
  if (!matchState) {
    return null;
  }

  const resources = ensureMatchResources(matchState);
  return {
    status: matchState.status,
    currentTurn: matchState.currentTurn,
    maxTurn: matchState.maxTurn,
    energy: matchState.energy,
    resources,
    endedTurn: matchState.endedTurn,
    lanes: LANES.map((lane) => publicLaneState(matchState, lane)),
    termination: publicTerminationState(matchState.termination),
    result: matchState.result,
    version: matchState.version,
    message: matchState.message
  };
}

function normalizeLane(value) {
  const laneName = String(value || "").trim();
  return LANES.find((lane) => lane.toLowerCase() === laneName.toLowerCase()) || "";
}

function calculateCardPower(cardId, lane) {
  const card = getPrototypeCardByIdSync(cardId);
  if (!card) {
    return 0;
  }

  let power = Number(card.power || 0);
  if (card.effectId === "ghost_companion" && lane === "Blockchain") {
    power += 2;
  }

  if (card.effectId === "blockchain_background" && lane === "Blockchain") {
    power += 2;
  }

  return power;
}

function scoreMatch(matchState) {
  const laneScores = LANES.map((lane) => publicLaneState(matchState, lane));
  const hostLaneWins = laneScores.filter((lane) => lane.winner === "host").length;
  const guestLaneWins = laneScores.filter((lane) => lane.winner === "guest").length;
  let winner = "Draw";

  if (hostLaneWins >= 2) {
    winner = "host";
  } else if (guestLaneWins >= 2) {
    winner = "guest";
  }

  return {
    laneScores,
    hostLaneWins,
    guestLaneWins,
    winner
  };
}

function applyActionToMatchState(room, action) {
  const matchState = room.matchState;
  if (!matchState || matchState.status === "complete") {
    return;
  }

  ensureMatchResources(matchState);

  if (action.type === "play-card") {
    const lane = normalizeLane(action.lane);
    const laneState = matchState.lanes[lane];
    if (!laneState) {
      throw Object.assign(new Error("Invite action lane is invalid."), { statusCode: 400 });
    }

    const sideCards = laneState[action.role];
    if (sideCards.length >= MAX_CARDS_PER_LANE) {
      throw Object.assign(new Error(`${lane} lane is full for this player.`), { statusCode: 409 });
    }

    sideCards.push({
      sequence: action.sequence,
      cardId: action.cardId,
      lane,
      playedAt: action.createdAt
    });
    matchState.message = `${action.username} played a card in ${lane}.`;
  }

  if (action.type === "discard-card") {
    const card = getPrototypeCardByIdSync(action.cardId);
    matchState.message = `${action.username} revealed and discarded ${card?.name || action.cardId}.`;
  }

  if (action.type === "discard-card-for-shard") {
    const card = getPrototypeCardByIdSync(action.cardId);
    const shardLane = normalizeLane(card?.discardShardLane);
    const shardValue = Math.max(0, Number(card?.discardShardValue || 0));
    const resourceKey = shardLane.toLowerCase();
    const resources = matchState.resources[action.role];
    if (!resources || !["art", "blockchain"].includes(resourceKey) || shardValue <= 0) {
      throw Object.assign(new Error("Card cannot be discarded for shards."), { statusCode: 409 });
    }

    resources[resourceKey] += shardValue;
    matchState.message = `${action.username} discarded ${card.name} for +${shardValue} ${shardLane} shard.`;
  }

  if (action.type === "spend-community-defense") {
    const resources = matchState.resources[action.role];
    if (!resources || resources.art <= 0 || resources.shield >= 3) {
      throw Object.assign(new Error("Art shard cannot be invested in Community defense."), { statusCode: 409 });
    }

    resources.art -= 1;
    resources.shield += 1;
    matchState.message = `${action.username} invested an Art shard in Community defense.`;
  }

  if (action.type === "spend-community-rally") {
    const resources = matchState.resources[action.role];
    if (!resources || resources.blockchain <= 0 || resources.rally >= 3) {
      throw Object.assign(new Error("Blockchain shard cannot be invested in Community rally."), { statusCode: 409 });
    }

    resources.blockchain -= 1;
    resources.rally += 1;
    matchState.message = `${action.username} invested a Blockchain shard in Community rally.`;
  }

  if (action.type === "end-turn") {
    matchState.endedTurn[action.role] = true;
    matchState.message = `${action.username} ended turn ${matchState.currentTurn}.`;

    if (matchState.endedTurn.host && matchState.endedTurn.guest) {
      matchState.resources.host.shield = 0;
      matchState.resources.host.rally = 0;
      matchState.resources.guest.shield = 0;
      matchState.resources.guest.rally = 0;
      if (matchState.currentTurn >= matchState.maxTurn) {
        matchState.status = "complete";
        matchState.result = scoreMatch(matchState);
        matchState.message = "Invite match complete.";
      } else {
        matchState.currentTurn += 1;
        matchState.energy.host = matchState.currentTurn;
        matchState.energy.guest = matchState.currentTurn;
        matchState.resources.host.art += 1;
        matchState.resources.host.blockchain += 1;
        matchState.resources.guest.art += 1;
        matchState.resources.guest.blockchain += 1;
        matchState.endedTurn.host = false;
        matchState.endedTurn.guest = false;
        matchState.message = `Turn ${matchState.currentTurn} started.`;
      }
    }
  }

  matchState.version += 1;
}

function getRoomOrThrow(inviteCode) {
  loadRoomsIfNeeded();
  pruneExpiredRooms();
  const code = String(inviteCode || "").trim().toUpperCase();
  const room = rooms.get(code);
  if (!room) {
    throw Object.assign(new Error("Invite room not found."), { statusCode: 404 });
  }

  return room;
}

export function createInviteRoom(input = {}) {
  loadRoomsIfNeeded();
  pruneExpiredRooms();
  const now = new Date().toISOString();
  const inviteCode = createInviteCode();
  const room = {
    inviteCode,
    matchId: `invite_${inviteCode.toLowerCase()}_${Date.now()}`,
    status: "waiting",
    createdAt: now,
    updatedAt: now,
    startedAt: null,
    host: createPlayer(input, "host"),
    guest: null,
    actions: [],
    nextActionSequence: 1,
    matchState: createMatchState(),
    message: "Waiting for invited player."
  };

  rooms.set(inviteCode, room);
  saveRoomsIfNeeded();
  return {
    room: publicRoom(room),
    player: publicPlayer(room.host)
  };
}

export function getInviteRoom(inviteCode) {
  return publicRoom(getRoomOrThrow(inviteCode));
}

export function getInviteMatchState(inviteCode) {
  const room = getRoomOrThrow(inviteCode);
  return {
    room: publicRoom(room),
    matchState: publicMatchState(room.matchState)
  };
}

export function announceInvitePresence(input = {}) {
  const player = upsertLobbyPlayer(input);
  return getInviteLobby({ ...input, playerId: player.id });
}

export function getInviteLobby(input = {}) {
  loadRoomsIfNeeded();
  pruneExpiredRooms();
  pruneLobbyPlayers();

  const shouldTrackSelf = Boolean(safePlayerId(input.playerId) || input.username);
  const player = shouldTrackSelf ? upsertLobbyPlayer(input) : null;
  const selfId = player?.id || safePlayerId(input.playerId);

  const players = [...lobbyPlayers.values()]
    .filter((candidate) => candidate.id !== selfId)
    .sort((a, b) => a.username.localeCompare(b.username))
    .map((candidate) => publicLobbyPlayer(candidate, selfId));

  const challenges = [...rooms.values()]
    .filter((room) => room.challengeTargetId === selfId && room.status !== "started")
    .sort((a, b) => Date.parse(b.updatedAt) - Date.parse(a.updatedAt))
    .map(publicRoom);

  return {
    playerId: selfId,
    players,
    challenges,
    message: "Invite lobby synced."
  };
}

export function challengeInvitePlayer(input = {}) {
  loadRoomsIfNeeded();
  pruneExpiredRooms();
  pruneLobbyPlayers();

  const targetId = safePlayerId(input.targetPlayerId);
  const target = lobbyPlayers.get(targetId);
  if (!target) {
    throw Object.assign(new Error("Target player is no longer available."), { statusCode: 404 });
  }

  const created = createInviteRoom(input);
  const room = getRoomOrThrow(created.room.inviteCode);
  const now = new Date().toISOString();
  room.challengeTargetId = target.id;
  room.challengeTargetUsername = target.username;
  room.updatedAt = now;
  room.message = `${room.host.username} challenged ${target.username}.`;
  saveRoomsIfNeeded();

  return {
    room: publicRoom(room),
    player: publicPlayer(room.host),
    challengedPlayer: publicLobbyPlayer(target, room.host.id),
    message: room.message
  };
}

export function joinInviteRoom(inviteCode, input = {}) {
  const room = getRoomOrThrow(inviteCode);
  if (room.status === "started") {
    throw Object.assign(new Error("Invite match has already started."), { statusCode: 409 });
  }

  if (room.guest && room.guest.username !== safeUsername(input.username)) {
    throw Object.assign(new Error("Invite room is already full."), { statusCode: 409 });
  }

  if (!room.guest) {
    room.guest = createPlayer(input, "guest");
  } else {
    room.guest.connected = true;
    room.guest.lastSeenAt = new Date().toISOString();
  }

  room.status = "ready";
  room.matchState.status = "ready";
  room.matchState.message = "Both players are ready.";
  room.updatedAt = new Date().toISOString();
  room.message = "Both players are ready.";
  saveRoomsIfNeeded();

  return {
    room: publicRoom(room),
    player: publicPlayer(room.guest)
  };
}

export function reconnectInviteRoom(inviteCode, input = {}) {
  const room = getRoomOrThrow(inviteCode);
  const requester = String(input?.playerId || "").trim();
  const requesterName = safeUsername(input?.username);
  const requesterRole = String(input?.role || "").trim().toLowerCase();
  const player = [room.host, room.guest].find((candidate) => {
    if (!candidate) {
      return false;
    }

    if (requester && candidate.id === requester) {
      return true;
    }

    const roleMatches = requesterRole ? candidate.role === requesterRole : true;
    return roleMatches && candidate.username === requesterName;
  });

  if (!player) {
    throw Object.assign(new Error("Invite room participant not found for reconnect."), { statusCode: 403 });
  }

  const now = new Date().toISOString();
  player.connected = true;
  player.lastSeenAt = now;
  room.updatedAt = now;
  room.message = "Player reconnected.";
  saveRoomsIfNeeded();

  return {
    room: publicRoom(room),
    player: publicPlayer(player),
    message: "Invite room reconnected."
  };
}

export function startInviteRoom(inviteCode, input = {}) {
  const room = getRoomOrThrow(inviteCode);
  const requester = String(input?.playerId || "").trim();
  const requesterName = safeUsername(input?.username);
  const isParticipant = requester
    ? room.host.id === requester || room.guest?.id === requester
    : room.host.username === requesterName || room.guest?.username === requesterName;

  if (!isParticipant) {
    throw Object.assign(new Error("Only a room participant can start this invite match."), { statusCode: 403 });
  }

  if (!room.guest) {
    throw Object.assign(new Error("Invite room needs a guest before starting."), { statusCode: 409 });
  }

  const now = new Date().toISOString();
  room.status = "started";
  room.matchState.status = "active";
  room.matchState.currentTurn = 1;
  room.matchState.energy.host = 1;
  room.matchState.energy.guest = 1;
  room.matchState.endedTurn.host = false;
  room.matchState.endedTurn.guest = false;
  room.matchState.message = "Invite match started.";
  room.matchState.version += 1;
  room.startedAt = now;
  room.updatedAt = now;
  room.message = "Invite match started.";
  saveRoomsIfNeeded();

  return {
    room: publicRoom(room),
    assignment: {
      matchId: room.matchId,
      inviteCode: room.inviteCode,
      mode: "Invite 1v1",
      status: room.status,
      players: [publicPlayer(room.host), publicPlayer(room.guest)],
      transport: "polling",
      message: "Phase 1.5 invite session created with polling card-action and turn sync."
    }
  };
}

export function respondToInviteTermination(inviteCode, input = {}) {
  const room = getRoomOrThrow(inviteCode);
  const requester = safePlayerId(input?.playerId);
  const player = [room.host, room.guest].find((candidate) => candidate?.id === requester);
  if (!player) {
    throw Object.assign(new Error("Only a room participant can respond to match termination."), { statusCode: 403 });
  }

  if (room.status === "terminated" || room.matchState?.status === "terminated") {
    return {
      room: publicRoom(room),
      matchState: publicMatchState(room.matchState),
      message: "Invite match was already terminated by agreement."
    };
  }

  if (room.status !== "started" || room.matchState?.status !== "active") {
    throw Object.assign(new Error("Only an active invite match can be terminated by agreement."), { statusCode: 409 });
  }

  const decision = String(input?.decision || "request").trim().toLowerCase();
  if (!["request", "accept", "decline"].includes(decision)) {
    throw Object.assign(new Error("Termination decision must be request, accept, or decline."), { statusCode: 400 });
  }

  const now = new Date().toISOString();
  let termination = ensureTerminationState(room.matchState);
  if (decision === "decline") {
    if (termination.status !== "pending") {
      throw Object.assign(new Error("There is no pending termination request to decline."), { statusCode: 409 });
    }

    room.matchState.termination = {
      ...createTerminationState(),
      status: "declined",
      resolvedAt: now,
      declinedByUsername: player.username
    };
    room.matchState.message = `${player.username} chose to continue the match.`;
    room.message = room.matchState.message;
  } else {
    if (termination.status !== "pending") {
      termination = createTerminationState();
      termination.status = "pending";
      termination.requestedByPlayerId = player.id;
      termination.requestedByRole = player.role;
      termination.requestedByUsername = player.username;
      termination.requestedAt = now;
      room.matchState.termination = termination;
    }

    termination[`${player.role}Accepted`] = true;
    if (termination.hostAccepted && termination.guestAccepted) {
      termination.status = "agreed";
      termination.resolvedAt = now;
      room.status = "terminated";
      room.matchState.status = "terminated";
      room.matchState.message = "Both players agreed to terminate the match.";
      room.message = room.matchState.message;
    } else {
      room.matchState.message = `${player.username} requested mutual match termination.`;
      room.message = room.matchState.message;
    }
  }

  room.matchState.version += 1;
  room.updatedAt = now;
  player.lastSeenAt = now;
  saveRoomsIfNeeded();
  return {
    room: publicRoom(room),
    matchState: publicMatchState(room.matchState),
    message: room.message
  };
}

export function recordInviteAction(inviteCode, input = {}) {
  const room = getRoomOrThrow(inviteCode);
  if (room.status !== "started") {
    throw Object.assign(new Error("Invite match has not started."), { statusCode: 409 });
  }

  const requester = String(input?.playerId || "").trim();
  const player = [room.host, room.guest].find((candidate) => candidate?.id === requester);
  if (!player) {
    throw Object.assign(new Error("Only a room participant can submit match actions."), { statusCode: 403 });
  }

  const type = String(input?.type || "").trim();
  const allowedTypes = [
    "play-card",
    "discard-card",
    "discard-card-for-shard",
    "end-turn",
    "spend-community-defense",
    "spend-community-rally"
  ];
  if (!allowedTypes.includes(type)) {
    throw Object.assign(new Error("Unsupported invite match action."), { statusCode: 400 });
  }

  if (room.matchState?.status === "complete") {
    throw Object.assign(new Error("Invite match is already complete."), { statusCode: 409 });
  }

  const actionId = String(input?.actionId || "").trim().slice(0, 64);
  if (actionId) {
    const existing = room.actions.find((action) => action.playerId === player.id && action.actionId === actionId);
    if (existing) {
      return {
        room: publicRoom(room),
        action: publicAction(existing)
      };
    }
  }

  const requestedTurn = Number.parseInt(input?.turn, 10) || room.matchState.currentTurn;
  if (requestedTurn !== room.matchState.currentTurn) {
    throw Object.assign(new Error("Invite action turn is stale."), { statusCode: 409 });
  }

  if (room.matchState.endedTurn[player.role]) {
    throw Object.assign(new Error("Player already ended this turn."), { statusCode: 409 });
  }

  const lane = normalizeLane(input?.lane);
  const cardId = String(input?.cardId || "").trim();
  if (type === "play-card" || type === "discard-card") {
    if (!getPrototypeCardByIdSync(cardId)) {
      throw Object.assign(new Error("Invite action card is not in the prototype card set."), { statusCode: 400 });
    }
  }

  if (type === "play-card") {
    if (!lane) {
      throw Object.assign(new Error("Invite action lane is required."), { statusCode: 400 });
    }
  }

  if (type === "discard-card-for-shard") {
    const card = getPrototypeCardByIdSync(cardId);
    const configuredLane = normalizeLane(card?.discardShardLane);
    const shardValue = Math.max(0, Number(card?.discardShardValue || 0));
    if (!card || shardValue <= 0 || !["Art", "Blockchain"].includes(configuredLane)) {
      throw Object.assign(new Error("Invite action card cannot be discarded for shards."), { statusCode: 400 });
    }

    if (lane !== configuredLane) {
      throw Object.assign(new Error("Invite shard lane does not match the card configuration."), { statusCode: 400 });
    }
  }

  const action = {
    sequence: room.nextActionSequence,
    actionId: actionId || `${player.id}_${room.nextActionSequence}`,
    type,
    playerId: player.id,
    username: player.username,
    role: player.role,
    cardId,
    lane,
    turn: requestedTurn,
    createdAt: new Date().toISOString()
  };

  applyActionToMatchState(room, action);
  room.nextActionSequence += 1;
  room.actions.push(action);
  room.updatedAt = action.createdAt;
  player.lastSeenAt = action.createdAt;
  saveRoomsIfNeeded();

  return {
    room: publicRoom(room),
    action: publicAction(action)
  };
}

export function getInviteActions(inviteCode, afterSequence = 0) {
  const room = getRoomOrThrow(inviteCode);
  const after = Number.parseInt(afterSequence, 10) || 0;
  return {
    room: publicRoom(room),
    actions: room.actions
      .filter((action) => action.sequence > after)
      .map(publicAction)
  };
}

export function clearInviteRoomsForTests() {
  roomsLoaded = true;
  rooms.clear();
  lobbyPlayers.clear();
  saveRoomsIfNeeded();
}
