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
            Assert.AreEqual(30, CardCatalog.AllCards.Count);
            Assert.AreEqual(12, CardCatalog.AllCards.Count(card => card.type == GameConstants.Original));
            Assert.AreEqual(6, CardCatalog.AllCards.Count(card => card.type == GameConstants.Companion));
            Assert.AreEqual(6, CardCatalog.AllCards.Count(card => card.type == GameConstants.Trait));
            Assert.AreEqual(6, CardCatalog.AllCards.Count(card => card.type == GameConstants.Background));
        }

        [Test]
        public void StarterDeckIsValid()
        {
            Assert.IsTrue(PlayerDeckService.ValidateDeck(CardCatalog.StarterDeckIds()));
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
