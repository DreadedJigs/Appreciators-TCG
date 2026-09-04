import { createHash, createHmac, randomBytes } from "node:crypto";
import { getPrototypeCardByIdSync, getPrototypeCardsSync } from "./cardRepository.js";

// This module deliberately has no HTTP or persistence dependency. A match may
// only be changed through applyAuthoritativeMatchAction; the browser supplies
// an intent, never card stats, draw order, score, combat damage, or outcome.
export const AUTHORITATIVE_RULES_VERSION = "authoritative-v2";
const DECK_SIZE = 12;
const HAND_LIMIT = 2;
const BOARD_LIMIT = 3;
const APPRECIATION_TO_WIN = 50;
const STARTING_HEALTH = 30;
const DEFAULT_EVENT_SECRET = "development-only-match-event-secret";
const ACTIONS = new Set([
  "draw",
  "build",
  "discard",
  "attack",
  "resolve-battle",
  "grow",
  "end-round",
  "concede",
  "emote"
]);

export function createAuthoritativeMatch({ mode, host, hostDeckIds, guest, guestDeckIds, eventSecret }) {
  const seed = randomBytes(16).toString("hex");
  const state = {
    schemaVersion: 2,
    rulesVersion: AUTHORITATIVE_RULES_VERSION,
    seedCommitment: sha256(seed),
    rngSeed: seed,
    nextInstanceId: 1,
    appreciationToWin: APPRECIATION_TO_WIN,
    startingHealth: STARTING_HEALTH,
    boardLimit: BOARD_LIMIT,
    handLimit: HAND_LIMIT,
    players: {
      host: createPlayerState(host, createDeck(hostDeckIds, seed, "host")),
      guest: createPlayerState(guest, createDeck(guestDeckIds, seed, "guest"))
    }
  };
  const createdAt = nowIso();
  const match = {
    id: "match_" + randomBytes(16).toString("hex"),
    mode,
    status: "active",
    version: 1,
    round: 1,
    phase: "draw",
    activeSide: "host",
    players: [
      { accountId: host.id, displayName: host.username, side: "host", deckIds: state.players.host.deck.slice() },
      { accountId: guest.id, displayName: guest.username, side: "guest", deckIds: state.players.guest.deck.slice() }
    ],
    state,
    events: [],
    actionIds: [],
    result: null,
    integrity: { algorithm: "hmac-sha256", latestHash: "", eventCount: 0 },
    createdAt,
    updatedAt: createdAt
  };
  appendEvent(match, {
    actionId: "match-created",
    type: "match-started",
    actorId: "",
    side: "",
    payload: { rulesVersion: AUTHORITATIVE_RULES_VERSION, seedCommitment: state.seedCommitment },
    createdAt
  }, eventSecret);
  return match;
}

export function applyAuthoritativeMatchAction(match, actorAccountId, rawAction, eventSecret) {
  assertAuthoritativeMatch(match);
  const action = normalizeAction(rawAction);
  const actor = playerForAccount(match, actorAccountId);
  if (!actor) throw requestError("You are not a participant in this match.", 403, "MATCH_MEMBERSHIP_REQUIRED");
  if (match.status === "complete") throw requestError("This match is complete.", 409, "MATCH_COMPLETE");
  if (match.actionIds.includes(action.actionId)) {
    return {
      idempotentReplay: true,
      match: publicAuthoritativeMatch(match, actorAccountId),
      event: publicEvent(match.events.find((event) => event.actionId === action.actionId), actor.side)
    };
  }

  let details;
  if (action.type === "emote") {
    details = { message: action.payload.message };
  } else if (action.type === "concede") {
    details = concede(match, actor.side);
  } else {
    if (actor.side !== match.activeSide) throw requestError("It is not your turn.", 409, "NOT_ACTIVE_PLAYER");
    details = applyTurnAction(match, actor.side, action);
  }

  match.actionIds.push(action.actionId);
  if (match.actionIds.length > 250) match.actionIds.splice(0, match.actionIds.length - 250);
  match.version += 1;
  match.updatedAt = nowIso();
  const event = appendEvent(match, {
    actionId: action.actionId,
    type: action.type,
    actorId: actorAccountId,
    side: actor.side,
    payload: details,
    createdAt: match.updatedAt
  }, eventSecret);
  return {
    idempotentReplay: false,
    match: publicAuthoritativeMatch(match, actorAccountId),
    event: publicEvent(event, actor.side)
  };
}

export function publicAuthoritativeMatch(match, accountId) {
  const actor = playerForAccount(match, accountId);
  const state = match.state;
  const players = ["host", "guest"].map((side) => {
    const player = state.players[side];
    const isOwner = actor?.side === side;
    return {
      accountId: player.accountId,
      displayName: player.displayName,
      side,
      appreciation: player.appreciation,
      health: player.health,
      deckCount: player.deck.length,
      handCount: player.hand.length,
      hand: isOwner ? player.hand.map(publicCard) : undefined,
      board: player.board.map(publicBoardCard),
      discard: player.discard.map(publicCard),
      committedThisTurn: player.committedThisTurn
    };
  });
  return {
    id: match.id,
    mode: match.mode,
    status: match.status,
    version: match.version,
    round: match.round,
    phase: match.phase,
    activeSide: match.activeSide,
    yourSide: actor?.side || "",
    rulesVersion: state.rulesVersion,
    seedCommitment: state.seedCommitment,
    seedReveal: match.status === "complete" ? state.rngSeed : "",
    appreciationToWin: state.appreciationToWin,
    players,
    result: match.result,
    updatedAt: match.updatedAt,
    integrity: { ...match.integrity }
  };
}

export function publicAuthoritativeEvents(match, accountId, afterSequence = 0) {
  const actor = playerForAccount(match, accountId);
  const after = Math.max(0, Number.parseInt(afterSequence, 10) || 0);
  return {
    match: publicAuthoritativeMatch(match, accountId),
    events: match.events.filter((event) => event.sequence > after).map((event) => publicEvent(event, actor?.side || "")),
    latestSequence: match.events.at(-1)?.sequence || 0
  };
}

export function verifyAuthoritativeMatchIntegrity(match, eventSecret) {
  assertAuthoritativeMatch(match);
  let previousHash = "";
  for (const event of match.events) {
    const expected = eventHash(previousHash, event, eventSecret);
    if (event.previousHash !== previousHash || event.hash !== expected) return false;
    previousHash = event.hash;
  }
  return match.integrity?.latestHash === previousHash && match.integrity?.eventCount === match.events.length;
}

export function isAuthoritativeMatch(match) {
  return match?.state?.rulesVersion === AUTHORITATIVE_RULES_VERSION;
}

function applyTurnAction(match, side, action) {
  const state = match.state;
  const player = state.players[side];
  const opponent = state.players[oppositeSide(side)];
  if (action.type === "draw") {
    requirePhase(match, "draw");
    const drawn = drawToHand(player, state.handLimit);
    match.phase = "learn";
    return { drawn, handCount: player.hand.length, phase: "learn" };
  }
  if (action.type === "build" || action.type === "discard") {
    requirePhase(match, "learn");
    if (player.committedThisTurn) throw requestError("You have already made your Build or Discard decision this turn.", 409, "DECISION_ALREADY_MADE");
    const cardIndex = player.hand.findIndex((card) => card.id === action.payload.cardId);
    if (cardIndex < 0) throw requestError("That card is not in your hand.", 409, "CARD_NOT_IN_HAND");
    if (action.type === "build" && player.board.length >= state.boardLimit) {
      throw requestError("Your board is full. Remove a card before building another.", 409, "BOARD_FULL");
    }
    const card = player.hand.splice(cardIndex, 1)[0];
    player.committedThisTurn = true;
    if (action.type === "build") {
      const instance = {
        ...card,
        instanceId: "card_" + match.id.slice(-8) + "_" + state.nextInstanceId++,
        currentAttack: card.attack,
        currentDefense: card.defense,
        exhausted: false,
        builtRound: match.round
      };
      player.board.push(instance);
      match.phase = "battle";
      return { card: publicBoardCard(instance), phase: "battle", message: card.name + " was built." };
    }
    player.discard.push(card);
    const reward = discardReward(card);
    player.pendingGrowth = (player.pendingGrowth || 0) + reward;
    match.phase = "battle";
    return { card: publicCard(card), growthQueued: reward, phase: "battle", message: card.name + " was discarded." };
  }
  if (action.type === "attack") {
    requirePhase(match, "battle");
    const attacker = player.board.find((card) => card.instanceId === action.payload.attackerInstanceId);
    if (!attacker) throw requestError("Choose one of your cards on the battlefield.", 409, "INVALID_ATTACKER");
    if (attacker.exhausted || attacker.currentAttack <= 0) throw requestError("That card cannot attack this battle.", 409, "ATTACKER_UNAVAILABLE");
    const result = resolveAttack(player, attacker, opponent, action.payload.targetInstanceId);
    return { ...result, phase: "battle" };
  }
  if (action.type === "resolve-battle") {
    requirePhase(match, "battle");
    match.phase = "grow";
    return { phase: "grow", message: "Battle resolved. Tally your Appreciation." };
  }
  if (action.type === "grow" || action.type === "end-round") {
    requirePhase(match, "grow");
    const growth = calculateGrowth(player);
    player.appreciation += growth;
    player.pendingGrowth = 0;
    if (player.appreciation >= state.appreciationToWin) {
      match.status = "complete";
      match.result = { winnerSide: side, reason: "appreciation", completedAt: nowIso() };
      return { growth, appreciation: player.appreciation, result: match.result };
    }
    if (opponent.health <= 0) {
      match.status = "complete";
      match.result = { winnerSide: side, reason: "health", completedAt: nowIso() };
      return { growth, appreciation: player.appreciation, result: match.result };
    }
    finishTurn(match, side);
    return { growth, appreciation: player.appreciation, phase: "draw", nextSide: match.activeSide };
  }
  throw requestError("That online action is not supported.", 400, "INVALID_MATCH_ACTION");
}

function resolveAttack(player, attacker, opponent, targetInstanceId) {
  const target = targetInstanceId ? opponent.board.find((card) => card.instanceId === targetInstanceId) : null;
  if (!target) {
    if (opponent.board.length > 0) throw requestError("Choose an opposing card to attack.", 409, "TARGET_REQUIRED");
    attacker.exhausted = true;
    opponent.health = Math.max(0, opponent.health - Math.max(0, attacker.currentAttack));
    return { attackerInstanceId: attacker.instanceId, directDamage: attacker.currentAttack, opponentHealth: opponent.health };
  }
  attacker.exhausted = true;
  target.currentDefense -= attacker.currentAttack;
  attacker.currentDefense -= Math.max(0, target.currentAttack);
  const destroyed = [];
  if (target.currentDefense <= 0) {
    opponent.board = opponent.board.filter((card) => card.instanceId !== target.instanceId);
    opponent.discard.push(stripBoardState(target));
    destroyed.push(target.instanceId);
  }
  if (attacker.currentDefense <= 0) {
    player.board = player.board.filter((card) => card.instanceId !== attacker.instanceId);
    player.discard.push(stripBoardState(attacker));
    destroyed.push(attacker.instanceId);
  }
  return { attackerInstanceId: attacker.instanceId, targetInstanceId: target.instanceId, destroyed };
}

function finishTurn(match, side) {
  const player = match.state.players[side];
  for (const card of player.board) card.exhausted = false;
  player.committedThisTurn = false;
  const next = oppositeSide(side);
  match.activeSide = next;
  if (next === "host") match.round += 1;
  match.phase = "draw";
}

function calculateGrowth(player) {
  const boardGrowth = player.board.reduce((total, card) => total + Math.max(1, Math.ceil(card.appreciation / 2)), 0);
  return Math.max(0, boardGrowth + Math.max(0, Number(player.pendingGrowth) || 0));
}

function discardReward(card) {
  // Discard remains a viable decision; no early-discard penalty is applied.
  return Math.max(1, Math.ceil(Math.max(0, card.appreciation) / 3));
}

function drawToHand(player, limit) {
  let drawn = 0;
  while (player.hand.length < limit && player.deck.length > 0) {
    const card = canonicalCard(player.deck.shift());
    if (!card) continue;
    player.hand.push(card);
    drawn += 1;
  }
  return drawn;
}

function createPlayerState(account, deck) {
  return {
    accountId: account.id,
    displayName: account.username,
    deck,
    hand: [],
    board: [],
    discard: [],
    appreciation: 0,
    health: STARTING_HEALTH,
    pendingGrowth: 0,
    committedThisTurn: false
  };
}

function createDeck(requestedIds, seed, side) {
  const eligible = getPrototypeCardsSync().cards.filter((card) => card?.id && card.rarity !== "1/1");
  const eligibleById = new Map(eligible.map((card) => [card.id, card]));
  const requested = Array.isArray(requestedIds) ? requestedIds.filter((id) => eligibleById.has(id)) : [];
  const source = requested.length >= DECK_SIZE ? requested.slice(0, DECK_SIZE) : eligible.map((card) => card.id).slice(0, DECK_SIZE);
  return shuffle(source.slice(), seedToState(seed + ":" + side));
}

function canonicalCard(cardId) {
  const source = getPrototypeCardByIdSync(cardId);
  if (!source) return null;
  return {
    id: source.id,
    name: source.name,
    rarity: source.rarity,
    type: source.type,
    attack: clampInt(source.attack, 0, 99),
    defense: clampInt(source.defense, 1, 99),
    appreciation: clampInt(source.appreciation, 0, 99),
    effectId: source.effectId || "none",
    discardEffectId: source.discardEffectId || "none"
  };
}

function publicCard(card) {
  return card ? {
    id: card.id,
    name: card.name,
    rarity: card.rarity,
    type: card.type,
    attack: card.attack,
    defense: card.defense,
    appreciation: card.appreciation,
    effectId: card.effectId,
    discardEffectId: card.discardEffectId
  } : null;
}

function publicBoardCard(card) {
  return {
    ...publicCard(card),
    instanceId: card.instanceId,
    currentAttack: card.currentAttack,
    currentDefense: card.currentDefense,
    exhausted: Boolean(card.exhausted),
    builtRound: card.builtRound
  };
}

function stripBoardState(card) {
  return publicCard(card);
}

function concede(match, side) {
  match.status = "complete";
  match.result = { winnerSide: oppositeSide(side), reason: "concession", completedAt: nowIso() };
  return { result: match.result };
}

function requirePhase(match, expected) {
  if (match.phase !== expected) throw requestError("This action is only available during " + expected + ".", 409, "INVALID_PHASE");
}

function playerForAccount(match, accountId) {
  const side = match.players.find((player) => player.accountId === accountId)?.side;
  return side ? { side, state: match.state?.players?.[side] } : null;
}

function appendEvent(match, values, eventSecret) {
  const previousHash = match.integrity.latestHash || "";
  const event = {
    sequence: match.events.length ? match.events.at(-1).sequence + 1 : 1,
    actionId: values.actionId,
    type: values.type,
    actorId: values.actorId,
    side: values.side,
    payload: values.payload,
    round: match.round,
    phase: match.phase,
    createdAt: values.createdAt,
    previousHash
  };
  event.hash = eventHash(previousHash, event, eventSecret);
  match.events.push(event);
  match.integrity.latestHash = event.hash;
  match.integrity.eventCount = match.events.length;
  return event;
}

function publicEvent(event, viewerSide) {
  if (!event) return null;
  const payload = structuredClone(event.payload || {});
  if (event.type === "draw" && event.side !== viewerSide) delete payload.cards;
  return { ...event, payload };
}

function eventHash(previousHash, event, eventSecret) {
  const material = stableJson({
    previousHash,
    sequence: event.sequence,
    actionId: event.actionId,
    type: event.type,
    actorId: event.actorId,
    side: event.side,
    payload: event.payload,
    round: event.round,
    phase: event.phase,
    createdAt: event.createdAt
  });
  const secret = String(eventSecret || process.env.MATCH_EVENT_SIGNING_SECRET || DEFAULT_EVENT_SECRET);
  return createHmac("sha256", secret).update(material).digest("hex");
}

function normalizeAction(payload = {}) {
  const type = String(payload.type || "").trim();
  if (!ACTIONS.has(type)) throw requestError("That online action is not supported.", 400, "INVALID_MATCH_ACTION");
  const actionId = String(payload.actionId || "").trim();
  if (!/^[A-Za-z0-9_-]{8,80}$/.test(actionId)) throw requestError("A unique actionId is required.", 400, "INVALID_ACTION_ID");
  const cardId = String(payload.cardId || "").trim();
  const attackerInstanceId = String(payload.attackerInstanceId || "").trim();
  const targetInstanceId = String(payload.targetInstanceId || "").trim();
  if ((type === "build" || type === "discard") && !canonicalCard(cardId)) {
    throw requestError("A valid card from the current card set is required.", 400, "INVALID_CARD_ID");
  }
  if (type === "attack" && !attackerInstanceId) throw requestError("Select an attacker.", 400, "INVALID_ATTACKER");
  return {
    type,
    actionId,
    payload: {
      cardId,
      attackerInstanceId: attackerInstanceId.slice(0, 96),
      targetInstanceId: targetInstanceId.slice(0, 96),
      message: String(payload.message || "").replace(/[<>]/g, "").trim().slice(0, 160)
    }
  };
}

function assertAuthoritativeMatch(match) {
  if (!isAuthoritativeMatch(match)) throw requestError("This legacy match cannot be resumed. Start a new authoritative match.", 409, "MATCH_RULES_UPGRADE_REQUIRED");
}

function oppositeSide(side) {
  return side === "host" ? "guest" : "host";
}

function seedToState(value) {
  return Number.parseInt(sha256(String(value)).slice(0, 8), 16) >>> 0;
}

function shuffle(items, seed) {
  let state = seed || 1;
  const next = () => {
    state |= 0;
    state = (state + 0x6D2B79F5) | 0;
    let value = Math.imul(state ^ state >>> 15, 1 | state);
    value = (value + Math.imul(value ^ value >>> 7, 61 | value)) ^ value;
    return ((value ^ value >>> 14) >>> 0) / 4294967296;
  };
  for (let index = items.length - 1; index > 0; index -= 1) {
    const target = Math.floor(next() * (index + 1));
    [items[index], items[target]] = [items[target], items[index]];
  }
  return items;
}

function stableJson(value) {
  if (Array.isArray(value)) return "[" + value.map(stableJson).join(",") + "]";
  if (value && typeof value === "object") {
    return "{" + Object.keys(value).sort().map((key) => JSON.stringify(key) + ":" + stableJson(value[key])).join(",") + "}";
  }
  return JSON.stringify(value);
}

function sha256(value) {
  return createHash("sha256").update(String(value)).digest("hex");
}

function clampInt(value, minimum, maximum) {
  const parsed = Number.parseInt(value, 10);
  return Math.max(minimum, Math.min(maximum, Number.isInteger(parsed) ? parsed : minimum));
}

function nowIso() {
  return new Date().toISOString();
}

function requestError(message, statusCode, errorCode) {
  return Object.assign(new Error(message), { statusCode, errorCode });
}
