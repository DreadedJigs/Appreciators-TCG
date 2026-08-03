using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppreciatorsTcg.Packs
{
    [Serializable]
    public class CardDefinition
    {
        public string id;
        public string name;
        public int attack;
        public int defense;
        public int cost;
        public int power;
        public int appreciation;
        public int artStrength;
        public int blockchainStrength;
        public int communityStrength;
        public Rarity rarity;
        public string rarityLabel;
        public Lane lane;
        public string laneLabel;
        public string type;
        public string cardType;
        public string effectText;
        public string description;
        public string flavorText;
        public string artKey;
        public string artPath;
        public string artReference;
        public bool isCollectible = true;
        public bool isActive = true;

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError("[PackOpening] Pack card definition is missing a stable id.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                Debug.LogError($"[PackOpening] Pack card '{id ?? "<unknown>"}' is missing a display name.");
            }

            if (!string.IsNullOrWhiteSpace(rarityLabel) && Enum.TryParse(rarityLabel, true, out Rarity parsedRarity))
            {
                rarity = parsedRarity;
            }
            else
            {
                rarityLabel = rarity.ToString();
            }

            if (!string.IsNullOrWhiteSpace(laneLabel) && Enum.TryParse(laneLabel, true, out Lane parsedLane))
            {
                lane = parsedLane;
            }
            else
            {
                laneLabel = lane.ToString();
            }

            cardType = string.IsNullOrWhiteSpace(cardType) ? type : cardType;
            type = string.IsNullOrWhiteSpace(type) ? cardType : type;
            description = string.IsNullOrWhiteSpace(description) ? effectText : description;
            effectText = string.IsNullOrWhiteSpace(effectText) ? description : effectText;
            artReference = string.IsNullOrWhiteSpace(artReference) ? EffectiveArtPath() : artReference;
            attack = GetAttack();
            defense = GetDefense();
            power = attack;
            appreciation = defense;
        }

        public int GetAttack()
        {
            return Math.Max(0, attack != 0 || power == 0 ? attack : power);
        }

        public int GetDefense()
        {
            return Math.Max(0, defense > 0 ? defense : appreciation);
        }

        public string EffectiveArtPath()
        {
            if (!string.IsNullOrWhiteSpace(artReference))
            {
                return artReference;
            }

            return string.IsNullOrWhiteSpace(artPath) ? $"Art/Cards/{id}" : artPath;
        }

        public int GetLaneStrength(Lane targetLane)
        {
            if (artStrength != 0 || blockchainStrength != 0 || communityStrength != 0)
            {
                switch (targetLane)
                {
                    case Lane.Art: return Math.Max(0, artStrength);
                    case Lane.Blockchain: return Math.Max(0, blockchainStrength);
                    case Lane.Community: return Math.Max(0, communityStrength);
                }
            }

            return Math.Max(0, GetAttack() + (lane == targetLane ? 2 : targetLane == Lane.Community ? 1 : 0));
        }

        public Lane StrongestLane()
        {
            Lane strongest = Lane.Art;
            int value = GetLaneStrength(strongest);
            foreach (Lane candidate in new[] { Lane.Blockchain, Lane.Community })
            {
                int candidateValue = GetLaneStrength(candidate);
                if (candidateValue > value)
                {
                    strongest = candidate;
                    value = candidateValue;
                }
            }

            return strongest;
        }
    }

    [Serializable]
    public class PackCardCollection
    {
        public List<CardDefinition> cards = new List<CardDefinition>();
    }
}
