using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public sealed class BattleCombatEvent
    {
        public LaneType Lane { get; set; }
        public OwnerSide Attacker { get; set; }
        public OwnerSide TargetOwner { get; set; }
        public int SourceInstanceId { get; set; }
        public int TargetInstanceId { get; set; }
        public string SourceCardName { get; set; }
        public string TargetCardName { get; set; }
        public int BaseDamage { get; set; }
        public int RallyBonus { get; set; }
        public int ShieldReduction { get; set; }
        public int Damage { get; set; }
        public bool TargetProtected { get; set; }
        public bool TargetDefeated { get; set; }
        public bool LaneBlocked { get; set; }
        public bool DirectAttack { get; set; }
        public bool Cancelled { get; set; }
        public bool Redirected { get; set; }
        public int DefenseBefore { get; set; }
        public int DefenseAfter { get; set; }
        public int HealthBefore { get; set; }
        public int HealthAfter { get; set; }

        public string Summary()
        {
            if (LaneBlocked)
            {
                return $"{Lane} lane is blocked this turn. Both sides committed 3 rally shards.";
            }

            if (DirectAttack)
            {
                return Cancelled
                    ? $"{SourceCardName}'s direct attack was cancelled."
                    : $"{SourceCardName} attacks {TargetCardName}: {HealthBefore} HP - {Damage} = {HealthAfter} HP.";
            }

            string rally = RallyBonus > 0 ? $" (+{RallyBonus} bonus)" : string.Empty;
            string shield = ShieldReduction > 0 ? $" (-{ShieldReduction} ward)" : string.Empty;
            string result = TargetProtected ? "blocked" : $"{Damage} damage";
            string calculation = TargetProtected || Cancelled ? string.Empty : $" ({DefenseBefore} Defense - {Damage} = {DefenseAfter})";
            return $"{SourceCardName} attacks {TargetCardName}: {result}{calculation}{rally}{shield}.";
        }
    }
}
