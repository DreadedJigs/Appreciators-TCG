import { readFile as readFileAsync } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const cardMetaPath = path.resolve(__dirname, "../data/card-meta-system.json");

let cachedSystem = null;

async function getSystem() {
  if (!cachedSystem) {
    cachedSystem = JSON.parse(await readFileAsync(cardMetaPath, "utf8"));
  }
  return cachedSystem;
}

function matches(value, expected) {
  return !expected || String(value || "").toLowerCase() === String(expected).toLowerCase();
}

function page(items, query = {}) {
  const offset = Math.max(0, Number.parseInt(query.offset, 10) || 0);
  const requestedLimit = Number.parseInt(query.limit, 10) || 100;
  const limit = Math.min(303, Math.max(1, requestedLimit));
  return {
    total: items.length,
    offset,
    limit,
    items: items.slice(offset, offset + limit)
  };
}

export async function getCardMetaSummary() {
  const system = await getSystem();
  const rarityCounts = Object.fromEntries(
    [...new Set(system.cards.map((card) => card.physicalRarity))]
      .map((rarity) => [rarity, system.cards.filter((card) => card.physicalRarity === rarity).length])
  );
  return {
    version: system.version,
    sourceWorkbook: system.sourceWorkbook,
    contract: system.contract,
    chain: system.chain,
    accuracyBoundary: system.accuracyBoundary,
    cards: system.cards.length,
    abilities: system.abilities.length,
    seasons: system.seasons.length,
    crowns: system.cards.filter((card) => card.physicalRarity === "Crown").length,
    rarityCounts,
    archetypes: system.archetypes,
    metadataMap: system.metadataMap
  };
}

export async function queryMetaCards(query = {}) {
  const system = await getSystem();
  const season = Number.parseInt(query.season, 10);
  const filtered = system.cards.filter((card) =>
    (!Number.isFinite(season) || card.season === season) &&
    matches(card.physicalRarity, query.rarity) &&
    matches(card.domain, query.domain) &&
    matches(card.pillar, query.pillar) &&
    matches(card.archetype, query.archetype)
  );
  return page(filtered, query);
}

export async function getMetaCard(tokenId) {
  const system = await getSystem();
  const normalizedId = Number.parseInt(tokenId, 10);
  return system.cards.find((card) => card.tokenId === normalizedId) || null;
}

export async function queryMetaAbilities(query = {}) {
  const system = await getSystem();
  const filtered = system.abilities.filter((ability) =>
    matches(ability.type, query.type) &&
    matches(ability.domain, query.domain) &&
    matches(ability.pillar, query.pillar) &&
    matches(ability.rarity, query.rarity)
  );
  return page(filtered, query);
}

export async function getMetaSeasons() {
  return (await getSystem()).seasons;
}
