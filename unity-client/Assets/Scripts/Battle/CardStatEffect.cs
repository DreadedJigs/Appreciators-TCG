using System;

namespace AppreciatorsTcg.Battle
{
    [Serializable]
    public sealed class CardStatEffect
    {
        public string Source { get; set; }
        public int AttackDelta { get; set; }
        public int DefenseDelta { get; set; }
        public bool IsTemporary { get; set; }
        public int AppliedTurn { get; set; }
        public string Duration { get; set; }

        public string Summary()
        {
            string attack = AttackDelta == 0 ? string.Empty : $"{(AttackDelta > 0 ? "+" : string.Empty)}{AttackDelta} Attack";
            string defense = DefenseDelta == 0 ? string.Empty : $"{(DefenseDelta > 0 ? "+" : string.Empty)}{DefenseDelta} Defense";
            string separator = attack.Length > 0 && defense.Length > 0 ? ", " : string.Empty;
            string duration = string.IsNullOrWhiteSpace(Duration) ? (IsTemporary ? " until Growth resolves" : " permanent") : $" {Duration}";
            return $"{attack}{separator}{defense} from {Source}{duration}";
        }
    }
}
