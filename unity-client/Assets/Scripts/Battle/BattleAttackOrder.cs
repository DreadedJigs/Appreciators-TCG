using System;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    [Serializable]
    public sealed class BattleAttackOrder
    {
        public OwnerSide AttackerSide { get; set; }
        public int SourceInstanceId { get; set; }
        public int TargetInstanceId { get; set; }
        public bool TargetsPlayer => TargetInstanceId <= 0;

        public BattleAttackOrder Clone()
        {
            return new BattleAttackOrder
            {
                AttackerSide = AttackerSide,
                SourceInstanceId = SourceInstanceId,
                TargetInstanceId = TargetInstanceId
            };
        }
    }
}
