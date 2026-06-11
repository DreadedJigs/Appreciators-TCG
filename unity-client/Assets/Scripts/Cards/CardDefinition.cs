using System;

namespace AppreciatorsTcg.Cards
{
    [Serializable]
    public class CardDefinition
    {
        public string id;
        public string name;
        public int cost;
        public int power;
        public string type;
        public string effectText;
        public string laneAffinity;
        public string effectId;
        public string artKey;
        public string artPath;

        public bool IsType(string cardType)
        {
            return string.Equals(type, cardType, StringComparison.OrdinalIgnoreCase);
        }

        public bool HasLaneAffinity(string laneName)
        {
            return string.Equals(laneAffinity, laneName, StringComparison.OrdinalIgnoreCase);
        }

        public string EffectiveArtPath()
        {
            return string.IsNullOrWhiteSpace(artPath) ? $"Art/Cards/{id}" : artPath;
        }
    }
}
