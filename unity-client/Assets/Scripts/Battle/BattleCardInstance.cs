using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public class BattleCardInstance
    {
        private static int nextInstanceId = 1;

        public BattleCardInstance(CardDefinition definition, OwnerSide owner)
        {
            Definition = definition;
            Owner = owner;
            CurrentPower = definition.power;
            InstanceId = nextInstanceId++;
        }

        public int InstanceId { get; }
        public CardDefinition Definition { get; }
        public OwnerSide Owner { get; }
        public int CurrentPower { get; set; }
        public bool HasTrait { get; set; }
        public bool IsProtected { get; set; }
        public bool GoldTraitBonusApplied { get; set; }

        public string ShortLabel()
        {
            string traitMarker = HasTrait ? "*" : string.Empty;
            return $"{Definition.name}{traitMarker} ({CurrentPower})";
        }
    }
}
