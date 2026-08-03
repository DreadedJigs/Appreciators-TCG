using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
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
            if (cards == null || cards.Count == 0)
            {
                return 0;
            }

            int growth = 0;
            for (int i = 1; i < cards.Count; i++)
            {
                CardDefinition left = cards[i - 1].Definition;
                CardDefinition right = cards[i].Definition;
                bool sharedTrait = !string.IsNullOrWhiteSpace(left.traitGroup) &&
                    string.Equals(left.traitGroup, right.traitGroup, StringComparison.OrdinalIgnoreCase);
                bool sharedAffinity = !string.IsNullOrWhiteSpace(left.laneAffinity) &&
                    string.Equals(left.laneAffinity, right.laneAffinity, StringComparison.OrdinalIgnoreCase);
                if (sharedTrait || sharedAffinity)
                {
                    growth += 2;
                }
            }

            int affinityCount = cards
                .Select(card => card.Definition.laneAffinity)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (affinityCount >= 3)
            {
                growth += 3;
            }

            return growth;
        }
    }
}
