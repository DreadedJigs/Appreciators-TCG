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
            CurrentAppreciation = definition.appreciation;
            InstanceId = nextInstanceId++;
        }

        public int InstanceId { get; }
        public CardDefinition Definition { get; }
        public OwnerSide Owner { get; }
        public int CurrentPower { get; set; }
        public int CurrentAppreciation { get; set; }
        public bool IsProtected { get; set; }
        public int ProtectedUntilTurn { get; set; } = -1;

        public string ShortLabel()
        {
            return $"{Definition.name} ({CurrentPower}/{CurrentAppreciation})";
        }
    }
}
