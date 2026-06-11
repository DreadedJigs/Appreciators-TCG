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
  "backend/start-backend-windows.cmd",
  "unity-client/Packages/manifest.json",
  "unity-client/ProjectSettings/EditorBuildSettings.asset",
  "unity-client/Assets/Resources/prototype-cards.json",
  "unity-client/Assets/Resources/app-config.json",
  "unity-client/Assets/Resources/Art/Cards/.gitkeep",
  "unity-client/Assets/Resources/Art/Placeholder/placeholder_original.png",
  "unity-client/Assets/Resources/Art/Placeholder/placeholder_companion.png",
  "unity-client/Assets/Resources/Art/Placeholder/placeholder_trait.png",
  "unity-client/Assets/Resources/Art/Placeholder/placeholder_background.png",
  "unity-client/Assets/Scripts/Battle/BattleGame.cs",
  "unity-client/Assets/Scripts/AI/SimpleAiPlayer.cs",
  "unity-client/Assets/Scripts/UI/MatchScreenController.cs",
  "unity-client/Assets/Scripts/Cards/CardArtResolver.cs",
  "unity-client/Assets/Editor/AppreciatorsBuildWebGL.cs",
  "unity-client/Assets/Editor/AppreciatorsPhase1Audit.cs",
  "unity-client/Assets/Tests/EditMode/BattleRulesEditModeTests.cs",
  "docs/DEBUG_AUDIT.md",
  "docs/ART_ASSET_PIPELINE.md",
  "docs/ART_ASSET_MANIFEST.csv"
];

const requiredScenes = [
  "Main",
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

assert.equal(backendCards.length, 29, "Approved Appreciators trait set needs 29 cards");
assert.equal(counts.ORIGINAL, 17, "Approved trait set needs 17 ORIGINALS");
assert.equal(counts.COMPANION, 5, "Approved trait set needs 5 COMPANIONS");
assert.equal(counts.ITEM, 7, "Approved trait set needs 7 ITEMS");
assert.equal(counts.EVENT || 0, 0, "Do not invent EVENT cards outside the approved list");

const backendSource = readFileSync(path.join(root, "backend/src/createApp.js"), "utf8");
for (const route of ["/health", "/api/profile", "/api/cards", "/api/matchmaking/casual", "/api/wallet/verify", "/api/nft/sync"]) {
  assert.ok(backendSource.includes(route), `Missing backend route ${route}`);
}
assert.ok(backendSource.includes("/api/assets/manifest"), "Missing backend route /api/assets/manifest");

for (const card of backendCards) {
  assert.equal(card.artKey, card.id, `${card.id} must have a stable artKey`);
  assert.equal(card.artPath, `Art/Cards/${card.id}`, `${card.id} must have a Unity Resources artPath`);
  assert.ok(Number.isInteger(card.appreciation), `${card.id} must have Appreciation`);
  assert.ok(["Common", "Rare", "Legendary", "1/1"].includes(card.rarity), `${card.id} must have approved rarity`);
  assert.ok(["ORIGINAL", "COMPANION", "ITEM", "EVENT"].includes(card.type), `${card.id} must have approved type`);
  assert.ok(!/dreaded ape/i.test(`${card.id} ${card.name} ${card.artPath}`), `${card.id} must not reference Dreaded Ape assets`);
}

const matchSource = [
  "unity-client/Assets/Scripts/Battle/BattleGame.cs",
  "unity-client/Assets/Scripts/Battle/LaneState.cs"
].map((file) => readFileSync(path.join(root, file), "utf8")).join("\n");
for (const rule of ["StartingHandSize", "CardsDrawnPerTurn", "MaxTurn", "MaxCardsPerLanePerPlayer"]) {
  assert.ok(matchSource.includes(rule), `Battle game is not wired to ${rule}`);
}

const buildSettings = readFileSync(path.join(root, "unity-client/ProjectSettings/EditorBuildSettings.asset"), "utf8");
assert.ok(buildSettings.includes("Assets/Scenes/Main.unity"), "Main runtime scene must be included in build settings");
assert.ok(buildSettings.indexOf("Assets/Scenes/Main.unity") < buildSettings.indexOf("Assets/Scenes/LoginScene.unity"), "Main runtime scene should be first in build settings");

const bootstrapperSource = readFileSync(path.join(root, "unity-client/Assets/Scripts/Core/SceneBootstrapper.cs"), "utf8");
assert.ok(bootstrapperSource.includes("case \"Main\":"), "SceneBootstrapper must route Main to the login screen");

console.log("Phase 1 audit passed.");
