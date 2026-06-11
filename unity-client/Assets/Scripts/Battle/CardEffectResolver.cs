using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
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
            List<BattleCardInstance> enemy = lane.GetCards(game.OppositeSide(card.Owner));

            switch (card.Definition.effectId)
            {
                case "ghost_companion":
                    if (lane.Lane == LaneType.Blockchain)
                    {
                        card.CurrentPower += 2;
                    }
                    break;
                case "pigeon_companion":
                    game.DefeatCard(lane, card, FindLowestAppreciation(enemy.Where(item => item.Definition.rarity == GameConstants.Common && !item.IsProtected)));
                    break;
                case "cat_companion":
                    foreach (BattleCardInstance kaiju in game.AllLanes().SelectMany(item => item.GetCards(card.Owner)).Where(item => item.Definition.HasTag("Kaiju")))
                    {
                        kaiju.CurrentPower += AllyBuffAmount(lane, 2);
                    }
                    break;
                case "devil_dog_companion":
                    owner.DrawCard();
                    break;
                case "tiger_shark_head":
                    BattleCardInstance attackTarget = FindLowestAppreciation(enemy.Where(item => !item.IsProtected));
                    game.DealAppreciationDamage(lane, card, attackTarget, card.CurrentPower);
                    break;
                case "unicorn_head":
                    foreach (BattleCardInstance ally in friendly.Where(item => item != card))
                    {
                        ally.CurrentAppreciation += AllyBuffAmount(lane, 1);
                    }
                    break;
                case "alpha_kaiju_head":
                    foreach (BattleCardInstance ally in friendly)
                    {
                        ally.CurrentPower += AllyBuffAmount(lane, 1);
                    }
                    break;
                case "blockchain_background":
                    if (lane.Lane == LaneType.Blockchain)
                    {
                        card.CurrentPower += 2;
                    }
                    break;
                case "ghost_flame_background":
                    card.IsProtected = true;
                    card.ProtectedUntilTurn = game.Turn;
                    break;
                case "pink_lemonade_background":
                    BattleCardInstance healTarget = FindLowestAppreciation(friendly);
                    if (healTarget != null)
                    {
                        healTarget.CurrentAppreciation += 2;
                    }
                    break;
                case "tropical_background":
                    owner.DrawCard();
                    break;
                case "overcast_background":
                    DebuffPower(FindHighestPower(enemy.Where(item => !item.IsProtected)), 1);
                    break;
                case "second_hand_smoke_seafoam":
                    foreach (BattleCardInstance ally in friendly)
                    {
                        ally.CurrentAppreciation += AllyBuffAmount(lane, 1);
                    }
                    break;
                case "blue_skin":
                    card.CurrentAppreciation += 2;
                    break;
                case "purple_skin":
                    BattleCardInstance stealTarget = FindHighestAppreciation(enemy.Where(item => !item.IsProtected));
                    if (stealTarget != null)
                    {
                        card.CurrentAppreciation += 1;
                        game.DealAppreciationDamage(lane, card, stealTarget, 1);
                    }
                    break;
                case "pink_skin":
                    if (game.AllLanes().Any(item => item.GetCards(card.Owner).Any(ally => ally != card && ally.Definition.IsType(GameConstants.Original))))
                    {
                        card.CurrentPower += 1;
                    }
                    break;
                case "captain_fish_food":
                    game.TrySummonToken(card.Owner, lane, FishCompanionToken());
                    break;
                case "the_original":
                    foreach (BattleCardInstance original in game.AllLanes().SelectMany(item => item.GetCards(card.Owner)).Where(item => item.Definition.IsType(GameConstants.Original)))
                    {
                        original.CurrentPower += AllyBuffAmount(lane, 2);
                    }
                    break;
            }
        }

        public static void ApplyAfterCardPlayed(BattleGame game, OwnerSide playedSide, BattleCardInstance playedCard)
        {
            if (!playedCard.Definition.IsType(GameConstants.Companion))
            {
                return;
            }

            OwnerSide opponent = game.OppositeSide(playedSide);
            foreach (BattleCardInstance snake in game.AllLanes().SelectMany(lane => lane.GetCards(opponent)).Where(card => card.Definition.effectId == "snake_companion"))
            {
                snake.CurrentPower += 1;
            }
        }

        public static void ApplyStartOfTurn(BattleGame game, BattlePlayerState owner, OwnerSide side)
        {
            foreach (LaneState lane in game.AllLanes())
            {
                foreach (BattleCardInstance card in lane.GetCards(side))
                {
                    if (card.ProtectedUntilTurn >= 0 && card.ProtectedUntilTurn < game.Turn)
                    {
                        card.IsProtected = false;
                        card.ProtectedUntilTurn = -1;
                    }

                    switch (card.Definition.effectId)
                    {
                        case "beer_helmet":
                        case "green_skin":
                        case "second_hand_smoke_dawn":
                            card.CurrentPower += 1;
                            break;
                        case "chaos":
                            ApplyChaosRoll(game, owner, lane, card);
                            break;
                    }
                }
            }
        }

        private static void ApplyChaosRoll(BattleGame game, BattlePlayerState owner, LaneState lane, BattleCardInstance chaos)
        {
            switch (game.NextAbilityRoll(4))
            {
                case 0:
                    chaos.CurrentPower += 1;
                    break;
                case 1:
                    chaos.CurrentAppreciation += 2;
                    break;
                case 2:
                    owner.DrawCard();
                    break;
                default:
                    foreach (BattleCardInstance ally in lane.GetCards(chaos.Owner).Where(item => item != chaos))
                    {
                        ally.CurrentPower += AllyBuffAmount(lane, 1);
                    }
                    break;
            }
        }

        private static int AllyBuffAmount(LaneState lane, int baseAmount)
        {
            return lane.Lane == LaneType.Community ? baseAmount + 1 : baseAmount;
        }

        private static void DebuffPower(BattleCardInstance target, int power)
        {
            if (target == null)
            {
                return;
            }

            target.CurrentPower -= power;
        }

        private static BattleCardInstance FindHighestPower(IEnumerable<BattleCardInstance> cards)
        {
            return cards.OrderByDescending(card => card.CurrentPower).FirstOrDefault();
        }

        private static BattleCardInstance FindLowestAppreciation(IEnumerable<BattleCardInstance> cards)
        {
            return cards.OrderBy(card => card.CurrentAppreciation).FirstOrDefault();
        }

        private static BattleCardInstance FindHighestAppreciation(IEnumerable<BattleCardInstance> cards)
        {
            return cards.OrderByDescending(card => card.CurrentAppreciation).FirstOrDefault();
        }

        private static CardDefinition FishCompanionToken()
        {
            return new CardDefinition
            {
                id = "fish_companion_token",
                name = "Fish Companion",
                cost = 0,
                power = 2,
                appreciation = 2,
                rarity = GameConstants.Common,
                type = GameConstants.Companion,
                traitGroup = "Token",
                effectText = "Summoned by CAPTAIN FISH FOOD.",
                laneAffinity = "Community",
                effectId = "none",
                artKey = "fish_companion_token",
                artPath = "Art/Placeholder/placeholder_companion"
            };
        }
    }
}
