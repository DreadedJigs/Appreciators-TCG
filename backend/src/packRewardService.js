import crypto from "node:crypto";
import { getPackCards, getPackDefinition } from "./packRepository.js";

export const SHARD_VALUES = Object.freeze({
  Common: 5,
  Uncommon: 15,
  Rare: 50,
  Epic: 150,
  Legendary: 500
});

export const MYSTERY_ODDS = Object.freeze({
  standard: Object.freeze({ Common: 50, Uncommon: 30, Rare: 15, Epic: 4, Legendary: 1 }),
  starter: Object.freeze({ Rare: 85, Epic: 13, Legendary: 2 }),
  event: Object.freeze({ Common: 45, Uncommon: 30, Rare: 18, Epic: 6, Legendary: 1 }),
  guaranteed_uncommon: Object.freeze({ Uncommon: 70, Rare: 20, Epic: 8, Legendary: 2 }),
  guaranteed_rare: Object.freeze({ Rare: 82, Epic: 15, Legendary: 3 }),
  guaranteed_epic: Object.freeze({ Epic: 90, Legendary: 10 }),
  guaranteed_legendary: Object.freeze({ Legendary: 100 })
});

export const ATTUNEMENT_SHARD_COST = 50;
export const ATTUNEMENT_CHANCE_PERCENT = 65;
export const PACK_SHARD_ODDS = Object.freeze([
  Object.freeze({ shards: 100, weight: 40 }),
  Object.freeze({ shards: 125, weight: 35 }),
  Object.freeze({ shards: 150, weight: 20 }),
  Object.freeze({ shards: 300, weight: 5 })
]);

const VALID_ATTUNEMENTS = new Set(["Art", "Community", "Blockchain"]);

export function normalizeAttunement(value) {
  const requested = String(value || "").trim();
  if (!requested || requested.toLowerCase() === "neutral") {
    return "Neutral";
  }

  const normalized = [...VALID_ATTUNEMENTS].find((lane) => lane.toLowerCase() === requested.toLowerCase());
  if (!normalized) {
    throw Object.assign(new Error("Attunement must be Neutral, Art, Community, or Blockchain."), { statusCode: 400 });
  }

  return normalized;
}

export function getMysteryOdds(profile = "standard") {
  return MYSTERY_ODDS[String(profile || "standard").toLowerCase()] || MYSTERY_ODDS.standard;
}

export function getPackOdds(packId) {
  const pack = getPackDefinition(packId);
  if (!pack) {
    throw Object.assign(new Error("Unknown pack definition."), { statusCode: 404, errorCode: "PACK_NOT_FOUND" });
  }

  return {
    success: true,
    packId: pack.id,
    packName: pack.displayName || pack.name,
    description: pack.description,
    attunementEnabled: false,
    validAttunements: ["Neutral"],
    attunementShardCost: 0,
    attunementChancePercent: 0,
    attunementAffectsSlot: 0,
    shardCost: Number(pack.shardCost) || 0,
    purchasable: pack.purchasable === true,
    storeTierLabel: pack.storeTierLabel || "",
    minimumMysteryRarity: pack.minimumMysteryRarity || "",
    packShardOdds: PACK_SHARD_ODDS.map((entry) => ({
      shards: entry.shards,
      percent: entry.weight
    })),
    attunementExplanation: "All Appreciation Ritual packs open Neutral. Lane attunement is disabled; displayed rarity odds are the complete opening rules.",
    starterRareOrBetterGuarantee: pack.mysteryProfile === "starter",
    slots: [...pack.slots]
      .sort((left, right) => left.slotIndex - right.slotIndex)
      .map((slot) => ({
        slotIndex: slot.slotIndex,
        slotType: slot.slotType || inferSlotType(slot.slotIndex),
        label: slot.label,
        isLaneAttuned: false,
        isAttunementEligible: false,
        isMystery: Boolean(slot.isMystery),
        rarityOdds: oddsToPercentages(slot.isMystery
          ? oddsToWeights(getMysteryOdds(pack.mysteryProfile))
          : slot.rarityOdds)
      })),
    complianceNotice: "No paid transactions are enabled. If packs are ever sold for real money or premium currency, retain visible pre-purchase odds and complete platform, regional, age-rating, and legal review."
  };
}

export function generatePackReward({ packId, attunement, ownedCardIds = [], random = secureRandom }) {
  const pack = getPackDefinition(packId);
  if (!pack) {
    throw Object.assign(new Error("Unknown pack definition."), { statusCode: 404 });
  }

  const lane = normalizePackAttunement(pack, attunement);
  const cards = getPackCards();
  if (!Array.isArray(cards) || cards.length === 0) {
    throw Object.assign(new Error("[PackOpening] No card data is available for reward generation."), { statusCode: 500 });
  }

  if (!Array.isArray(pack.slots) || pack.slots.length !== 5) {
    throw Object.assign(new Error(`[PackOpening] Pack '${pack.id}' does not contain exactly five slots.`), { statusCode: 500 });
  }

  const owned = new Set(ownedCardIds);
  const rewards = [];
  let totalDuplicateShards = 0;
  let attunementSucceeded = false;

  for (const slot of [...pack.slots].sort((left, right) => left.slotIndex - right.slotIndex)) {
    const odds = slot.isMystery
      ? oddsToWeights(getMysteryOdds(pack.mysteryProfile))
      : slot.rarityOdds;
    const rarity = rollWeightedRarity(odds, random);
    let includeLane = null;
    let excludeLane = null;
    if (slot.isMystery && lane !== "Neutral") {
      attunementSucceeded = randomUnit(random) < ATTUNEMENT_CHANCE_PERCENT / 100;
      includeLane = attunementSucceeded ? lane : null;
      excludeLane = attunementSucceeded ? null : lane;
    }

    const card = selectCard(cards, rarity, { includeLane, excludeLane }, random);
    if (!card) {
      throw Object.assign(new Error(`No card is available for ${rarity}.`), { statusCode: 500 });
    }

    const isDuplicate = owned.has(card.id);
    const shardsAwarded = isDuplicate ? SHARD_VALUES[rarity] : 0;
    owned.add(card.id);
    totalDuplicateShards += shardsAwarded;
    rewards.push({
      slotIndex: slot.slotIndex,
      slotLabel: slot.label,
      isMysterySlot: Boolean(slot.isMystery),
      isDuplicate,
      shardsAwarded,
      card
    });
  }

  const packShardsAwarded = rollPackShards(random);

  return {
    rewardId: crypto.randomUUID(),
    packId: pack.id,
    packName: pack.name,
    attunement: lane,
    attunementLabel: lane,
    attunementChancePercent: lane === "Neutral" ? 0 : ATTUNEMENT_CHANCE_PERCENT,
    attunementSucceeded: lane !== "Neutral" && attunementSucceeded,
    attunementShardsSpent: lane === "Neutral" ? 0 : ATTUNEMENT_SHARD_COST,
    openedAtUtc: new Date().toISOString(),
    packShardsAwarded,
    totalDuplicateShards,
    totalShardsAwarded: packShardsAwarded + totalDuplicateShards,
    cards: rewards
  };
}

export function simulatePackOpenings({ packId, attunement, count = 100, random = secureRandom }) {
  const safeCount = Math.max(1, Math.min(10000, Number.parseInt(count, 10) || 100));
  const distribution = { Common: 0, Uncommon: 0, Rare: 0, Epic: 0, Legendary: 0 };
  const laneDistribution = { Art: 0, Community: 0, Blockchain: 0, Neutral: 0 };
  const ownedCardIds = new Set();
  let duplicateCount = 0;
  let totalShardsAwarded = 0;
  let attunementSuccessCount = 0;

  for (let index = 0; index < safeCount; index += 1) {
    const result = generatePackReward({ packId, attunement, ownedCardIds, random });
    for (const reward of result.cards) {
      distribution[reward.card.rarityLabel] += 1;
      laneDistribution[reward.card.laneLabel] += 1;
      duplicateCount += reward.isDuplicate ? 1 : 0;
      ownedCardIds.add(reward.card.id);
    }
    totalShardsAwarded += result.totalShardsAwarded;
    attunementSuccessCount += result.attunementSucceeded ? 1 : 0;
  }

  return {
    packId,
    attunement: normalizePackAttunement(getPackDefinition(packId), attunement),
    packCount: safeCount,
    cardsOpened: safeCount * 5,
    distribution,
    rarityDistribution: distribution,
    laneDistribution,
    duplicateCount,
    totalShardsAwarded,
    averageShardsPerPack: totalShardsAwarded / safeCount,
    attunementChancePercent: resultAttunementChance(packId, attunement),
    attunementSuccessCount
  };
}

function oddsToWeights(odds) {
  return Object.entries(odds).map(([rarityLabel, weight]) => ({ rarityLabel, weight }));
}

function rollWeightedRarity(weights = [], random) {
  const valid = weights.filter((item) => Number(item.weight) > 0);
  const total = valid.reduce((sum, item) => sum + Number(item.weight), 0);
  if (total <= 0) {
    throw Object.assign(new Error("Pack slot has no valid rarity odds."), { statusCode: 500 });
  }

  const roll = randomUnit(random) * total;
  let cumulative = 0;
  for (const item of valid) {
    cumulative += Number(item.weight);
    if (roll < cumulative) {
      return item.rarityLabel;
    }
  }

  return valid[valid.length - 1].rarityLabel;
}

function selectCard(cards, rarity, laneRule, random) {
  let candidates = cards.filter((card) => card && card.id && card.name && card.rarityLabel === rarity);
  if (laneRule?.includeLane) {
    candidates = candidates.filter((card) => card.laneLabel === laneRule.includeLane);
  } else if (laneRule?.excludeLane) {
    candidates = candidates.filter((card) => card.laneLabel !== laneRule.excludeLane);
  }

  if (candidates.length === 0) {
    const target = laneRule?.includeLane
      ? `${rarity} cards in the ${laneRule.includeLane} lane`
      : laneRule?.excludeLane
        ? `${rarity} cards outside the ${laneRule.excludeLane} lane`
        : `${rarity} cards`;
    throw Object.assign(new Error(`[PackOpening] No active collectible ${target} are configured.`), {
      statusCode: 500,
      errorCode: "PACK_CARD_POOL_INCOMPLETE"
    });
  }

  return candidates[Math.floor(randomUnit(random) * candidates.length)] || null;
}

export function normalizePackAttunement(pack, value) {
  if (!pack) {
    throw Object.assign(new Error("Unknown pack definition."), { statusCode: 404, errorCode: "PACK_NOT_FOUND" });
  }

  if (pack.attunementEnabled === false) {
    return "Neutral";
  }

  const lane = normalizeAttunement(value);
  if (lane === "Neutral") {
    return lane;
  }
  if (!validAttunementsForPack(pack).includes(lane)) {
    throw Object.assign(new Error(`Pack '${pack.id}' does not allow ${lane} Attunement.`), {
      statusCode: 400,
      errorCode: "INVALID_ATTUNEMENT"
    });
  }

  return lane;
}

function validAttunementsForPack(pack) {
  const configured = Array.isArray(pack?.validAttunements) ? pack.validAttunements : [...VALID_ATTUNEMENTS];
  return configured.filter((lane) => VALID_ATTUNEMENTS.has(lane));
}

function oddsToPercentages(weights = []) {
  const valid = weights.filter((item) => item && Number(item.weight) > 0);
  const total = valid.reduce((sum, item) => sum + Number(item.weight), 0);
  return valid.map((item) => ({
    rarityLabel: item.rarityLabel,
    percent: total > 0 ? (Number(item.weight) / total) * 100 : 0
  }));
}

function inferSlotType(slotIndex) {
  return ["", "CommonSlot", "CommonOrUncommonSlot", "UncommonOrRareSlot", "RandomLaneSlot", "MysterySlot"][slotIndex] || "MysterySlot";
}

function rollPackShards(random) {
  const total = PACK_SHARD_ODDS.reduce((sum, entry) => sum + entry.weight, 0);
  const roll = randomUnit(random) * total;
  let cumulative = 0;
  for (const entry of PACK_SHARD_ODDS) {
    cumulative += entry.weight;
    if (roll < cumulative) {
      return entry.shards;
    }
  }

  return PACK_SHARD_ODDS[PACK_SHARD_ODDS.length - 1].shards;
}

function resultAttunementChance(packId, attunement) {
  const pack = getPackDefinition(packId);
  return normalizePackAttunement(pack, attunement) === "Neutral" ? 0 : ATTUNEMENT_CHANCE_PERCENT;
}

function randomUnit(random) {
  const value = Number(random());
  if (!Number.isFinite(value) || value < 0 || value >= 1) {
    throw Object.assign(new Error("[PackOpening] Random source must return a value from 0 inclusive to 1 exclusive."), { statusCode: 500 });
  }

  return value;
}

function secureRandom() {
  return crypto.randomInt(0, 1_000_000) / 1_000_000;
}
