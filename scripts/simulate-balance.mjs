import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const candidateMode = process.argv.includes("--candidate");
const candidateCosts = {
  unicorn_head: 2,
  alpha_kaiju_head: 4,
  decapitated_body: 3,
  ghost_flame_background: 1,
  pink_lemonade_background: 1,
  tropical_background: 1,
  overcast_background: 2,
  second_hand_smoke_dawn: 2,
  second_hand_smoke_seafoam: 2,
  green_skin: 2,
  purple_skin: 2
};
const sourceCards = JSON.parse(await readFile(path.join(root, "backend", "data", "cards.json"), "utf8")).cards;
const cards = sourceCards.map((card) => candidateMode && candidateCosts[card.id] !== undefined
  ? { ...card, cost: candidateCosts[card.id] }
  : card);
const byId = new Map(cards.map((card) => [card.id, card]));
const matchCountArgument = process.argv.find((value) => /^\d+$/.test(value));
const matchCount = Number(matchCountArgument || 20000);
const random = mulberry32(20260621);
const lanes = ["Art", "Community", "Blockchain"];
const stats = new Map(cards.map((card) => [card.id, { card, included: 0, played: 0, includeScore: 0, playScore: 0 }]));
let firstWins = 0;
let secondWins = 0;
let draws = 0;

for (let matchIndex = 0; matchIndex < matchCount; matchIndex += 1) {
  const game = createGame(randomDeck(), randomDeck());
  startGame(game);
  while (!game.complete) {
    playAiTurn(game, "player");
    playAiTurn(game, "opponent");
    if (game.turn >= 6) completeGame(game);
    else {
      game.turn += 1;
      startTurn(game);
    }
  }

  const playerScore = game.result === "Victory" ? 1 : game.result === "Draw" ? 0.5 : 0;
  const opponentScore = 1 - playerScore;
  if (playerScore === 1) firstWins += 1;
  else if (opponentScore === 1) secondWins += 1;
  else draws += 1;
  record(game.player, playerScore);
  record(game.opponent, opponentScore);
}

const sorted = [...stats.values()].sort((a, b) => playRate(b) - playRate(a));
const lines = [
  "# Appreciators TCG Preliminary Balance Report",
  "",
  `- Simulations: ${matchCount.toLocaleString("en-US")}`,
  "- Seed: 20260621",
  `- First-side wins: ${firstWins.toLocaleString("en-US")}`,
  `- Second-side wins: ${secondWins.toLocaleString("en-US")}`,
  `- Draws: ${draws.toLocaleString("en-US")}`,
  `- Mode: ${candidateMode ? "candidate cost pass + CHAOS entry trigger" : "current committed values"}`,
  "- Method: independent deterministic mirror of the Unity six-turn battle rules",
  "",
  "A draw counts as half a win. The Unity editor audit remains authoritative; this independent pass catches curve and effect outliers before that run.",
  "",
  "| Card | Cost | Art | Chain | Community | APP | Included | Inclusion WR | Played | Played WR |",
  "|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
  ...sorted.map(({ card, included, played, includeScore, playScore }) =>
    `| ${card.name} | ${card.cost} | ${card.artStrength} | ${card.blockchainStrength} | ${card.communityStrength} | ${card.appreciation} | ${included.toLocaleString("en-US")} | ${percent(includeScore / included)} | ${played.toLocaleString("en-US")} | ${percent(played ? playScore / played : 0)} |`),
  "",
  "## Interpretation",
  "",
  "Played-card win rates outside 47%-53% are manual-review flags, not automatic nerfs or buffs. Human playtests remain necessary because this AI values energy efficiency and lane deficits but cannot model bluffing or long-term sequencing intent.",
  "",
  "`THE ORIGINAL` remains a deliberate 1/1 outlier under the supplied 12/12 and global +2 rules. It must remain universally available in gameplay and must never be gated by NFT ownership. Ranked should either grant the same 1/1 pool to every player or restrict special 1/1 cards until human testing validates that power band.",
  ""
];

const reportName = candidateMode ? "BALANCE_CANDIDATE_REPORT.md" : "BALANCE_REPORT.md";
await writeFile(path.join(root, "docs", reportName), `${lines.join("\n")}\n`, "utf8");
console.log(`Simulated ${matchCount.toLocaleString("en-US")} matches; wrote docs/${reportName}.`);

function createGame(playerDeck, opponentDeck) {
  return {
    turn: 1,
    complete: false,
    result: "Draw",
    player: createPlayer(playerDeck),
    opponent: createPlayer(opponentDeck),
    board: Object.fromEntries(lanes.map((lane) => [lane, { player: [], opponent: [] }]))
  };
}

function createPlayer(deck) {
  return { deck: shuffle([...deck]), hand: [], energy: 0, returned: new Set(), included: new Set(deck.map((card) => card.id)), played: new Set() };
}

function randomDeck() {
  return shuffle([...cards]).slice(0, 12);
}

function startGame(game) {
  draw(game.player, 3);
  draw(game.opponent, 3);
  startTurn(game);
}

function startTurn(game) {
  for (const side of ["player", "opponent"]) {
    const owner = game[side];
    owner.energy = game.turn;
    draw(owner, 1);
    for (const laneName of lanes) {
      for (const card of game.board[laneName][side]) {
        if (card.protectedUntil >= 0 && card.protectedUntil < game.turn) {
          card.protected = false;
          card.protectedUntil = -1;
        }
        if (["green_skin", "second_hand_smoke_dawn"].includes(card.def.effectId)) card.power += 1;
        if (card.def.effectId === "chaos") applyChaos(game, owner, laneName, side, card);
      }
    }
  }
}

function playAiTurn(game, side) {
  const owner = game[side];
  for (let plays = 0; plays < 8; plays += 1) {
    const open = lanes.filter((lane) => game.board[lane][side].length < 4);
    if (!open.length) return;
    const playable = owner.hand
      .map((card, index) => ({ card, index, cost: effectiveCost(card) }))
      .filter((entry) => entry.cost <= owner.energy)
      .sort((a, b) => b.cost - a.cost || b.card.power - a.card.power);
    if (!playable.length) return;
    const choice = playable[0];
    const weighted = [];
    for (const lane of open) {
      const ownPower = lanePower(game, lane, side);
      const enemyPower = lanePower(game, lane, other(side));
      let weight = ownPower < enemyPower ? 4 : 1;
      if (ownPower === enemyPower) weight += 1;
      weight += Math.max(0, laneStrength(choice.card, lane) - choice.card.power);
      for (let i = 0; i < weight; i += 1) weighted.push(lane);
    }
    playCard(game, side, choice.index, weighted[Math.floor(random() * weighted.length)]);
  }
}

function playCard(game, side, handIndex, laneName) {
  const owner = game[side];
  const cardDef = owner.hand[handIndex];
  const cost = effectiveCost(cardDef);
  owner.hand.splice(handIndex, 1);
  owner.energy -= cost;
  owner.played.add(cardDef.id);
  const card = instance(cardDef, side);
  card.power = laneStrength(cardDef, laneName);
  game.board[laneName][side].push(card);
  applyOnPlay(game, owner, laneName, side, card);
}

function applyOnPlay(game, owner, laneName, side, card) {
  const friendly = game.board[laneName][side];
  const enemy = game.board[laneName][other(side)];
  const buff = (amount) => amount + (laneName === "Community" ? 1 : 0);
  switch (card.def.effectId) {
    case "tiger_shark_head": damage(game, laneName, card, lowest(enemy.filter((item) => !item.protected), "appreciation"), card.power); break;
    case "unicorn_head": {
      const index = friendly.indexOf(card);
      for (const targetIndex of [index - 1, index + 1]) if (friendly[targetIndex]) friendly[targetIndex].appreciation += buff(1);
      break;
    }
    case "alpha_kaiju_head": for (const ally of friendly) ally.power += buff(1); break;
    case "ghost_flame_background": card.protected = true; card.protectedUntil = game.turn; break;
    case "pink_lemonade_background": { const target = lowest(friendly, "appreciation"); if (target) target.appreciation += 2; break; }
    case "tropical_background": draw(owner, 1); break;
    case "overcast_background": { const target = highest(enemy.filter((item) => !item.protected), "power"); if (target) target.power -= 1; break; }
    case "second_hand_smoke_seafoam": for (const ally of friendly) ally.appreciation += buff(1); break;
    case "blue_skin": card.appreciation += 2; break;
    case "purple_skin": { const target = highest(enemy.filter((item) => !item.protected), "appreciation"); if (target) { card.appreciation += 1; damage(game, laneName, card, target, 1); } break; }
    case "pink_skin": if (hasFriendly(game, side, (item) => item !== card && item.def.type === "ORIGINAL")) card.power += 1; break;
    case "captain_fish_food": draw(owner, 1); break;
    case "the_original": forEachFriendly(game, side, (item) => { if (item.def.type === "ORIGINAL") item.power += buff(2); }); break;
    case "chaos": applyChaos(game, owner, laneName, side, card); break;
  }
}

function applyChaos(game, owner, laneName, side, card) {
  const roll = Math.floor(random() * 4);
  if (roll === 0) card.power += 1;
  else if (roll === 1) card.appreciation += 2;
  else if (roll === 2) draw(owner, 1);
  else for (const ally of game.board[laneName][side]) if (ally !== card) ally.power += 1 + (laneName === "Community" ? 1 : 0);
}

function damage(game, laneName, source, target, amount) {
  if (!target || target.protected) return;
  target.appreciation -= Math.max(0, amount);
  if (target.appreciation <= 0) defeat(game, laneName, source, target);
}

function defeat(game, laneName, source, target) {
  if (!target || target.protected) return;
  const owner = game[target.side];
  const lane = game.board[laneName][target.side];
  if (target.def.effectId === "decapitated_body" && !owner.returned.has(target.def.id)) {
    owner.returned.add(target.def.id);
    owner.hand.push(target.def);
  } else if (target.def.effectId === "yellow_skin") draw(owner, 1);
  lane.splice(lane.indexOf(target), 1);
  if (source?.def.effectId === "great_white_head") source.appreciation += 2;
}

function completeGame(game) {
  let playerLanes = 0;
  let opponentLanes = 0;
  for (const lane of lanes) {
    const player = lanePower(game, lane, "player");
    const opponent = lanePower(game, lane, "opponent");
    if (player > opponent) playerLanes += 1;
    else if (opponent > player) opponentLanes += 1;
  }
  game.result = playerLanes >= 2 ? "Victory" : opponentLanes >= 2 ? "Defeat" : "Draw";
  game.complete = true;
}

function lanePower(game, laneName, side) {
  const laneCards = game.board[laneName][side];
  return laneCards.reduce((sum, card) => sum + card.power, 0);
}

function laneStrength(card, laneName) {
  if (laneName === "Art") return Math.max(0, Number(card.artStrength ?? card.power));
  if (laneName === "Blockchain") return Math.max(0, Number(card.blockchainStrength ?? card.power));
  return Math.max(0, Number(card.communityStrength ?? card.power));
}

function record(player, score) {
  for (const id of player.included) { const stat = stats.get(id); stat.included += 1; stat.includeScore += score; }
  for (const id of player.played) { const stat = stats.get(id); stat.played += 1; stat.playScore += score; }
}

function draw(player, count) { for (let i = 0; i < count && player.deck.length; i += 1) player.hand.push(player.deck.shift()); }
function effectiveCost(card) { return Math.max(0, card.cost - (card.effectId === "no_head_body" ? 1 : 0)); }
function instance(def, side) { return { def, side, power: def.power, appreciation: def.appreciation, protected: false, protectedUntil: -1 }; }
function other(side) { return side === "player" ? "opponent" : "player"; }
function lowest(items, key) { return [...items].sort((a, b) => a[key] - b[key])[0]; }
function highest(items, key) { return [...items].sort((a, b) => b[key] - a[key])[0]; }
function forEachFriendly(game, side, action) { for (const lane of lanes) for (const card of game.board[lane][side]) action(card); }
function hasFriendly(game, side, predicate) { return lanes.some((lane) => game.board[lane][side].some(predicate)); }
function playRate(stat) { return stat.played ? stat.playScore / stat.played : 0; }
function percent(value) { return Number.isFinite(value) ? `${(value * 100).toFixed(1)}%` : "0.0%"; }
function shuffle(values) { for (let i = values.length - 1; i > 0; i -= 1) { const j = Math.floor(random() * (i + 1)); [values[i], values[j]] = [values[j], values[i]]; } return values; }
function mulberry32(seed) { return () => { seed |= 0; seed = seed + 0x6D2B79F5 | 0; let value = Math.imul(seed ^ seed >>> 15, 1 | seed); value = value + Math.imul(value ^ value >>> 7, 61 | value) ^ value; return ((value ^ value >>> 14) >>> 0) / 4294967296; }; }
