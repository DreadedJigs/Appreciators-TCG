using System;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    [Serializable]
    public sealed class BattleReplayEvent
    {
        public int Turn;
        public BattleTurnPhase Phase;
        public OwnerSide Side;
        public string EventType;
        public string CardId;
        public string Decision;
        public string[] TargetIds = Array.Empty<string>();
        public int AppreciationChange;
        public int GrowthChange;
        public int DefenseBefore;
        public int DefenseAfter;
        public bool Revealed;
        public bool DirectAttack;
        public bool UsedAutoAttack;
        public string Summary;
    }
}
