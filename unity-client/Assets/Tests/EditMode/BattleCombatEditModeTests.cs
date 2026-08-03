using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AppreciatorsTcg.AI;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.Tests
{
    public class BattleCombatEditModeTests
    {
        [Test]
        public void DrawCreatesTwoCardDecisionAndSecondCardWaitsForForcedDiscard()
        {
            BattleGame game = CreateGame();
            game.Start();
            game.Player.Hand.Clear();
            game.Player.Hand.Add(CardCatalog.GetCard("tropical_background"));
            game.Player.Hand.Add(CardCatalog.GetCard("white_skin"));

            Assert.AreEqual(2, game.Player.Hand.Count);
            Assert.AreEqual(BattleTurnPhase.Learn, game.Phase);
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out string message), message);
            Assert.AreEqual(1, game.Player.Hand.Count);
            Assert.IsTrue(game.Player.HasCommittedCardThisTurn);
            Assert.IsFalse(game.TryDiscardCard(OwnerSide.Player, 0, out string secondMessage), secondMessage);
            StringAssert.Contains("already played", secondMessage);

            CardDefinition remaining = game.Player.Hand.Single();
            CardDefinition forced = game.ForceDiscardRemainingCard(OwnerSide.Player, out string forcedMessage);
            Assert.AreSame(remaining, forced, forcedMessage);
            Assert.IsEmpty(game.Player.Hand);
            Assert.Contains(remaining, game.Player.DiscardPile);
        }

        [Test]
        public void DiscardIsPublicAndGrowthBanksOnlyAtTally()
        {
            BattleGame game = CreateGame();
            game.Start();
            game.Player.Hand.Clear();
            game.Player.Hand.Add(CardCatalog.GetCard("tropical_background"));
            game.Player.Hand.Add(CardCatalog.GetCard("white_skin"));
            int discardIndex = game.Player.Hand.FindIndex(card => CardEffectResolver.CanResolveDiscard(game, game.Player, OwnerSide.Player, card, out _));
            Assert.GreaterOrEqual(discardIndex, 0);
            CardDefinition discardedCard = game.Player.Hand[discardIndex];
            CardDefinition retainedCard = game.Player.Hand[discardIndex == 0 ? 1 : 0];
            int startingScore = game.Player.Appreciation;

            Assert.IsTrue(game.TryDiscardCard(OwnerSide.Player, discardIndex, out string message), message);
            Assert.AreEqual(startingScore, game.Player.Appreciation, "Pending Growth must not become score before Cycle.");
            Assert.AreEqual(discardedCard.id, game.Player.DiscardPile.Single().id);

            game.TryBuildCard(OwnerSide.Opponent, 0, out _);
            game.ResolveGrowthAndAdvanceTurn();

            Assert.GreaterOrEqual(game.Player.Appreciation, startingScore);
            Assert.AreEqual(2, game.Player.Hand.Count, "Every new turn draws two fresh cards.");
            Assert.Contains(retainedCard, game.Player.DiscardPile, "The unplayed second card must become a public forced discard.");
        }

        [Test]
        public void BuiltPermanentsExhaustForGrowthAndRefreshNextTurn()
        {
            BattleGame game = CreateGame();
            game.Start();
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Player, 0, out _));
            Assert.IsTrue(game.TryBuildCard(OwnerSide.Opponent, 0, out _));
            BattleCardInstance permanent = game.MainLane.PlayerCards.Single();

            game.GatherAndTally(OwnerSide.Player);
            Assert.IsTrue(permanent.IsExhausted);
            Assert.AreEqual(permanent.GrowthValue, game.LastPlayerTally.BoardGrowth);

            game.GatherAndTally(OwnerSide.Opponent);
            game.ResolveGrowthAndAdvanceTurn();
            Assert.IsFalse(permanent.IsExhausted);
        }

        [Test]
        public void AdjacentTraitsAndThreeDomainsCreateCombinationGrowth()
        {
            List<BattleCardInstance> cards = new List<BattleCardInstance>
            {
                new BattleCardInstance(CardCatalog.GetCard("great_white_head"), OwnerSide.Player),
                new BattleCardInstance(CardCatalog.GetCard("unicorn_head"), OwnerSide.Player),
                new BattleCardInstance(CardCatalog.GetCard("purple_skin"), OwnerSide.Player),
                new BattleCardInstance(CardCatalog.GetCard("tropical_background"), OwnerSide.Player)
            };

            int growth = BattleRules.CalculateCombinationGrowth(cards);

            Assert.GreaterOrEqual(growth, 5, "A matching Head pair grants +2 and three represented domains grant +3.");
        }

        [Test]
        public void LeaderModifierRoundsUpDuringTally()
        {
            BattleGame game = CreateGame();
            game.Start();
            int discardIndex = game.Player.Hand.FindIndex(card => CardEffectResolver.CanResolveDiscard(game, game.Player, OwnerSide.Player, card, out _));
            Assert.IsTrue(game.TryDiscardCard(OwnerSide.Player, discardIndex, out _));
            Assert.IsTrue(game.TryUseLeaderAbility(OwnerSide.Player, out string leaderMessage), leaderMessage);
            int pending = game.Player.PendingAbilityGrowth;

            BattleTallyResult tally = game.GatherAndTally(OwnerSide.Player);

            Assert.AreEqual((pending + 3) / 4, tally.ModifierGrowth);
            Assert.AreEqual(pending + tally.ModifierGrowth, tally.TotalGrowth);
        }

        [Test]
        public void VictoryIsCheckedAfterTallyNotWhenGrowthIsQueued()
        {
            BattleGame game = CreateGame();
            game.Start();
            game.Player.Appreciation = GameConstants.AppreciationVictoryTarget - 1;
            int discardIndex = game.Player.Hand.FindIndex(card => CardEffectResolver.CanResolveDiscard(game, game.Player, OwnerSide.Player, card, out _));
            Assert.IsTrue(game.TryDiscardCard(OwnerSide.Player, discardIndex, out _));
            game.Player.QueueGrowth(2);
            Assert.IsFalse(game.IsComplete);
            Assert.Less(game.Player.Appreciation, GameConstants.AppreciationVictoryTarget);

            Assert.IsTrue(game.TryBuildCard(OwnerSide.Opponent, 0, out _));
            game.ResolveGrowthAndAdvanceTurn();

            Assert.IsTrue(game.IsComplete);
            Assert.GreaterOrEqual(game.Player.Appreciation, GameConstants.AppreciationVictoryTarget);
        }

        [Test]
        public void MatchPlaymatUsesOneGrowthRowAndKeepsExistingControlPlacements()
        {
            Canvas[] before = Object.FindObjectsOfType<Canvas>();
            GameObject host = new GameObject("GrowthRowUiTestHost");

            try
            {
                MatchScreenController controller = host.AddComponent<MatchScreenController>();
                InvokeLifecycle(controller, "Awake", typeof(ScreenControllerBase));
                InvokeLifecycle(controller, "Start", typeof(MatchScreenController));
                Canvas canvas = Object.FindObjectsOfType<Canvas>().Except(before).Single();
                RectTransform hand = canvas.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "Hand");
                RectTransform endTurn = canvas.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "END TURN");
                RectTransform nextPhase = canvas.GetComponentsInChildren<RectTransform>(true).Single(item => item.name.Contains("NEXT PHASE"));
                RectTransform growthRow = canvas.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "GrowthLane");

                Assert.GreaterOrEqual(endTurn.anchorMin.x, hand.anchorMax.x);
                Assert.AreEqual(endTurn.anchorMin, nextPhase.anchorMin, "NEXT PHASE must overlay the native End Turn footprint.");
                Assert.AreEqual(endTurn.anchorMax, nextPhase.anchorMax, "NEXT PHASE must overlay the native End Turn footprint.");
                Assert.IsFalse(nextPhase.gameObject.activeSelf);
                Assert.AreEqual(0.936f, growthRow.anchorMax.x - growthRow.anchorMin.x, 0.001f);
                Assert.AreEqual(1, canvas.GetComponentsInChildren<MatchLaneDropZone>(true).Length);
                Assert.IsFalse(canvas.GetComponentsInChildren<RectTransform>(true).Any(item => item.name == "Art" || item.name == "Blockchain"));
                Assert.IsTrue(canvas.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("BUILD OR DISCARD")));
                Assert.AreEqual(2, canvas.GetComponentsInChildren<AppreciationLiquidMeter>(true).Length);
                Assert.IsFalse(canvas.GetComponentsInChildren<Transform>(true).Any(item => item.name == "OpponentEmptyMat" || item.name == "PlayerEmptyMat"));
                Assert.AreEqual(0f, growthRow.GetComponent<Image>().color.a, 0.001f, "The play field must leave the printed playmat visible.");
                RectTransform playerMeter = canvas.GetComponentsInChildren<AppreciationLiquidMeter>(true).Single(item => item.name == "PlayerAppreciationReservoir").GetComponent<RectTransform>();
                Assert.AreEqual(0.198f, playerMeter.anchorMin.x, 0.001f);
                Assert.AreEqual(0.375f, playerMeter.anchorMax.x, 0.001f);
                Assert.IsNull(playerMeter.GetComponent<Image>(), "The native Appreciation meter must not draw a panel over the printed button.");
                Image nativeFill = playerMeter.GetComponentsInChildren<Image>(true).Single(image => image.name == "NativeAppreciationFill");
                Assert.AreEqual(Image.Type.Filled, nativeFill.type);
                Assert.IsNotNull(nativeFill.sprite, "The fill should reuse the printed playmat button art.");
                playerMeter.GetComponent<AppreciationLiquidMeter>().SetValue(GameConstants.AppreciationVictoryTarget / 2, false);
                Assert.AreEqual(0.5f, nativeFill.fillAmount, 0.001f, "The printed button itself should fill in proportion to Appreciation.");
                Assert.AreEqual("app_playmat_native", nativeFill.sprite.texture.name);
                Assert.IsTrue(playerMeter.GetComponentsInChildren<Text>(true).Any(text => text.text == "25/50"),
                    "The x/50 score must be printed inside the native Appreciation button footprint.");
                Assert.IsFalse(canvas.GetComponentsInChildren<RectTransform>(true).Any(item => item.name.EndsWith("PlaymatLabel") || item.name.EndsWith("NativeWordmark")),
                    "Learn, Build, and Grow must be printed into the playmat rather than drawn as UI overlays.");
                Assert.IsFalse(canvas.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("ACTION MAT") || text.text.Contains("LEADER")));
                Assert.AreEqual(0, hand.GetComponentsInChildren<MatchHandCardInput>(true).Length, "The visible hand must begin empty before the Draw animation.");
                Assert.IsNotNull(canvas.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(item => item.name == "FaceDownDrawDeck"));
                Assert.IsEmpty(canvas.GetComponentsInChildren<Button>(true).Where(button => button.name.Contains("Shard")));
                Assert.IsNotNull(canvas.GetComponentsInChildren<Button>(true).SingleOrDefault(button => button.name.Contains("PHASES: AUTO")));
                Assert.IsNotNull(canvas.GetComponentsInChildren<Button>(true).SingleOrDefault(button => button.name.Contains("NEXT PHASE")));
                Assert.AreEqual(2, canvas.GetComponentsInChildren<Button>(true).Count(button => button.name.EndsWith("StarMenuZone")));
                Transform starSettings = canvas.GetComponentsInChildren<Transform>(true).Single(item => item.name == "StarSettingsMenu");
                Assert.IsFalse(starSettings.gameObject.activeSelf);
                Assert.IsTrue(starSettings.GetComponentsInChildren<Text>(true).Any(text => text.text.StartsWith("THEME:")));
                canvas.GetComponentsInChildren<Button>(true).Single(button => button.name == "PlayerStarMenuZone").onClick.Invoke();
                Assert.IsTrue(starSettings.gameObject.activeSelf);
                canvas.GetComponentsInChildren<Button>(true).Single(button => button.name == "OpponentStarMenuZone").onClick.Invoke();
                Assert.IsFalse(starSettings.gameObject.activeSelf);
                Image playmat = canvas.GetComponentsInChildren<Transform>(true).Single(item => item.name == "PlaymatArt").GetComponent<Image>();
                Assert.AreEqual("app_playmat_native", playmat.sprite.texture.name, "The revised labels should be part of the playmat texture itself.");
                Assert.Less(playmat.color.r, 0.75f, "The match playmat should use its dark-mode tint.");

                FieldInfo drawPresentationField = typeof(MatchScreenController).GetField("drawPresentationActive", BindingFlags.Instance | BindingFlags.NonPublic);
                drawPresentationField.SetValue(controller, false);
                InvokeLifecycle(controller, "UpdateScreen", typeof(MatchScreenController));
                Assert.AreEqual(2, canvas.GetComponentsInChildren<Transform>(true).Count(item => item.name == "CombatStatsBadge"));
                Assert.IsTrue(canvas.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("ATK") && text.text.Contains("DEF")));

                Button[] handButtons = hand.GetComponentsInChildren<Button>(true);
                Assert.AreEqual(2, handButtons.Length);
                Assert.IsTrue(hand.GetComponentsInChildren<UiCardMotion>(true).All(card => Mathf.Approximately(card.transform.localScale.x, 1f)),
                    "Rebuilt card views must begin at full scale so they do not blink between state updates.");
                Transform[] originalPlayerCards = hand.GetComponentsInChildren<MatchHandCardInput>(true).Select(item => item.transform).ToArray();
                Transform[] originalOpponentCards = canvas.GetComponentsInChildren<RectTransform>(true).Where(item => item.name == "CardBack").Cast<Transform>().ToArray();
                handButtons[0].onClick.Invoke();
                CollectionAssert.AreEqual(originalPlayerCards, hand.GetComponentsInChildren<MatchHandCardInput>(true).Select(item => item.transform).ToArray(), "Selecting a card must not rebuild the player's hand.");
                CollectionAssert.AreEqual(originalOpponentCards, canvas.GetComponentsInChildren<RectTransform>(true).Where(item => item.name == "CardBack").Cast<Transform>().ToArray(), "Selecting a card must not rebuild the opponent's hand.");

                FieldInfo gameField = typeof(MatchScreenController).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
                BattleGame matchGame = (BattleGame)gameField.GetValue(controller);
                matchGame.Player.Appreciation = 100;
                matchGame.MainLane.PlayerCards.Add(new BattleCardInstance(CardCatalog.GetCard("regular_body"), OwnerSide.Player));
                matchGame.MainLane.PlayerCards.Add(new BattleCardInstance(CardCatalog.GetCard("white_skin"), OwnerSide.Player));
                controller.PlayHandCardFromDrop(0, LaneType.Community);
                Assert.IsFalse(matchGame.Player.HasCommittedCardThisTurn, "Dropping on the board must wait for the player's Build/Discard choice.");
                Image choiceBackdrop = canvas.GetComponentsInChildren<Transform>(true).Single(item => item.name == "PlayChoiceDialog").GetComponent<Image>();
                Assert.AreEqual(0f, choiceBackdrop.color.a, 0.001f, "The card choice must not black out the play field.");
                Button cancelChoice = canvas.GetComponentsInChildren<Button>(true).Single(button => button.name == "PUT CARD BACK");
                cancelChoice.onClick.Invoke();
                Assert.IsFalse(matchGame.Player.HasCommittedCardThisTurn);
                CollectionAssert.AreEqual(originalPlayerCards, hand.GetComponentsInChildren<MatchHandCardInput>(true).Select(item => item.transform).ToArray(), "Canceling the chooser must leave the hand intact.");

                int discardIndex = matchGame.Player.Hand.FindIndex(card => CardEffectResolver.CanResolveDiscard(matchGame, matchGame.Player, OwnerSide.Player, card, out _));
                Assert.GreaterOrEqual(discardIndex, 0);
                handButtons[discardIndex].onClick.Invoke();
                controller.PlayHandCardFromDrop(discardIndex, LaneType.Community);
                Button discardButton = canvas.GetComponentsInChildren<Button>(true).Single(button => button.name.StartsWith("DISCARD"));
                discardButton.onClick.Invoke();
                Button harmfulConfirm = canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(button => button.name == "CONTINUE — DISCARD");
                harmfulConfirm?.onClick.Invoke();
                Assert.IsTrue(matchGame.Player.HasCommittedCardThisTurn);
                Assert.AreEqual(1, matchGame.Player.DiscardPile.Count);

                CardDefinition revealedOpponent = matchGame.Opponent.Hand.First();
                matchGame.Opponent.Reveal(revealedOpponent);
                InvokeLifecycle(controller, "UpdateScreen", typeof(MatchScreenController));
                CardInspectionTrigger publicTrigger = canvas.GetComponentsInChildren<CardInspectionTrigger>(true)
                    .First(trigger => trigger.Card == revealedOpponent && trigger.ClickToInspect);
                Assert.IsNotNull(publicTrigger);
            }
            finally
            {
                foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>().Except(before))
                {
                    Object.DestroyImmediate(canvas.gameObject);
                }
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FieldCardsExchangeAttackAgainstDefenseBeforeTally()
        {
            CardDefinition playerDefinition = new CardDefinition
            {
                id = "test_player",
                name = "Test Builder",
                attack = 5,
                defense = 4,
                rarity = GameConstants.Common,
                type = GameConstants.Original
            };
            CardDefinition opponentDefinition = new CardDefinition
            {
                id = "test_opponent",
                name = "Test Defender",
                attack = 3,
                defense = 5,
                rarity = GameConstants.Common,
                type = GameConstants.Original
            };
            BattleGame game = new BattleGame(
                "Battle Tester",
                new List<CardDefinition> { playerDefinition },
                new List<CardDefinition> { opponentDefinition },
                77);
            BattleCardInstance playerCard = new BattleCardInstance(playerDefinition, OwnerSide.Player);
            BattleCardInstance opponentCard = new BattleCardInstance(opponentDefinition, OwnerSide.Opponent);
            playerCard.PlaceInGrowthRow();
            opponentCard.PlaceInGrowthRow();
            game.MainLane.PlayerCards.Add(playerCard);
            game.MainLane.OpponentCards.Add(opponentCard);
            game.Player.HasCommittedCardThisTurn = true;
            game.Opponent.HasCommittedCardThisTurn = true;

            game.ResolveFieldBattle();

            Assert.AreEqual(BattleTurnPhase.Battle, game.Phase);
            Assert.AreEqual(2, game.LastCombatEvents.Count, "Both field cards should animate an attack in the exchange.");
            Assert.AreEqual(1, playerCard.CurrentAppreciation, "Opponent Attack should reduce the player's Defense.");
            Assert.AreEqual(0, opponentCard.CurrentAppreciation, "Player Attack should reduce the opponent's Defense to zero.");
            Assert.AreEqual(1, game.MainLane.PlayerCards.Count);
            Assert.IsEmpty(game.MainLane.OpponentCards);
            Assert.AreEqual(opponentDefinition, game.Opponent.DiscardPile.Single());

            BattleTallyResult playerTally = game.GatherAndTally(OwnerSide.Player);
            BattleTallyResult opponentTally = game.GatherAndTally(OwnerSide.Opponent);
            Assert.AreEqual(playerCard.GrowthValue, playerTally.BoardGrowth);
            Assert.AreEqual(0, opponentTally.BoardGrowth, "Defeated cards must leave the field before Growth is tallied.");
        }

        [Test]
        public void WorkbookCardMetaManifestLoadsAllSeasonsAndIdentities()
        {
            CardMetaManifestDocument manifest = CardMetaManifest.Load();

            Assert.IsNotNull(manifest);
            Assert.AreEqual(6666, manifest.totalCards);
            Assert.AreEqual(432, manifest.totalAbilities);
            Assert.AreEqual(22, manifest.totalSeasons);
            Assert.AreEqual(22, manifest.crownCards);
            Assert.AreEqual(22, manifest.seasons.Length);
            Assert.AreEqual(303, manifest.seasons[0].cards);
            Assert.AreEqual(141, manifest.seasons[0].common);
            Assert.AreEqual(1, manifest.seasons[0].crown);
            Assert.AreEqual(9, manifest.archetypes.Length);
            StringAssert.Contains("pending", manifest.metadataStatus.ToLowerInvariant());
        }

        [Test]
        public void TwentyFiveSeededEngineBattlesCompleteWithinTargetArc()
        {
            List<CardDefinition> deck = CardCatalog.GetCards(CardCatalog.StarterDeckIds());
            for (int seed = 1; seed <= 25; seed++)
            {
                BattleGame game = new BattleGame("Stress A", deck, deck, seed);
                System.Random random = new System.Random(seed);
                game.Start();

                while (!game.IsComplete)
                {
                    SimpleAiPlayer.PlayTurn(game, OwnerSide.Player, random);
                    SimpleAiPlayer.PlayTurn(game, OwnerSide.Opponent, random);
                    Assert.IsTrue(game.Player.HasCommittedCardThisTurn || game.Player.CommitSkippedThisTurn,
                        $"Player AI failed to commit at seed {seed}, turn {game.Turn}.");
                    string opponentDecisionState = string.Join(" | ", game.Opponent.Hand.Select(card =>
                    {
                        bool payable = CardEffectResolver.CanResolveDiscard(game, game.Opponent, OwnerSide.Opponent, card, out string reason);
                        return $"{card.id}: discard={payable} {reason}";
                    }));
                    Assert.IsTrue(game.Opponent.HasCommittedCardThisTurn || game.Opponent.CommitSkippedThisTurn,
                        $"Opponent AI failed to commit at seed {seed}, turn {game.Turn}. Board={game.MainLane.OpponentCards.Count}; skipped={game.Opponent.CommitSkippedThisTurn}; {opponentDecisionState}");
                    game.ResolveGrowthAndAdvanceTurn();
                }

                Assert.LessOrEqual(game.Turn, GameConstants.MaxTurn);
                Assert.AreEqual(1, MatchResultData.LastResult.laneScores.Length);
                Assert.Greater(game.Player.Appreciation + game.Opponent.Appreciation, 0);
            }
        }

        private static BattleGame CreateGame()
        {
            List<CardDefinition> deck = CardCatalog.GetCards(CardCatalog.StarterDeckIds());
            return new BattleGame("Growth Tester", deck, deck, 20260714);
        }

        private static void InvokeLifecycle(object target, string methodName, System.Type declaringType)
        {
            MethodInfo method = declaringType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing lifecycle method {declaringType.Name}.{methodName}");
            method.Invoke(target, null);
        }
    }
}
