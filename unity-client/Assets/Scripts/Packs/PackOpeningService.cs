using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AppreciatorsTcg.Packs
{
    public class PackOpeningService
    {
        private const string CardsResourcePath = "PackData/sample_cards";
        private const string PacksResourcePath = "PackData/sample_packs";
        private const int AttunementShardCost = 50;
        private const int AttunementChancePercent = 65;

#if UNITY_EDITOR
        private readonly System.Random random;

        public PackOpeningService()
            : this(new System.Random())
        {
        }

        public PackOpeningService(System.Random random)
        {
            this.random = random ?? new System.Random();
        }
#else
        public PackOpeningService()
        {
        }
#endif

        public List<CardDefinition> LoadCardDefinitions()
        {
            TextAsset asset = Resources.Load<TextAsset>(CardsResourcePath);
            if (asset == null)
            {
                Debug.LogError($"Missing pack card data at Resources/{CardsResourcePath}.json");
                return new List<CardDefinition>();
            }

            PackCardCollection collection;
            try
            {
                collection = JsonUtility.FromJson<PackCardCollection>(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PackOpening] Could not parse Resources/{CardsResourcePath}.json: {exception.Message}");
                return new List<CardDefinition>();
            }

            if (collection?.cards == null || collection.cards.Count == 0)
            {
                Debug.LogError($"[PackOpening] Pack card data at Resources/{CardsResourcePath}.json contains no cards.");
                return new List<CardDefinition>();
            }

            List<CardDefinition> validCards = new List<CardDefinition>();
            for (int index = 0; index < collection.cards.Count; index++)
            {
                CardDefinition card = collection.cards[index];
                if (card == null)
                {
                    Debug.LogError($"[PackOpening] Pack card data contains a null entry at index {index}.");
                    continue;
                }

                card.Normalize();
                if (string.IsNullOrWhiteSpace(card.id) || string.IsNullOrWhiteSpace(card.name))
                {
                    Debug.LogError($"[PackOpening] Skipping invalid pack card entry at index {index}. Both id and name are required.");
                    continue;
                }

                validCards.Add(card);
            }

            if (validCards.Count == 0)
            {
                Debug.LogError("[PackOpening] No valid pack cards were loaded. Pack opening cannot continue.");
            }

            return validCards;
        }

        public List<PackDefinition> LoadPackDefinitions()
        {
            TextAsset asset = Resources.Load<TextAsset>(PacksResourcePath);
            if (asset == null)
            {
                Debug.LogError($"Missing pack data at Resources/{PacksResourcePath}.json");
                return new List<PackDefinition>();
            }

            PackDefinitionCollection collection;
            try
            {
                collection = JsonUtility.FromJson<PackDefinitionCollection>(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[PackOpening] Could not parse Resources/{PacksResourcePath}.json: {exception.Message}");
                return new List<PackDefinition>();
            }

            if (collection?.packs == null || collection.packs.Count == 0)
            {
                Debug.LogError($"[PackOpening] Pack data at Resources/{PacksResourcePath}.json contains no pack definitions.");
                return new List<PackDefinition>();
            }

            List<PackDefinition> validPacks = new List<PackDefinition>();
            for (int index = 0; index < collection.packs.Count; index++)
            {
                PackDefinition pack = collection.packs[index];
                if (pack == null)
                {
                    Debug.LogError($"[PackOpening] Pack data contains a null entry at index {index}.");
                    continue;
                }

                pack.Normalize();
                if (string.IsNullOrWhiteSpace(pack.id) || string.IsNullOrWhiteSpace(pack.name))
                {
                    Debug.LogError($"[PackOpening] Skipping pack definition at index {index}; id and name are required.");
                    continue;
                }

                if (pack.attunementEnabled && (pack.validAttunements == null || pack.validAttunements.Length == 0))
                {
                    Debug.LogError($"[PackOpening] Skipping pack '{pack.id}' because it enables attunement without valid lanes.");
                    continue;
                }

                if (pack.slots == null || pack.slots.Length != 5 || pack.slots.Any(slot => slot == null || slot.rarityOdds == null || slot.rarityOdds.Length == 0))
                {
                    Debug.LogError($"[PackOpening] Skipping pack '{pack.id}'; exactly five non-empty reward slots are required.");
                    continue;
                }

                validPacks.Add(pack);
            }

            if (validPacks.Count == 0)
            {
                Debug.LogError("[PackOpening] No valid pack definitions were loaded. The ritual UI will remain unavailable.");
            }

            return validPacks;
        }

#if UNITY_EDITOR
        // Editor-only deterministic tooling. Player builds receive signed results from the Render backend.
        public PackRewardResult OpenPack(PackDefinition pack, IReadOnlyList<CardDefinition> cardPool, Lane attunement, IEnumerable<string> ownedCardIds)
        {
            if (pack == null)
            {
                throw new ArgumentNullException(nameof(pack));
            }

            if (pack.slots == null || pack.slots.Length != 5 || pack.slots.Any(slot => slot == null))
            {
                throw new InvalidOperationException($"[PackOpening] Editor simulation pack '{pack.id ?? "<unknown>"}' must contain exactly five non-null slots.");
            }

            List<CardDefinition> cards = cardPool?.Where(card => card != null).ToList() ?? new List<CardDefinition>();
            if (cards.Count == 0)
            {
                throw new InvalidOperationException("[PackOpening] Editor simulation cannot generate rewards without valid card data.");
            }

            HashSet<string> owned = new HashSet<string>(ownedCardIds ?? Array.Empty<string>());
            Lane effectiveAttunement = pack.attunementEnabled ? attunement : Lane.Neutral;

            PackRewardResult result = new PackRewardResult
            {
                packId = pack.id,
                packName = pack.name,
                attunement = effectiveAttunement,
                attunementLabel = effectiveAttunement.ToString(),
                attunementChancePercent = effectiveAttunement == Lane.Neutral ? 0 : AttunementChancePercent,
                attunementShardsSpent = effectiveAttunement == Lane.Neutral ? 0 : AttunementShardCost,
                packShardsAwarded = RollPackShards(),
                openedAtUtc = DateTime.UtcNow.ToString("O")
            };

            foreach (PackSlotDefinition slot in pack.slots.OrderBy(item => item.slotIndex))
            {
                Rarity rarity = RollRarity(slot);
                bool applyMysteryAttunement = slot.isMystery && effectiveAttunement != Lane.Neutral;
                bool attunementSucceeded = applyMysteryAttunement && random.Next(100) < AttunementChancePercent;
                CardDefinition card = SelectCard(
                    cards,
                    rarity,
                    applyMysteryAttunement ? effectiveAttunement : Lane.Neutral,
                    applyMysteryAttunement && !attunementSucceeded);
                bool duplicate = card != null && owned.Contains(card.id);
                int shards = duplicate ? PackInventoryService.ShardsForRarity(card.rarity) : 0;

                if (applyMysteryAttunement)
                {
                    result.attunementSucceeded = attunementSucceeded;
                }

                if (card != null && !duplicate)
                {
                    owned.Add(card.id);
                }

                result.cards.Add(new PackRewardCardResult
                {
                    slotIndex = slot.slotIndex,
                    slotLabel = slot.label,
                    isMysterySlot = slot.isMystery,
                    isDuplicate = duplicate,
                    shardsAwarded = shards,
                    card = card
                });

                result.totalDuplicateShards += shards;
            }

            result.totalShardsAwarded = result.packShardsAwarded + result.totalDuplicateShards;

            return result;
        }

        public Dictionary<Rarity, int> SimulateOpenings(PackDefinition pack, IReadOnlyList<CardDefinition> cardPool, Lane attunement, int packCount)
        {
            Dictionary<Rarity, int> counts = new Dictionary<Rarity, int>();
            if (pack == null || packCount <= 0)
            {
                return counts;
            }

            for (int i = 0; i < packCount; i++)
            {
                PackRewardResult result = OpenPack(pack, cardPool, attunement, Array.Empty<string>());
                foreach (PackRewardCardResult reward in result.cards)
                {
                    if (reward.card == null)
                    {
                        continue;
                    }

                    if (!counts.ContainsKey(reward.card.rarity))
                    {
                        counts[reward.card.rarity] = 0;
                    }

                    counts[reward.card.rarity]++;
                }
            }

            return counts;
        }

        private Rarity RollRarity(PackSlotDefinition slot)
        {
            if (slot == null)
            {
                throw new InvalidOperationException("[PackOpening] Editor simulation encountered a null reward slot.");
            }

            List<RarityWeight> weights = (slot.rarityOdds ?? Array.Empty<RarityWeight>()).Where(item => item != null && item.weight > 0f).ToList();
            if (weights.Count == 0)
            {
                return Rarity.Common;
            }

            float total = weights.Sum(item => item.weight);
            double roll = random.NextDouble() * total;
            float cumulative = 0f;

            foreach (RarityWeight weight in weights)
            {
                cumulative += weight.weight;
                if (roll <= cumulative)
                {
                    return weight.rarity;
                }
            }

            return weights[weights.Count - 1].rarity;
        }

        private int RollPackShards()
        {
            int roll = random.Next(100);
            if (roll < 40) return 100;
            if (roll < 75) return 125;
            if (roll < 95) return 150;
            return 300;
        }

        private CardDefinition SelectCard(IReadOnlyList<CardDefinition> cards, Rarity rarity, Lane attunedLane, bool excludeAttunedLane)
        {
            List<CardDefinition> rarityMatches = cards.Where(card => card.rarity == rarity).ToList();
            if (rarityMatches.Count == 0)
            {
                Debug.LogError($"[PackOpening] Editor simulation has no card configured at {rarity} rarity.");
                return null;
            }

            if (attunedLane != Lane.Neutral)
            {
                List<CardDefinition> laneMatches = excludeAttunedLane
                    ? rarityMatches.Where(card => card.lane != attunedLane).ToList()
                    : rarityMatches.Where(card => card.lane == attunedLane).ToList();
                if (laneMatches.Count == 0)
                {
                    string laneRule = excludeAttunedLane ? $"outside {attunedLane}" : $"in {attunedLane}";
                    Debug.LogError($"[PackOpening] Editor simulation has no {rarity} card {laneRule} for the mystery attunement roll.");
                    return null;
                }

                return laneMatches[random.Next(laneMatches.Count)];
            }

            return rarityMatches.Count == 0 ? null : rarityMatches[random.Next(rarityMatches.Count)];
        }
#endif
    }
}
