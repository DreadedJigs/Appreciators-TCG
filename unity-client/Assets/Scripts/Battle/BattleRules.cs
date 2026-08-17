using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public sealed class BattleGrowthPreview
    {
        public int BaseGrowth { get; set; }
        public int LinkGrowth { get; set; }
        public int UnityGrowth { get; set; }
        public int TotalGrowth => BaseGrowth + LinkGrowth + UnityGrowth;
        public int LinkCount => LinkGrowth / 2;
        public bool HasUnity => UnityGrowth > 0;
    }

    public static class BattleRules
    {
        public static int CalculateLanePower(LaneState lane, OwnerSide side, bool finalScore)
        {
            List<BattleCardInstance> cards = lane.GetCards(side);
            return cards.Sum(card => card.CurrentPower);
        }

        public static int CalculateBoardGrowth(LaneState lane, OwnerSide side)
        {
            return lane.GetCards(side).Where(card => !card.IsExhausted).Sum(card => card.GrowthValue);
        }

        public static int CalculateCombinationGrowth(IReadOnlyList<BattleCardInstance> cards)
        {
            return CalculateLinkGrowth(cards) + CalculateUnityGrowth(cards);
        }

        public static BattleGrowthPreview CreateGrowthPreview(IReadOnlyList<BattleCardInstance> cards)
        {
            if (cards == null)
            {
                return new BattleGrowthPreview();
            }

            return new BattleGrowthPreview
            {
                BaseGrowth = cards.Where(card => !card.IsExhausted).Sum(card => card.GrowthValue),
                LinkGrowth = CalculateLinkGrowth(cards),
                UnityGrowth = CalculateUnityGrowth(cards)
            };
        }

        // A Link is one fixed adjacency boundary. Even if two cards happen to
        // share several data traits, that pair grants only one +2 bonus.
        public static int CalculateLinkGrowth(IReadOnlyList<BattleCardInstance> cards)
        {
            if (cards == null || cards.Count == 0) return 0;
            int growth = 0;
            for (int i = 1; i < cards.Count; i++)
            {
                if (AreLinked(cards[i - 1], cards[i]))
                {
                    growth += 2;
                }
            }
            return growth;
        }

        public static bool AreLinked(BattleCardInstance left, BattleCardInstance right)
        {
            if (left == null || right == null) return false;
            CardDefinition leftDefinition = left.Definition;
            CardDefinition rightDefinition = right.Definition;
            bool sharedTrait = !string.IsNullOrWhiteSpace(leftDefinition.traitGroup) &&
                string.Equals(leftDefinition.traitGroup, rightDefinition.traitGroup, StringComparison.OrdinalIgnoreCase);
            bool sharedPillar = !string.IsNullOrWhiteSpace(leftDefinition.laneAffinity) &&
                string.Equals(leftDefinition.laneAffinity, rightDefinition.laneAffinity, StringComparison.OrdinalIgnoreCase);
            return sharedTrait || sharedPillar;
        }

        // Unity is a single +3 diversity reward for controlling Art,
        // Community, and Blockchain together. It never stacks per set.
        public static int CalculateUnityGrowth(IReadOnlyList<BattleCardInstance> cards)
        {
            if (cards == null || cards.Count == 0) return 0;
            int affinityCount = cards
                .Select(card => card.Definition.laneAffinity)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            return affinityCount >= 3 ? 3 : 0;
        }
    }
}
