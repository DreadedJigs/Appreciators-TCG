using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using NUnit.Framework;

namespace AppreciatorsTcg.Tests
{
    public sealed class RevampRulesEditModeTests
    {
        [Test]
        public void RoundRecordsOnlyDrawCommitBattleAndAppreciate()
        {
            BattleGame game = CreateGame();
            game.Start();
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out _));
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Opponent, 0, out _));
            game.ResolveGrowthAndAdvanceTurn();

            BattleTurnPhase[] required =
            {
                BattleTurnPhase.Draw, BattleTurnPhase.Commit, BattleTurnPhase.Battle,
                BattleTurnPhase.Appreciate, BattleTurnPhase.Draw, BattleTurnPhase.Commit
            };
            int cursor = 0;
            foreach (BattleTurnPhase phase in game.PhaseHistory)
                if (cursor < required.Length && phase == required[cursor]) cursor++;
            Assert.AreEqual(required.Length, cursor, string.Join(", ", game.PhaseHistory));
        }

        [Test]
        public void UnusedCardClearsWithoutResolvingItsDiscardAbility()
        {
            BattleGame game = CreateGame();
            game.Start();
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out _));
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Opponent, 0, out _));
            CardDefinition playerRemaining = game.Player.Hand.Single();
            CardDefinition opponentRemaining = game.Opponent.Hand.Single();
            game.BeginEndTurnPhase();
            game.ResolveForcedDiscardPhase();

            Assert.AreEqual(BattleTurnPhase.Commit, game.Phase);
            Assert.IsEmpty(game.Player.Hand);
            Assert.IsEmpty(game.Opponent.Hand);
            Assert.Contains(playerRemaining, game.Player.DiscardPile);
            Assert.Contains(opponentRemaining, game.Opponent.DiscardPile);
            Assert.IsTrue(game.ReplayEvents.Any(entry => entry.EventType == "unused-card-cleared" && entry.Side == OwnerSide.Player));
            Assert.IsTrue(game.ReplayEvents.Any(entry => entry.EventType == "unused-card-cleared" && entry.Side == OwnerSide.Opponent));
            Assert.IsTrue(game.ResolveCombatPlans(new BattleAttackOrder[0], new BattleAttackOrder[0], out _));
            Assert.AreEqual(BattleTurnPhase.Battle, game.Phase);
        }

        [Test]
        public void FullBattlefieldCanRebuildByReplacingOneFriendlyCard()
        {
            BattleGame game = CreateGame();
            game.Start();
            for (int i = 0; i < GameConstants.MaxCardsPerLanePerPlayer; i++)
            {
                AddUnit(game, OwnerSide.Player, "regular_body");
            }
            CardDefinition replacement = game.Player.Hand[0];
            BattleCardInstance replaced = game.MainLane.PlayerCards[2];

            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, replaced.InstanceId, out string message), message);
            Assert.AreEqual(GameConstants.MaxCardsPerLanePerPlayer, game.MainLane.PlayerCards.Count);
            Assert.IsFalse(game.MainLane.PlayerCards.Contains(replaced));
            Assert.Contains(replaced.Definition, game.Player.DiscardPile);
            Assert.IsTrue(game.MainLane.PlayerCards.Any(card => card.Definition == replacement));
        }

        [Test]
        public void LinksAndUnityAreOneClearlyBoundedScoringSystem()
        {
            BattleGame game = CreateGame();
            AddUnit(game, OwnerSide.Player, "regular_body");
            AddUnit(game, OwnerSide.Player, "unicorn_head");
            AddUnit(game, OwnerSide.Player, "blue_skin");
            BattleGrowthPreview preview = game.PreviewAppreciation(OwnerSide.Player);

            Assert.AreEqual(BattleRules.CalculateLinkGrowth(game.MainLane.PlayerCards), preview.LinkGrowth);
            Assert.AreEqual(BattleRules.CalculateUnityGrowth(game.MainLane.PlayerCards), preview.UnityGrowth);
            Assert.AreEqual(preview.BaseGrowth + preview.LinkGrowth + preview.UnityGrowth, preview.TotalGrowth);
        }

        [Test]
        public void DiscardPhaseClearsOverflowCardsFromBothHands()
        {
            BattleGame game = CreateGame();
            game.Start();
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out _));
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Opponent, 0, out _));
            game.Player.Hand.Add(CardCatalog.GetCard("regular_body"));
            game.Opponent.Hand.Add(CardCatalog.GetCard("blue_skin"));

            game.BeginEndTurnPhase();
            game.ResolveForcedDiscardPhase();

            Assert.IsEmpty(game.Player.Hand);
            Assert.IsEmpty(game.Opponent.Hand);
            Assert.GreaterOrEqual(game.Player.DiscardPile.Count, 2);
            Assert.GreaterOrEqual(game.Opponent.DiscardPile.Count, 2);
        }

        [Test]
        public void CardRulesTextAlwaysLabelsBuildAndDiscardAbilities()
        {
            CardDefinition card = CardCatalog.GetCard("regular_body");
            string rules = card.GetCardRulesText();

            StringAssert.Contains("BUILD:", rules);
            StringAssert.Contains(card.GetBuildEffect(), rules);
            StringAssert.Contains("DISCARD:", rules);
            StringAssert.Contains(card.GetDiscardEffect(), rules);
        }

        [Test]
        public void AiCommitsBeforePlayerCombatCanResolve()
        {
            BattleGame game = CreateGame();
            game.Start();
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out _));
            Assert.IsFalse(game.ResolveCombatPlans(new BattleAttackOrder[0], new BattleAttackOrder[0], out string blocked));
            StringAssert.Contains("Each player", blocked);

            game.RunAiTurn();

            Assert.IsTrue(game.Opponent.HasCommittedCardThisTurn);
            Assert.IsTrue(game.ResolveCombatPlans(new BattleAttackOrder[0], new BattleAttackOrder[0], out _));
        }

        [Test]
        public void CostlyDiscardRequiresAndPaysItsBoardConsequence()
        {
            BattleGame game = CreateGame();
            CardDefinition discard = CardCatalog.GetCard("great_white_head");
            Assert.IsFalse(CardEffectResolver.CanResolveDiscard(game, game.Player, OwnerSide.Player, discard, out string blocked));
            StringAssert.Contains("Exhaust one allied unit", blocked);

            BattleCardInstance ally = AddUnit(game, OwnerSide.Player, "regular_body");
            Assert.IsTrue(CardEffectResolver.CanResolveDiscard(game, game.Player, OwnerSide.Player, discard, out _));
            CardEffectResolver.PayDiscardBoardCost(game, game.Player, OwnerSide.Player, discard);
            Assert.IsTrue(ally.IsExhausted);
        }

        [Test]
        public void EmptyBoardDirectAttackReducesHpImmediately()
        {
            BattleGame game = CreateGame();
            BattleCardInstance attacker = AddUnit(game, OwnerSide.Player, "regular_body");
            CommitBoth(game);
            BattleAttackOrder direct = new BattleAttackOrder { AttackerSide = OwnerSide.Player, SourceInstanceId = attacker.InstanceId, TargetInstanceId = 0 };

            Assert.IsTrue(game.ResolveCombatPlans(new[] { direct }, new BattleAttackOrder[0], out string message), message);
            Assert.AreEqual(GameConstants.StartingHealth - attacker.CurrentAttack, game.Opponent.Health);
            Assert.IsTrue(game.LastCombatEvents.Single().DirectAttack);
        }

        [Test]
        public void ManualTargetingRejectsDirectAttackWhileDefenderExists()
        {
            BattleGame game = CreateGame();
            BattleCardInstance attacker = AddUnit(game, OwnerSide.Player, "regular_body");
            BattleCardInstance defender = AddUnit(game, OwnerSide.Opponent, "white_skin");
            BattleAttackOrder illegal = new BattleAttackOrder { AttackerSide = OwnerSide.Player, SourceInstanceId = attacker.InstanceId, TargetInstanceId = 0 };
            Assert.IsFalse(game.ValidateAttackPlan(OwnerSide.Player, new[] { illegal }, out string message));
            StringAssert.Contains("no eligible defenders", message);

            BattleAttackOrder legal = new BattleAttackOrder { AttackerSide = OwnerSide.Player, SourceInstanceId = attacker.InstanceId, TargetInstanceId = defender.InstanceId };
            Assert.IsTrue(game.ValidateAttackPlan(OwnerSide.Player, new[] { legal }, out _));
        }

        [Test]
        public void AutoAttackIsUnavailableInCompetitiveMode()
        {
            List<CardDefinition> deck = CardCatalog.GetCards(CardCatalog.StarterDeckIds());
            BattleGame game = new BattleGame("Ranked", deck, deck, 19, true);
            AddUnit(game, OwnerSide.Player, "regular_body");
            Assert.IsFalse(game.CanUseAutoAttack);
            Assert.IsEmpty(game.BuildAutoAttackPlan(OwnerSide.Player));
            StringAssert.Contains("competitive", game.AutoAttackRestriction);
        }

        [Test]
        public void DefenseDamagePersistsThroughRefresh()
        {
            BattleGame game = CreateGame();
            BattleCardInstance attacker = AddUnit(game, OwnerSide.Player, "regular_body");
            CardDefinition wall = new CardDefinition { id = "wall", name = "Wall", attack = 0, defense = 9, rarity = GameConstants.Common, type = GameConstants.Original };
            BattleCardInstance defender = new BattleCardInstance(wall, OwnerSide.Opponent);
            defender.PlaceInGrowthRow();
            game.MainLane.OpponentCards.Add(defender);
            CommitBoth(game);
            BattleAttackOrder order = new BattleAttackOrder { AttackerSide = OwnerSide.Player, SourceInstanceId = attacker.InstanceId, TargetInstanceId = defender.InstanceId };
            Assert.IsTrue(game.ResolveCombatPlans(new[] { order }, new BattleAttackOrder[0], out _));
            int damagedDefense = defender.CurrentDefense;
            Assert.AreEqual(9 - attacker.CurrentAttack, damagedDefense);
            game.ResolveRefresh();
            Assert.AreEqual(damagedDefense, defender.CurrentDefense);
        }

        [Test]
        public void BuffsAndNerfsTrackBaseCurrentSourceDurationAndExpiry()
        {
            BattleCardInstance unit = new BattleCardInstance(CardCatalog.GetCard("regular_body"), OwnerSide.Player);
            unit.ApplyStatEffect("Community Support", 1, 1, false, 1);
            unit.ApplyStatEffect("Sabotage", 3, -2, true, 1);
            Assert.AreEqual(unit.BaseAttack + 4, unit.CurrentAttack);
            Assert.AreEqual(unit.BaseDefense - 1, unit.CurrentDefense);
            Assert.AreEqual(3, unit.TemporaryAttackBonus);
            Assert.AreEqual(1, unit.PermanentDefenseBonus);
            StringAssert.Contains("Sabotage", unit.EffectSummary());
            StringAssert.Contains("until Growth", unit.EffectSummary());
            unit.Refresh();
            Assert.AreEqual(unit.BaseAttack + 1, unit.CurrentAttack);
            Assert.AreEqual(unit.BaseDefense + 1, unit.CurrentDefense);
        }

        [Test]
        public void RevealedOpponentCardRemainsPublicUntilItChangesZones()
        {
            BattleGame game = CreateGame();
            CardDefinition hidden = CardCatalog.GetCard("white_skin");
            game.Opponent.Hand.Add(hidden);
            CardEffectResolver.ResolveDiscard(game, game.Player, OwnerSide.Player, CardCatalog.GetCard("tropical_background"), out _);
            Assert.IsTrue(game.Opponent.IsRevealed(hidden));
            Assert.Contains(hidden, game.Opponent.Hand);
        }

        [Test]
        public void AppreciationVictoryOccursOnlyDuringCycle()
        {
            BattleGame game = CreateGame();
            Assert.AreEqual(50, GameConstants.AppreciationVictoryTarget);
            game.Player.Appreciation = GameConstants.AppreciationVictoryTarget - 1;
            game.Player.QueueGrowth(2);
            CommitBoth(game);
            Assert.IsFalse(game.IsComplete);
            Assert.IsTrue(game.ResolveCombatPlans(new BattleAttackOrder[0], new BattleAttackOrder[0], out _));
            Assert.IsFalse(game.IsComplete);
            game.ResolveCycleAndAdvanceTurn();
            Assert.IsTrue(game.IsComplete);
            Assert.GreaterOrEqual(game.Player.Appreciation, GameConstants.AppreciationVictoryTarget);
            Assert.AreEqual(game.Player.Health, MatchResultData.LastResult.playerHp);
        }

        [Test]
        public void RetiredCardIsAbsentAndEveryPlayableCardHasBuildAndDiscardData()
        {
            Assert.IsNull(CardCatalog.GetCard("beer_helmet"));
            Assert.AreEqual(23, CardCatalog.AllCards.Count);
            Assert.IsTrue(CardCatalog.AllCards.All(card =>
                !string.IsNullOrWhiteSpace(card.GetArchetype()) &&
                !string.IsNullOrWhiteSpace(card.GetPillar()) &&
                !string.IsNullOrWhiteSpace(card.GetBuildEffect()) &&
                !string.IsNullOrWhiteSpace(card.GetDiscardCategory()) &&
                !string.IsNullOrWhiteSpace(card.GetDiscardEffect())));
        }

        [Test]
        public void HarmfulDiscardClampsAppreciationAndStaysFaceUp()
        {
            BattleGame game = CreateGame();
            game.Start();
            game.Player.Hand.Clear();
            CardDefinition risky = CardCatalog.GetCard("the_original");
            CardDefinition retained = CardCatalog.GetCard("white_skin");
            game.Player.Hand.Add(risky);
            game.Player.Hand.Add(retained);
            game.Player.Appreciation = 5;

            Assert.IsTrue(risky.IsHarmfulDiscard());
            StringAssert.Contains("lose 15 Appreciation", risky.GetDiscardConfirmation());
            Assert.IsTrue(game.TryDiscardCard(OwnerSide.Player, 0, out string message), message);
            Assert.AreEqual(0, game.Player.Appreciation);
            Assert.AreSame(risky, game.Player.DiscardPile.Single());
            Assert.AreSame(retained, game.Player.Hand.Single());
        }

        [Test]
        public void DecisionsAndPhaseTransitionsAreReplayable()
        {
            BattleGame game = CreateGame();
            game.Start();
            CardDefinition built = game.Player.Hand[0];
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out _));

            BattleReplayEvent decision = game.ReplayEvents.Last(entry => entry.EventType == "card-decision");
            Assert.AreEqual("Build", decision.Decision);
            Assert.AreEqual(built.id, decision.CardId);
            Assert.IsTrue(game.ReplayEvents.Any(entry => entry.EventType == "phase-transition" && entry.Phase == BattleTurnPhase.BuildOrDiscard));
        }

        [Test]
        public void TutorialProgressCanSaveAndRestart()
        {
            try
            {
                LocalSaveSystem.SaveTutorialProgress(8, true);
                Assert.AreEqual(8, LocalSaveSystem.LoadTutorialStep());
                Assert.IsTrue(LocalSaveSystem.LoadTutorialCoreDemonstrated());
                LocalSaveSystem.ResetTutorialProgress();
                Assert.AreEqual(0, LocalSaveSystem.LoadTutorialStep());
                Assert.IsFalse(LocalSaveSystem.LoadTutorialCoreDemonstrated());
            }
            finally
            {
                LocalSaveSystem.ResetTutorialProgress();
            }
        }

        [Test]
        public void AutoAttackRemainsAvailableInCasualAndTutorialRules()
        {
            BattleGame game = CreateGame();
            Assert.IsTrue(game.CanUseAutoAttack);
            Assert.IsEmpty(game.AutoAttackRestriction);
        }

        [Test]
        public void ThemeSelectionPersistsLocally()
        {
            AppreciatorsTheme original = LocalSaveSystem.LoadTheme();
            try
            {
                LocalSaveSystem.SaveTheme(AppreciatorsTheme.Light);
                Assert.AreEqual(AppreciatorsTheme.Light, LocalSaveSystem.LoadTheme());
                Assert.IsFalse(ThemeService.IsDark);
                ThemeService.Toggle();
                Assert.AreEqual(AppreciatorsTheme.Dark, LocalSaveSystem.LoadTheme());
            }
            finally
            {
                LocalSaveSystem.SaveTheme(original);
            }
        }

        private static BattleGame CreateGame()
        {
            List<CardDefinition> deck = CardCatalog.GetCards(CardCatalog.StarterDeckIds());
            return new BattleGame("Revamp Tester", deck, deck, 20260716);
        }

        private static BattleCardInstance AddUnit(BattleGame game, OwnerSide side, string id)
        {
            BattleCardInstance unit = new BattleCardInstance(CardCatalog.GetCard(id), side);
            unit.PlaceInGrowthRow();
            game.MainLane.GetCards(side).Add(unit);
            return unit;
        }

        private static void CommitBoth(BattleGame game)
        {
            game.Player.HasCommittedCardThisTurn = true;
            game.Opponent.HasCommittedCardThisTurn = true;
        }
    }
}
