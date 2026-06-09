using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public static class BattleRules
    {
        public static int CalculateLanePower(LaneState lane, OwnerSide side, bool finalScore)
        {
            List<BattleCardInstance> cards = lane.GetCards(side);
            int total = cards.Sum(card => card.CurrentPower);

            if (lane.Lane == LaneType.Art && HasBackground(cards, "art_gallery"))
            {
                total += cards.Count(card => card.Definition.IsType(GameConstants.Original));
            }

            if (lane.Lane == LaneType.Community && HasBackground(cards, "community_rally"))
            {
                total += cards.Count(card => card.Definition.IsType(GameConstants.Companion));
            }

            if (lane.Lane == LaneType.Blockchain && HasBackground(cards, "mint_day"))
            {
                total += cards.Count;
            }

            int rareOriginals = cards.Count(card => card.Definition.effectId == "rare_original");
            int traitsInLane = cards.Count(card => card.Definition.IsType(GameConstants.Trait));
            total += rareOriginals * traitsInLane;

            if (lane.Lane == LaneType.Art && HasBackground(cards, "creator_studio"))
            {
                total += cards.Count(card => card.HasTrait) * 2;
            }

            if (finalScore)
            {
                int communitySpaces = cards.Count(card => card.Definition.effectId == "community_space");
                total += communitySpaces * cards.Count;
            }

            return total;
        }

        public static bool HasBackground(IEnumerable<BattleCardInstance> cards, string effectId)
        {
            return cards.Any(card => card.Definition.IsType(GameConstants.Background) && card.Definition.effectId == effectId);
        }
    }
}
