using System;
using System.Collections.Generic;

namespace AppreciatorsTcg.Packs
{
    public enum PackSlotType
    {
        CommonSlot,
        CommonOrUncommonSlot,
        UncommonOrRareSlot,
        AttunedLaneSlot,
        MysterySlot,
        RandomLaneSlot
    }

    [Serializable]
    public class PackDefinition
    {
        public string id;
        public string name;
        public string displayName;
        public string description;
        public string packArtReference;
        public bool attunementEnabled = true;
        public string[] validAttunements = { "Art", "Community", "Blockchain" };
        public bool isActive = true;
        public bool isTestPack = true;
        public string mysteryProfile = "standard";
        public Lane featuredLane;
        public string featuredLaneLabel;
        public string displayedOddsText;
        public int shardCost;
        public bool purchasable;
        public string storeTierLabel;
        public string minimumMysteryRarity;
        public PackSlotDefinition[] slots;

        public void Normalize()
        {
            displayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;
            name = string.IsNullOrWhiteSpace(name) ? displayName : name;
            packArtReference = string.IsNullOrWhiteSpace(packArtReference) ? $"Art/Packs/{id}" : packArtReference;
            validAttunements = validAttunements ?? new[] { "Art", "Community", "Blockchain" };

            if (!string.IsNullOrWhiteSpace(featuredLaneLabel) && Enum.TryParse(featuredLaneLabel, true, out Lane parsedLane))
            {
                featuredLane = parsedLane;
            }
            else
            {
                featuredLaneLabel = featuredLane.ToString();
            }

            if (slots == null)
            {
                slots = new PackSlotDefinition[0];
            }

            foreach (PackSlotDefinition slot in slots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.Normalize();
            }
        }
    }

    [Serializable]
    public class PackSlotDefinition
    {
        public int slotIndex;
        public string label;
        public bool isLaneAttuned;
        public bool isMystery;
        public PackSlotType slotType;
        public string slotTypeLabel;
        public RarityWeight[] rarityOdds;

        public void Normalize()
        {
            if (!string.IsNullOrWhiteSpace(slotTypeLabel) && Enum.TryParse(slotTypeLabel, true, out PackSlotType parsedSlotType))
            {
                slotType = parsedSlotType;
            }
            else
            {
                slotType = InferSlotType(slotIndex);
                slotTypeLabel = slotType.ToString();
            }

            if (rarityOdds == null)
            {
                rarityOdds = new RarityWeight[0];
            }

            foreach (RarityWeight odds in rarityOdds)
            {
                if (odds != null)
                {
                    odds.Normalize();
                }
            }
        }

        private static PackSlotType InferSlotType(int index)
        {
            switch (index)
            {
                case 1: return PackSlotType.CommonSlot;
                case 2: return PackSlotType.CommonOrUncommonSlot;
                case 3: return PackSlotType.UncommonOrRareSlot;
                case 4: return PackSlotType.RandomLaneSlot;
                default: return PackSlotType.MysterySlot;
            }
        }
    }

    [Serializable]
    public class RarityWeight
    {
        public Rarity rarity;
        public string rarityLabel;
        public float weight;

        public void Normalize()
        {
            if (!string.IsNullOrWhiteSpace(rarityLabel) && Enum.TryParse(rarityLabel, true, out Rarity parsedRarity))
            {
                rarity = parsedRarity;
            }
            else
            {
                rarityLabel = rarity.ToString();
            }
        }
    }

    [Serializable]
    public class PackDefinitionCollection
    {
        public List<PackDefinition> packs = new List<PackDefinition>();
    }
}
