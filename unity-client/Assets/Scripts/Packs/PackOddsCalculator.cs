using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AppreciatorsTcg.Data;

namespace AppreciatorsTcg.Packs
{
    public static class PackOddsCalculator
    {
        public static string BuildOddsPreview(PackOddsResponse response, Lane attunement)
        {
            if (response?.slots == null || response.slots.Length == 0)
            {
                return "Server odds are unavailable for this pack.";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(response.packName);
            builder.AppendLine(response.attunementExplanation);
            if (response.purchasable)
            {
                builder.AppendLine($"Shard price: {response.shardCost:N0} | Final Mystery guarantee: {response.minimumMysteryRarity}+");
            }
            string shardOdds = string.Join(", ", (response.packShardOdds ?? Array.Empty<PackShardOddsEntry>())
                .Where(item => item != null)
                .Select(item => $"{item.shards} shards: {item.percent:0.#}%"));
            if (!string.IsNullOrWhiteSpace(shardOdds))
            {
                builder.AppendLine($"Pack shard reward: {shardOdds}");
            }
            if (response.starterRareOrBetterGuarantee)
            {
                builder.AppendLine("Starter guarantee: the Mystery Slot is always Rare or better.");
            }

            builder.AppendLine("Opening mode: Neutral. Lane selection is not part of the Appreciation Ritual.");
            builder.AppendLine();
            foreach (PackOddsSlot slot in response.slots.Where(item => item != null).OrderBy(item => item.slotIndex))
            {
                string percentages = string.Join(", ", (slot.rarityOdds ?? Array.Empty<PackOddsEntry>())
                    .Where(item => item != null)
                    .Select(item => $"{item.rarityLabel}: {item.percent:0.#}%"));
                builder.AppendLine($"Slot {slot.slotIndex}: {slot.label} - {percentages}");
            }

            builder.AppendLine();
            builder.AppendLine(response.complianceNotice);
            return builder.ToString();
        }

        public static string BuildOddsPreview(PackDefinition pack, Lane attunement)
        {
            if (pack == null)
            {
                return "Select a pack to preview odds.";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(pack.name);
            builder.AppendLine("Packs use earned Appreciation Shards only. If packs are ever sold for real money, displayed odds must be shown before purchase.");
            builder.AppendLine($"Neutral opening. Shard price: {(pack.purchasable ? pack.shardCost : 0):N0}. Lane selection is disabled.");
            builder.AppendLine();

            foreach (PackSlotDefinition slot in (pack.slots ?? Array.Empty<PackSlotDefinition>()).Where(item => item != null).OrderBy(item => item.slotIndex))
            {
                builder.Append($"Slot {slot.slotIndex}: {slot.label} - ");
                builder.AppendLine(string.Join(", ", Normalize(slot.rarityOdds).Select(item => $"{item.Key}: {item.Value:0.#}%")));
            }

            if (!string.IsNullOrWhiteSpace(pack.displayedOddsText))
            {
                builder.AppendLine();
                builder.AppendLine(pack.displayedOddsText);
            }

            return builder.ToString();
        }

        public static string FormatRarityDistribution(Dictionary<Rarity, int> counts, int cardsOpened)
        {
            if (counts == null || cardsOpened <= 0)
            {
                return "No rarity distribution yet.";
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"Simulated {cardsOpened} cards:");
            foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
            {
                counts.TryGetValue(rarity, out int count);
                float percent = cardsOpened == 0 ? 0 : (count / (float)cardsOpened) * 100f;
                builder.AppendLine($"{rarity}: {count} ({percent:0.0}%)");
            }

            return builder.ToString();
        }

        private static Dictionary<Rarity, float> Normalize(IEnumerable<RarityWeight> weights)
        {
            Dictionary<Rarity, float> result = new Dictionary<Rarity, float>();
            List<RarityWeight> validWeights = (weights ?? Array.Empty<RarityWeight>()).Where(item => item != null && item.weight > 0f).ToList();
            float total = validWeights.Sum(item => item.weight);

            if (total <= 0f)
            {
                return result;
            }

            foreach (RarityWeight weight in validWeights)
            {
                if (!result.ContainsKey(weight.rarity))
                {
                    result[weight.rarity] = 0f;
                }

                result[weight.rarity] += (weight.weight / total) * 100f;
            }

            return result;
        }
    }
}
