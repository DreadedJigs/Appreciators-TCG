using System;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Cards
{
    [Serializable]
    public class CardDefinition
    {
        public string id;
        public string name;
        public int attack;
        public int defense;
        // Legacy values remain readable so existing collections and saved data migrate
        // without a conversion step. New card-facing rules use Attack and Defense only.
        public int cost;
        public int power;
        public int appreciation;
        public int artStrength;
        public int blockchainStrength;
        public int communityStrength;
        public string rarity;
        public string type;
        public string archetype;
        public string pillar;
        public string traitGroup;
        public string effectText;
        public string laneAffinity;
        public string effectId;
        public string buildEffect;
        public string discardEffect;
        public string discardEffectId;
        public string discardCategory;
        public string discardTargets;
        public int discardAppreciationChange;
        public int discardGrowthChange;
        public string discardBoardCost;
        public string discardBoardCostType;
        public int discardBoardCostAmount;
        public string discardCondition;
        public string discardEffectDuration;
        public string discardEffectSource;
        public int discardAiWeight;
        public string[] tags;
        public string artKey;
        public string artPath;
        public string discardShardLane;
        public int discardShardValue;

        public bool IsType(string cardType)
        {
            return string.Equals(type, cardType, StringComparison.OrdinalIgnoreCase);
        }

        public bool HasLaneAffinity(string laneName)
        {
            return string.Equals(laneAffinity, laneName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetLaneStrength(LaneType lane)
        {
            if (artStrength != 0 || blockchainStrength != 0 || communityStrength != 0)
            {
                switch (lane)
                {
                    case LaneType.Art: return Math.Max(0, artStrength);
                    case LaneType.Blockchain: return Math.Max(0, blockchainStrength);
                    case LaneType.Community: return Math.Max(0, communityStrength);
                }
            }

            int bonus = HasLaneAffinity(lane.ToString()) ? 2 : 0;
            if (bonus == 0 && lane == LaneType.Community && !string.IsNullOrWhiteSpace(laneAffinity))
            {
                bonus = 1;
            }

            return Math.Max(0, GetAttack() + bonus);
        }

        public int GetLanePowerBonus(LaneType lane)
        {
            return Math.Max(0, GetLaneStrength(lane) - GetAttack());
        }

        public int GetAttack()
        {
            return Math.Max(0, attack != 0 || power == 0 ? attack : power);
        }

        public int GetDefense()
        {
            return Math.Max(0, defense > 0 ? defense : appreciation);
        }

        public int GetBaseGrowth()
        {
            // Defense also paces the lasting value of a card built to the row.
            return Math.Max(1, (GetDefense() + 1) / 2);
        }

        public int GetDiscardGrowthValue()
        {
            if (discardGrowthChange != 0)
            {
                return discardGrowthChange;
            }

            return Math.Max(0, Math.Min(4, GetAttack() + 1));
        }

        public string GetBuildEffect()
        {
            return string.IsNullOrWhiteSpace(buildEffect) ? effectText : buildEffect;
        }

        public string GetDiscardEffect()
        {
            return string.IsNullOrWhiteSpace(discardEffect) ? "Reveal this card, then place it face-up in the discard pile." : discardEffect;
        }

        public string GetCardRulesText()
        {
            string build = string.IsNullOrWhiteSpace(GetBuildEffect()) ? "No additional effect." : GetBuildEffect().Trim();
            string discard = GetDiscardEffect().Trim();
            return $"BUILD: {build}\nDISCARD: {discard}";
        }

        public string GetDiscardCategory()
        {
            if (!string.IsNullOrWhiteSpace(discardCategory))
            {
                return discardCategory;
            }

            if (string.Equals(rarity, GameConstants.OneOfOne, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rarity, GameConstants.Crown, StringComparison.OrdinalIgnoreCase))
            {
                return "Dangerous Discard";
            }

            if (string.Equals(rarity, GameConstants.Common, StringComparison.OrdinalIgnoreCase))
            {
                return "Safe Discard";
            }

            return "Tactical Discard";
        }

        public bool IsHarmfulDiscard()
        {
            // The old game could punish a player for choosing Discard before
            // ending their turn. In the one-action Commit model, Discard is a
            // first-class action and never needs a warning or a penalty.
            return false;
        }

        public string GetDiscardConfirmation()
        {
            return string.Empty;
        }

        public string GetArchetype()
        {
            return string.IsNullOrWhiteSpace(archetype) ? "Original" : archetype;
        }

        public string GetPillar()
        {
            return string.IsNullOrWhiteSpace(pillar) ? "Build" : pillar;
        }

        public LaneType StrongestLane()
        {
            LaneType strongest = LaneType.Art;
            int strongestValue = GetLaneStrength(strongest);
            foreach (LaneType lane in new[] { LaneType.Blockchain, LaneType.Community })
            {
                int value = GetLaneStrength(lane);
                if (value > strongestValue)
                {
                    strongest = lane;
                    strongestValue = value;
                }
            }

            return strongest;
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

        public bool TryGetDiscardShard(out LaneType lane, out int amount)
        {
            amount = Math.Max(0, discardShardValue);
            if (amount <= 0 || !Enum.TryParse(discardShardLane, true, out lane))
            {
                lane = default;
                return false;
            }

            return lane == LaneType.Art || lane == LaneType.Blockchain;
        }
    }
}
