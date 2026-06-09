using System.Collections.Generic;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public class LaneState
    {
        public LaneState(LaneType lane)
        {
            Lane = lane;
        }

        public LaneType Lane { get; }
        public List<BattleCardInstance> PlayerCards { get; } = new List<BattleCardInstance>();
        public List<BattleCardInstance> OpponentCards { get; } = new List<BattleCardInstance>();

        public List<BattleCardInstance> GetCards(OwnerSide side)
        {
            return side == OwnerSide.Player ? PlayerCards : OpponentCards;
        }

        public bool HasSpace(OwnerSide side)
        {
            return GetCards(side).Count < GameConstants.MaxCardsPerLanePerPlayer;
        }
    }
}
