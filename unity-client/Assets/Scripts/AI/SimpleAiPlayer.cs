using System;
using System.Linq;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.AI
{
    public static class SimpleAiPlayer
    {
        public static void PlayTurn(BattleGame game, Random random)
        {
            PlayTurn(game, OwnerSide.Opponent, random);
        }

        public static void PlayTurn(BattleGame game, OwnerSide side, Random random)
        {
            BattlePlayerState owner = game.GetPlayerState(side);
            if (game.IsComplete || owner.HasCommittedCardThisTurn || owner.CommitSkippedThisTurn || owner.Hand.Count == 0)
            {
                return;
            }

            bool rowFull = !game.MainLane.HasSpace(side);
            int buildIndex = Enumerable.Range(0, owner.Hand.Count)
                .OrderByDescending(index => BuildValue(game, side, owner.Hand[index]))
                .First();
            int discardIndex = Enumerable.Range(0, owner.Hand.Count)
                .Where(index => CardEffectResolver.CanResolveDiscard(game, owner, side, owner.Hand[index], out _))
                .OrderByDescending(index => DiscardValue(game, side, owner.Hand[index]))
                .DefaultIfEmpty(-1)
                .First();

            int buildValue = BuildValue(game, side, owner.Hand[buildIndex]);
            int discardValue = discardIndex < 0 ? int.MinValue : DiscardValue(game, side, owner.Hand[discardIndex]);
            bool canDiscard = discardIndex >= 0;
            bool canBuild = !rowFull;
            bool useDiscard = canDiscard && (!canBuild || discardValue > buildValue);

            if (useDiscard)
            {
                game.TryDiscardCard(side, discardIndex, out _);
            }
            else
            {
                game.TryBuildCard(side, buildIndex, out _);
            }

            if (!owner.HasCommittedCardThisTurn)
            {
                int fallbackDiscard = Enumerable.Range(0, owner.Hand.Count)
                    .Where(index => CardEffectResolver.CanResolveDiscard(game, owner, side, owner.Hand[index], out _))
                    .DefaultIfEmpty(-1)
                    .First();
                if (fallbackDiscard >= 0)
                {
                    game.TryDiscardCard(side, fallbackDiscard, out _);
                }
                else if (game.MainLane.HasSpace(side))
                {
                    game.TryBuildCard(side, buildIndex, out _);
                }
            }
        }

        private static int BuildValue(BattleGame game, OwnerSide side, CardDefinition card)
        {
            int value = card.GetBaseGrowth() * Math.Max(1, GameConstants.MaxTurn - game.Turn + 1);
            var row = game.MainLane.GetCards(side);
            if (row.Count > 0)
            {
                CardDefinition neighbor = row[row.Count - 1].Definition;
                if ((!string.IsNullOrWhiteSpace(card.traitGroup) &&
                     string.Equals(card.traitGroup, neighbor.traitGroup, StringComparison.OrdinalIgnoreCase)) ||
                    card.HasLaneAffinity(neighbor.laneAffinity))
                {
                    value += 4;
                }
            }

            if (card.effectId == "green_skin" || card.effectId == "second_hand_smoke_dawn")
            {
                value += Math.Max(0, GameConstants.MaxTurn - game.Turn);
            }

            return value;
        }

        private static int DiscardValue(BattleGame game, OwnerSide side, CardDefinition card)
        {
            BattlePlayerState owner = game.GetPlayerState(side);
            BattlePlayerState opponent = game.GetPlayerState(game.OppositeSide(side));
            int value = card.discardAiWeight + card.discardGrowthChange + card.discardAppreciationChange;
            int friendlyBoard = game.MainLane.GetCards(side).Count;
            int enemyBoard = game.MainLane.GetCards(game.OppositeSide(side)).Count;

            if (card.discardEffectId == "restore_defense" && game.MainLane.GetCards(side).Any(unit => unit.CurrentDefense < unit.BaseDefense)) value += 5;
            if ((card.discardEffectId == "remove_enemy" || card.discardEffectId == "disable_enemy") && enemyBoard > 0) value += 7;
            if (card.discardEffectId == "prevent_tally" && opponent.UnbankedGrowth > 0) value += 6;
            if (card.discardEffectId == "steal_growth" && opponent.UnbankedGrowth > 0) value += 4;
            if (card.discardEffectId == "extra_attack" && friendlyBoard > 0) value += 3;
            if (owner.Health <= 10 && card.discardEffectId == "cancel_attack") value += 8;
            if (owner.Appreciation > opponent.Appreciation && card.discardAppreciationChange < 0) value -= 4;

            int matchingPublicDiscards = owner.DiscardPile.Concat(opponent.DiscardPile)
                .Count(item => string.Equals(item.GetArchetype(), card.GetArchetype(), StringComparison.OrdinalIgnoreCase));
            int matchingCardsRemaining = owner.DrawPile.Count(item =>
                string.Equals(item.GetArchetype(), card.GetArchetype(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.GetPillar(), card.GetPillar(), StringComparison.OrdinalIgnoreCase));
            value += Math.Min(3, matchingPublicDiscards);
            if (matchingCardsRemaining >= 3) value -= 2;
            if (string.Equals(card.rarity, GameConstants.OneOfOne, StringComparison.OrdinalIgnoreCase)) value -= 5;

            // Discarding gives up permanent board value and can expose the player to direct attacks.
            value -= Math.Max(0, BuildValue(game, side, card) / 3);
            if (friendlyBoard <= 1) value -= 8;
            if (enemyBoard > friendlyBoard) value -= 4;
            return value;
        }

        private static int MainThreatScore(this BattlePlayerState state)
        {
            return state.Appreciation + state.UnbankedGrowth + state.Health;
        }
    }
}
