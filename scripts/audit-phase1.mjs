import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

const root = process.cwd();
const requiredFiles = [
  "README.md",
  ".gitignore",
  "render.yaml",
  "backend/package.json",
  "backend/src/createApp.js",
  "backend/src/index.js",
  "backend/data/cards.json",
  "backend/test/cards.test.js",
  "backend/test/api.test.js",
  "unity-client/Packages/manifest.json",
  "unity-client/ProjectSettings/EditorBuildSettings.asset",
  "unity-client/Assets/Resources/prototype-cards.json",
  "unity-client/Assets/Resources/app-config.json",
  "unity-client/Assets/Scripts/Battle/BattleGame.cs",
  "unity-client/Assets/Scripts/AI/SimpleAiPlayer.cs",
  "unity-client/Assets/Scripts/UI/MatchScreenController.cs",
  "unity-client/Assets/Tests/EditMode/BattleRulesEditModeTests.cs",
  "docs/DEBUG_AUDIT.md"
];

const requiredScenes = [
  "LoginScene",
  "MainMenuScene",
  "CollectionScene",
  "DeckBuilderScene",
  "MatchScene",
  "ResultsScene",
  "Web3MockScene"
];

for (const file of requiredFiles) {
  assert.ok(existsSync(path.join(root, file)), `Missing ${file}`);
}

for (const scene of requiredScenes) {
  assert.ok(existsSync(path.join(root, `unity-client/Assets/Scenes/${scene}.unity`)), `Missing ${scene}.unity`);
}

const backendCards = JSON.parse(readFileSync(path.join(root, "backend/data/cards.json"), "utf8")).cards;
const unityCards = JSON.parse(readFileSync(path.join(root, "unity-client/Assets/Resources/prototype-cards.json"), "utf8")).cards;
assert.deepEqual(unityCards, backendCards, "Unity and backend card data must match");

const counts = backendCards.reduce((result, card) => {
  result[card.type] = (result[card.type] || 0) + 1;
  return result;
}, {});

assert.equal(backendCards.length, 30, "Phase 1 needs 30 prototype cards");
assert.equal(counts.ORIGINAL, 12, "Phase 1 needs 12 ORIGINALS");
assert.equal(counts.COMPANION, 6, "Phase 1 needs 6 COMPANIONS");
assert.equal(counts.TRAIT, 6, "Phase 1 needs 6 TRAITS");
assert.equal(counts.BACKGROUND, 6, "Phase 1 needs 6 BACKGROUNDS");

const backendSource = readFileSync(path.join(root, "backend/src/createApp.js"), "utf8");
for (const route of ["/health", "/api/profile", "/api/cards", "/api/matchmaking/casual", "/api/wallet/verify", "/api/nft/sync"]) {
  assert.ok(backendSource.includes(route), `Missing backend route ${route}`);
}

const matchSource = [
  "unity-client/Assets/Scripts/Battle/BattleGame.cs",
  "unity-client/Assets/Scripts/Battle/LaneState.cs"
].map((file) => readFileSync(path.join(root, file), "utf8")).join("\n");
for (const rule of ["StartingHandSize", "CardsDrawnPerTurn", "MaxTurn", "MaxCardsPerLanePerPlayer"]) {
  assert.ok(matchSource.includes(rule), `Battle game is not wired to ${rule}`);
}

console.log("Phase 1 audit passed.");
