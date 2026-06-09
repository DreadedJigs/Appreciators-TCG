using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public static class CardEffectResolver
    {
        public static void ApplyOnPlay(
            BattleGame game,
            BattlePlayerState owner,
            LaneState lane,
            BattleCardInstance card,
            int ownerPowerBefore,
            int opponentPowerBefore)
        {
            List<BattleCardInstance> friendly = lane.GetCards(card.Owner);

            switch (card.Definition.effectId)
            {
                case "blue_face_original":
                    if (lane.Lane == LaneType.Art)
                    {
                        card.CurrentPower += 1;
                    }
                    break;
                case "gallery_original":
                    if (lane.Lane == LaneType.Art)
                    {
                        card.CurrentPower += 2;
                    }
                    break;
                case "chain_original":
                    if (lane.Lane == LaneType.Blockchain)
                    {
                        card.CurrentPower += 2;
                    }
                    break;
                case "community_original":
                    BuffCard(FindLowestPower(friendly.Where(item => item != card)), 1, false);
                    break;
                case "rally_original":
                    if (lane.Lane == LaneType.Community)
                    {
                        owner.DrawCard();
                    }
                    break;
                case "on_chain_original":
                    owner.NextTraitCostReduction += 1;
                    break;
                case "be_original":
                    if (friendly.Count == 1)
                    {
                        card.CurrentPower += 3;
                    }
                    break;
                case "dreaded_original":
                    if (ownerPowerBefore > opponentPowerBefore)
                    {
                        card.CurrentPower += 2;
                    }
                    break;
                case "kaizo":
                    BuffCard(FindHighestPower(friendly.Where(item => item != card && item.Definition.IsType(GameConstants.Original))), 2, false);
                    break;
                case "spike":
                    if (ownerPowerBefore < opponentPowerBefore)
                    {
                        card.CurrentPower += 2;
                    }
                    break;
                case "community_pup":
                    BuffCard(FindLowestPower(friendly.Where(item => item != card)), 1, false);
                    break;
                case "chain_guardian":
                    if (lane.Lane == LaneType.Blockchain)
                    {
                        card.CurrentPower += 1;
                    }
                    break;
                case "gallery_scout":
                    if (lane.Lane == LaneType.Art)
                    {
                        card.CurrentPower += 2;
                    }
                    break;
                case "rally_beast":
                    foreach (BattleCardInstance companion in friendly.Where(item => item.Definition.IsType(GameConstants.Companion)))
                    {
                        companion.CurrentPower += 1;
                    }
                    break;
                case "gold_x_emblem":
                    AttachTrait(FindHighestPower(friendly.Where(item => item != card && item.Definition.IsType(GameConstants.Original))), 2);
                    break;
                case "astronaut_helmet":
                    BattleCardInstance helmetTarget = FindHighestPower(friendly.Where(item => item != card));
                    AttachTrait(helmetTarget, 1);
                    if (helmetTarget != null)
                    {
                        helmetTarget.IsProtected = true;
                    }
                    break;
                case "dread_trait":
                    AttachTrait(FindHighestPower(friendly.Where(item => item != card)), lane.Lane == LaneType.Community ? 2 : 1);
                    break;
                case "rare_eyes":
                    AttachTrait(FindHighestPower(friendly.Where(item => item != card)), 3);
                    break;
                case "chain_badge":
                    AttachTrait(FindHighestPower(friendly.Where(item => item != card)), lane.Lane == LaneType.Blockchain ? 3 : 1);
                    break;
                case "original_fit":
                    AttachTrait(FindHighestPower(friendly.Where(item => item != card && item.Definition.IsType(GameConstants.Original))), 4);
                    break;
            }
        }

        private static void AttachTrait(BattleCardInstance target, int power)
        {
            if (target == null)
            {
                return;
            }

            target.HasTrait = true;
            target.CurrentPower += power;

            if (target.Definition.effectId == "gold_x_original" && !target.GoldTraitBonusApplied)
            {
                target.CurrentPower += 2;
                target.GoldTraitBonusApplied = true;
            }
        }

        private static void BuffCard(BattleCardInstance target, int power, bool markTrait)
        {
            if (target == null)
            {
                return;
            }

            target.CurrentPower += power;
            if (markTrait)
            {
                target.HasTrait = true;
            }
        }

        private static BattleCardInstance FindHighestPower(IEnumerable<BattleCardInstance> cards)
        {
            return cards.OrderByDescending(card => card.CurrentPower).FirstOrDefault();
        }

        private static BattleCardInstance FindLowestPower(IEnumerable<BattleCardInstance> cards)
        {
            return cards.OrderBy(card => card.CurrentPower).FirstOrDefault();
        }
    }
}
