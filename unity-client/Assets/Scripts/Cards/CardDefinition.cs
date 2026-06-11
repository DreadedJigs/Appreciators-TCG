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
        public int appreciation;
        public string rarity;
        public string type;
        public string traitGroup;
        public string effectText;
        public string laneAffinity;
        public string effectId;
        public string[] tags;
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

        public bool HasTag(string tag)
        {
            if (tags == null)
            {
                return false;
            }

            foreach (string item in tags)
            {
                if (string.Equals(item, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public string EffectiveArtPath()
        {
            return string.IsNullOrWhiteSpace(artPath) ? $"Art/Cards/{id}" : artPath;
        }
    }
}
