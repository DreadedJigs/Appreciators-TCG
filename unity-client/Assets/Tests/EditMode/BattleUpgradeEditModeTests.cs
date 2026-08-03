using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using NUnit.Framework;

namespace AppreciatorsTcg.Tests
{
    public class BattleUpgradeEditModeTests
    {
        [Test]
        public void EveryLegacyDomainMapsToTheSameTraditionalGrowthRow()
        {
            BattleGame game = CreateGame(CreateCard("balanced", 2, 4, 5));

            Assert.AreSame(game.MainLane, game.GetLane(LaneType.Art));
            Assert.AreSame(game.MainLane, game.GetLane(LaneType.Community));
            Assert.AreSame(game.MainLane, game.GetLane(LaneType.Blockchain));
            Assert.AreEqual(1, game.Lanes.Count);
            Assert.IsEmpty(game.LaneEvents);
        }

        [Test]
        public void BuildingDoesNotSpendAppreciation()
        {
            BattleGame game = CreateGame(CreateCard("engine", 6, 8, 8));
            game.Start();
            game.Player.Appreciation = 37;

            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out string message), message);

            Assert.AreEqual(37, game.Player.Appreciation);
            Assert.AreEqual(1, game.MainLane.PlayerCards.Count);
            Assert.AreEqual(1, game.Player.Hand.Count);
        }

        [Test]
        public void CommunityLeaderDevelopsMatchingPermanentsAndAddsTallyModifier()
        {
            CardDefinition card = CreateCard("community_focus", 1, 3, 5, "Community");
            BattleGame game = CreateGame(card);
            game.Start();
            Assert.AreEqual(LaneType.Community, game.Player.Leader.FocusLane);
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out _));
            BattleCardInstance built = game.MainLane.PlayerCards.Single();
            int growthBefore = built.GrowthValue;

            Assert.IsTrue(game.TryUseLeaderAbility(OwnerSide.Player, out string message), message);

            Assert.AreEqual(growthBefore + 1, built.GrowthValue);
            Assert.AreEqual(125, game.Player.TallyMultiplierPercent);
            Assert.IsFalse(game.TryUseLeaderAbility(OwnerSide.Player, out _));
        }

        [Test]
        public void FullGrowthRowStillAllowsActions()
        {
            CardDefinition card = CreateCard("full_row", 2, 3, 4);
            BattleGame game = CreateGame(card);
            game.Start();
            for (int i = 0; i < GameConstants.MaxCardsPerLanePerPlayer; i++)
            {
                game.MainLane.PlayerCards.Add(new BattleCardInstance(card, OwnerSide.Player));
            }

            Assert.IsFalse(game.TryBuildCard(OwnerSide.Player, 0, out _));
            Assert.IsTrue(game.TryDiscardCard(OwnerSide.Player, 0, out string actionMessage), actionMessage);
        }

        private static BattleGame CreateGame(CardDefinition card)
        {
            List<CardDefinition> deck = Enumerable.Range(0, GameConstants.DeckSize).Select(_ => card).ToList();
            return new BattleGame("Tester", deck, deck, 77);
        }

        private static CardDefinition CreateCard(string id, int cost, int power, int appreciation, string affinity = "Community")
        {
            return new CardDefinition
            {
                id = id,
                name = id.Replace('_', ' '),
                cost = cost,
                power = power,
                appreciation = appreciation,
                artStrength = affinity == "Art" ? power + 2 : power,
                blockchainStrength = affinity == "Blockchain" ? power + 2 : power,
                communityStrength = affinity == "Community" ? power + 2 : power,
                laneAffinity = affinity,
                traitGroup = "Test",
                type = GameConstants.Original,
                rarity = GameConstants.Common,
                effectId = "none",
                effectText = "Test card."
            };
        }
    }
}
