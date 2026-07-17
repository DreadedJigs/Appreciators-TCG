import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const currentDir = dirname(fileURLToPath(import.meta.url));
const cardsPath = resolve(currentDir, "../data/packs/cards.json");
const packsPath = resolve(currentDir, "../data/packs/packs.json");
const validRarities = new Set(["Common", "Uncommon", "Rare", "Epic", "Legendary"]);
const validLanes = new Set(["Art", "Community", "Blockchain", "Neutral"]);

let cachedCards;
let cachedPacks;

export function getPackCards() {
  if (!cachedCards) {
    cachedCards = readDataArray(cardsPath, "cards", "pack card").map((card) => ({
      ...card,
      cardType: card?.cardType || card?.type || "Card",
      description: card?.description || card?.effectText || "",
      artReference: card?.artReference || card?.artPath || card?.artKey || `Art/Cards/${card?.id || "missing"}`,
      isCollectible: card?.isCollectible !== false,
      isActive: card?.isActive !== false
    }));
    const cardIds = new Set();
    for (const [index, card] of cachedCards.entries()) {
      if (!card || !card.id || !card.name || !card.rarityLabel || !card.laneLabel) {
        throw serverDataError(`Pack card entry ${index} is missing id, name, rarityLabel, or laneLabel.`);
      }

      if (!validRarities.has(card.rarityLabel) || !validLanes.has(card.laneLabel)) {
        throw serverDataError(`Pack card '${card.id}' has an unknown rarity or lane label.`);
      }

      if (!card.isCollectible || !card.isActive) {
        throw serverDataError(`Pack card '${card.id}' must be active and collectible while it is in the reward pool.`);
      }

      if (cardIds.has(card.id)) {
        throw serverDataError(`Pack card id '${card.id}' is duplicated.`);
      }

      cardIds.add(card.id);
    }
  }

  return cachedCards;
}

export function getPackDefinitions() {
  if (!cachedPacks) {
    cachedPacks = readDataArray(packsPath, "packs", "pack definition").map((pack) => ({
      ...pack,
      displayName: pack?.displayName || pack?.name || "",
      packArtReference: pack?.packArtReference || `Art/Packs/${pack?.id || "missing"}`,
      attunementEnabled: pack?.attunementEnabled !== false,
      validAttunements: Array.isArray(pack?.validAttunements)
        ? pack.validAttunements
        : ["Art", "Community", "Blockchain"],
      isActive: pack?.isActive !== false,
      isTestPack: pack?.isTestPack !== false
    }));
    const packIds = new Set();
    for (const [index, pack] of cachedPacks.entries()) {
      if (!pack || !pack.id || !pack.name || !Array.isArray(pack.slots) || pack.slots.length !== 5) {
        throw serverDataError(`Pack definition entry ${index} must have id, name, and exactly five slots.`);
      }

      if (pack.slots.some((slot) => !slot || !slot.slotIndex || !slot.label || !Array.isArray(slot.rarityOdds) || slot.rarityOdds.length === 0)) {
        throw serverDataError(`Pack '${pack.id}' contains an invalid or empty reward slot.`);
      }

      if (packIds.has(pack.id)) {
        throw serverDataError(`Pack definition id '${pack.id}' is duplicated.`);
      }

      if (!pack.isActive) {
        throw serverDataError(`Pack '${pack.id}' is inactive but present in the active pack catalog.`);
      }

      if (pack.attunementEnabled && (pack.validAttunements.length === 0 || pack.validAttunements.some((lane) => !validLanes.has(lane) || lane === "Neutral"))) {
        throw serverDataError(`Pack '${pack.id}' contains invalid attunement lanes.`);
      }

      packIds.add(pack.id);
      const slotIndexes = new Set(pack.slots.map((slot) => slot.slotIndex));
      const mysterySlots = pack.slots.filter((slot) => slot.isMystery);
      if (slotIndexes.size !== 5 || [...slotIndexes].some((slotIndex) => slotIndex < 1 || slotIndex > 5)) {
        throw serverDataError(`Pack '${pack.id}' must use each slot index from 1 through 5 exactly once.`);
      }

      if (mysterySlots.length !== 1 || mysterySlots[0].slotIndex !== 5) {
        throw serverDataError(`Pack '${pack.id}' must have exactly one mystery reward in slot 5.`);
      }

      for (const slot of pack.slots) {
        if (slot.rarityOdds.some((odds) => !odds || !validRarities.has(odds.rarityLabel) || !Number.isFinite(Number(odds.weight)) || Number(odds.weight) <= 0)) {
          throw serverDataError(`Pack '${pack.id}' slot ${slot.slotIndex} has invalid rarity odds.`);
        }
      }
    }


    const cards = getPackCards();
    for (const pack of cachedPacks) {
      const mysterySlot = pack.slots.find((slot) => slot.isMystery && slot.slotIndex === 5);
      if (!mysterySlot || !pack.attunementEnabled) {
        continue;
      }

      for (const lane of pack.validAttunements) {
        for (const odds of mysterySlot.rarityOdds) {
          const hasAttunedCard = cards.some((card) => card.laneLabel === lane && card.rarityLabel === odds.rarityLabel);
          const hasMissCard = cards.some((card) => card.laneLabel !== lane && card.rarityLabel === odds.rarityLabel);
          if (!hasAttunedCard || !hasMissCard) {
            throw serverDataError(`Pack '${pack.id}' cannot support probabilistic ${lane} attunement at ${odds.rarityLabel} rarity.`);
          }
        }
      }
    }
  }

  return cachedPacks;
}

function readDataArray(path, property, label) {
  let parsed;
  try {
    parsed = JSON.parse(readFileSync(path, "utf8"));
  } catch (error) {
    throw serverDataError(`Unable to read ${label} data at ${path}: ${error.message}`);
  }

  if (!Array.isArray(parsed?.[property]) || parsed[property].length === 0) {
    throw serverDataError(`${label} data at ${path} contains no '${property}' entries.`);
  }

  return parsed[property];
}

function serverDataError(message) {
  return Object.assign(new Error(`[PackOpening] ${message}`), { statusCode: 500 });
}

export function getPackDefinition(packId) {
  const id = String(packId || "").trim();
  return getPackDefinitions().find((pack) => pack.id === id) || null;
}

export function getPublicPackCatalog() {
  return {
    version: "pack-catalog-v1",
    packs: getPackDefinitions(),
    cards: getPackCards()
  };
}
