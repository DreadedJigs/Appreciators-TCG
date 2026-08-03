using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.AI;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using NUnit.Framework;

namespace AppreciatorsTcg.Tests
{
    public class BattleSimulationEditModeTests
    {
        [Test]
        public void SeededAiVersusAiCompletesUsingProductionRules()
        {
            List<CardDefinition> playerDeck = CardCatalog.AllCards.Take(GameConstants.DeckSize).ToList();
            List<CardDefinition> opponentDeck = CardCatalog.AllCards.Skip(5).Take(GameConstants.DeckSize).ToList();
            BattleGame game = new BattleGame("Simulation A", playerDeck, opponentDeck, 20260621);
            Random random = new Random(20260621);
            game.Start();

            int safetyTurns = 0;
            while (!game.IsComplete && safetyTurns++ < GameConstants.MaxTurn + 5)
            {
                SimpleAiPlayer.PlayTurn(game, OwnerSide.Player, random);
                SimpleAiPlayer.PlayTurn(game, OwnerSide.Opponent, random);
                game.EndPlayerTurnOnly();
            }

            Assert.IsTrue(game.IsComplete,
                $"Simulation stalled on turn {game.Turn} during {game.Phase}; player committed={game.Player.HasCommittedCardThisTurn}, opponent committed={game.Opponent.HasCommittedCardThisTurn}, player hand={game.Player.Hand.Count}, opponent hand={game.Opponent.Hand.Count}.");
            Assert.LessOrEqual(game.Turn, GameConstants.MaxTurn);
            Assert.IsNotNull(MatchResultData.LastResult);
            Assert.AreEqual(1, MatchResultData.LastResult.laneScores.Length);
        }

        [Test]
        public void EveryApprovedCardHasPlayableDataAndKnownEffect()
        {
            HashSet<string> knownEffects = new HashSet<string>
            {
                "none",
                "great_white_head",
                "tiger_shark_head",
                "unicorn_head",
                "alpha_kaiju_head",
                "no_head_body",
                "decapitated_body",
                "blockchain_background",
                "ghost_flame_background",
                "pink_lemonade_background",
                "tropical_background",
                "overcast_background",
                "second_hand_smoke_dawn",
                "second_hand_smoke_seafoam",
                "green_skin",
                "blue_skin",
                "purple_skin",
                "pink_skin",
                "yellow_skin",
                "chaos",
                "captain_fish_food",
                "the_original"
            };

            foreach (CardDefinition card in CardCatalog.AllCards)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(card.id), "Card id is required.");
                Assert.GreaterOrEqual(card.GetAttack(), 0, $"{card.id} Attack must be non-negative.");
                Assert.Greater(card.GetDefense(), 0, $"{card.id} Defense must be positive.");
                Assert.IsTrue(CardArtResolver.HasFinalArt(card), $"{card.id} must use metadata-backed artwork instead of a retired card design.");
                Assert.IsTrue(knownEffects.Contains(card.effectId), $"Unknown effect {card.effectId} on {card.id}.");
            }
        }

        [Test]
        public void UnicornBuffsOnlyAdjacentCards()
        {
            BattleGame game = new BattleGame(
                "Adjacency Test",
                CardCatalog.GetCards(CardCatalog.StarterDeckIds()),
                CardCatalog.GetCards(CardCatalog.StarterDeckIds()),
                77);
            LaneState lane = game.MainLane;
            BattleCardInstance left = new BattleCardInstance(CardCatalog.GetCard("regular_body"), OwnerSide.Player);
            BattleCardInstance unicorn = new BattleCardInstance(CardCatalog.GetCard("unicorn_head"), OwnerSide.Player);
            BattleCardInstance right = new BattleCardInstance(CardCatalog.GetCard("blue_skin"), OwnerSide.Player);
            BattleCardInstance distant = new BattleCardInstance(CardCatalog.GetCard("pink_skin"), OwnerSide.Player);
            lane.PlayerCards.Add(left);
            lane.PlayerCards.Add(unicorn);
            lane.PlayerCards.Add(right);
            lane.PlayerCards.Add(distant);

            CardEffectResolver.ApplyOnPlay(game, game.Player, lane, unicorn, 0, 0);

            Assert.AreEqual(left.BaseAttack + 1, left.CurrentAttack);
            Assert.AreEqual(right.BaseAttack + 1, right.CurrentAttack);
            Assert.AreEqual(distant.BaseAttack, distant.CurrentAttack);
        }

        [Test]
        public void ChaosReceivesLegendaryRollWhenPlayed()
        {
            List<CardDefinition> deck = CardCatalog.AllCards.Take(GameConstants.DeckSize).ToList();
            BattleGame game = new BattleGame("Chaos Test", deck, deck, 91);
            LaneState lane = game.MainLane;
            BattleCardInstance ally = new BattleCardInstance(CardCatalog.GetCard("regular_body"), OwnerSide.Player);
            BattleCardInstance chaos = new BattleCardInstance(CardCatalog.GetCard("chaos"), OwnerSide.Player);
            lane.PlayerCards.Add(ally);
            lane.PlayerCards.Add(chaos);
            int before = chaos.CurrentPower + chaos.CurrentDefense + chaos.GrowthValue + ally.CurrentPower + game.Player.PendingAbilityGrowth + game.Player.PendingGrowthBonus;

            CardEffectResolver.ApplyOnPlay(game, game.Player, lane, chaos, 0, 0);

            int after = chaos.CurrentPower + chaos.CurrentDefense + chaos.GrowthValue + ally.CurrentPower + game.Player.PendingAbilityGrowth + game.Player.PendingGrowthBonus;
            Assert.Greater(after, before);
        }

        [Test]
        public void SecondPassEnergyCurveIsApplied()
        {
            Assert.AreEqual(1, CardCatalog.GetCard("tropical_background").cost);
            Assert.AreEqual(2, CardCatalog.GetCard("second_hand_smoke_dawn").cost);
            Assert.AreEqual(2, CardCatalog.GetCard("unicorn_head").cost);
            Assert.AreEqual(3, CardCatalog.GetCard("decapitated_body").cost);
            Assert.AreEqual(4, CardCatalog.GetCard("alpha_kaiju_head").cost);
        }
    }
}
