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
