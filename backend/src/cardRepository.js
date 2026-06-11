import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const cardsPath = path.resolve(__dirname, "../data/cards.json");

let cachedCards = null;

export async function getPrototypeCards() {
  if (!cachedCards) {
    const raw = await readFile(cardsPath, "utf8");
    cachedCards = JSON.parse(raw);
  }

  return cachedCards;
}

export async function getAssetManifest() {
  const prototypeCards = await getPrototypeCards();

  return {
    version: "phase-1",
    expectedUnityFolder: "unity-client/Assets/Resources/Art/Cards",
    placeholderUnityFolder: "unity-client/Assets/Resources/Art/Placeholder",
    filenamePattern: "{card_id}.png",
    cards: prototypeCards.cards.map((card) => ({
      id: card.id,
      name: card.name,
      type: card.type,
      traitGroup: card.traitGroup,
      rarity: card.rarity,
      power: card.power,
      appreciation: card.appreciation,
      laneAffinity: card.laneAffinity || "",
      artKey: card.artKey,
      artPath: card.artPath,
      expectedFile: `${card.id}.png`
    }))
  };
}
