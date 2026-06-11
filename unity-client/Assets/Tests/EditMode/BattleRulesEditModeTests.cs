using System.Linq;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using NUnit.Framework;

namespace AppreciatorsTcg.Tests
{
    public class BattleRulesEditModeTests
    {
        [Test]
        public void PrototypeCardSetHasRequiredCounts()
        {
            Assert.AreEqual(29, CardCatalog.AllCards.Count);
            Assert.AreEqual(17, CardCatalog.AllCards.Count(card => card.type == GameConstants.Original));
            Assert.AreEqual(5, CardCatalog.AllCards.Count(card => card.type == GameConstants.Companion));
            Assert.AreEqual(7, CardCatalog.AllCards.Count(card => card.type == GameConstants.Item));
            Assert.AreEqual(0, CardCatalog.AllCards.Count(card => card.type == GameConstants.Event));
        }

        [Test]
        public void StarterDeckIsValid()
        {
            Assert.IsTrue(PlayerDeckService.ValidateDeck(CardCatalog.StarterDeckIds()));
        }

        [Test]
        public void CardsHaveFinalArtSlots()
        {
            foreach (var card in CardCatalog.AllCards)
            {
                Assert.AreEqual(card.id, card.artKey);
                Assert.AreEqual($"Art/Cards/{card.id}", card.EffectiveArtPath());
            }
        }

        [Test]
        public void BattleCompletesAfterSixTurns()
        {
            BattleGame game = new BattleGame("Tester", PlayerDeckService.LoadDeckOrStarter());
            game.Start();

            while (!game.IsComplete)
            {
                game.EndPlayerTurnAndRunAi();
            }

            Assert.AreEqual(GameConstants.MaxTurn, game.Turn);
            Assert.IsNotNull(MatchResultData.LastResult);
            Assert.AreEqual(3, MatchResultData.LastResult.laneScores.Length);
        }
    }
}
