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

            if (lane.Lane == LaneType.Art)
            {
                total += cards.Count(card => card.Definition.HasLaneAffinity("Art"));
            }

            if (lane.Lane == LaneType.Blockchain)
            {
                total += cards.Count(card => card.Definition.HasTag("Technology") || card.Definition.HasLaneAffinity("Blockchain")) * 2;
            }

            return total;
        }
    }
}
