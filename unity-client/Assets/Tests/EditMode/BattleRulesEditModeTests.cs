using System.Linq;
using System.Reflection;
using AppreciatorsTcg.AI;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.Packs;
using AppreciatorsTcg.UI;
using BattleCardDefinition = AppreciatorsTcg.Cards.CardDefinition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.Tests
{
    public class BattleRulesEditModeTests
    {
        [Test]
        public void PrototypeCardSetHasRequiredCounts()
        {
            Assert.AreEqual(23, CardCatalog.AllCards.Count);
            Assert.AreEqual(16, CardCatalog.AllCards.Count(card => card.type == GameConstants.Original));
            Assert.AreEqual(0, CardCatalog.AllCards.Count(card => card.type == GameConstants.Companion));
            Assert.AreEqual(7, CardCatalog.AllCards.Count(card => card.type == GameConstants.Item));
            Assert.AreEqual(0, CardCatalog.AllCards.Count(card => card.type == GameConstants.Event));
        }

        [Test]
        public void StarterDeckIsValid()
        {
            var deck = CardCatalog.StarterDeckIds();
            Assert.AreEqual(GameConstants.DeckSize, deck.Count);
            Assert.IsTrue(PlayerDeckService.ValidateDeck(deck));
            Assert.LessOrEqual(deck.GroupBy(id => id).Max(group => group.Count()), GameConstants.MaxNormalCardCopies);
        }

        [Test]
        public void CompetitiveDeckCopyLimitsAreEnforced()
        {
            var starter = CardCatalog.StarterDeckIds();
            string normalId = starter.GroupBy(id => id).First(group => group.Count() == 2).Key;
            var tooManyNormal = starter.ToList();
            tooManyNormal[tooManyNormal.FindIndex(id => id != normalId)] = normalId;
            Assert.IsFalse(PlayerDeckService.ValidateDeck(tooManyNormal));

            BattleCardDefinition premium = CardCatalog.AllCards.First(card => PlayerDeckService.MaxCopies(card) == 1);
            var premiumDuplicate = starter.ToList();
            premiumDuplicate[0] = premium.id;
            premiumDuplicate[1] = premium.id;
            Assert.IsFalse(PlayerDeckService.ValidateDeck(premiumDuplicate));
        }

        [Test]
        public void AppreciationRitualUsesThreeStarterPacksAndNeutralShardStoreTiers()
        {
            PackOpeningService service = new PackOpeningService(new System.Random(69));
            var packs = service.LoadPackDefinitions();

            Assert.AreEqual(6, packs.Count);
            Assert.IsTrue(packs.All(pack => !pack.attunementEnabled));
            Assert.AreEqual(0, packs.Single(pack => pack.id == "starter_appreciation_pack").shardCost);
            Assert.AreEqual(300, packs.Single(pack => pack.id == "random_appreciation_pack").shardCost);
            Assert.AreEqual(900, packs.Single(pack => pack.id == "uncommon_guaranteed_pack").shardCost);
            Assert.AreEqual(1200, packs.Single(pack => pack.id == "rare_guaranteed_pack").shardCost);
            Assert.AreEqual(1500, packs.Single(pack => pack.id == "mythic_guaranteed_pack").shardCost);
            Assert.AreEqual(1800, packs.Single(pack => pack.id == "legendary_guaranteed_pack").shardCost);
            Assert.IsNotNull(PackCardArtResolver.LoadPackSprite(packs[0]), "Unopened packs should use the official card-back artwork.");

            var rewardCards = service.LoadCardDefinitions();
            Assert.AreEqual(23, rewardCards.Count);
            Assert.AreEqual(9, rewardCards.Count(card => card.lane == Lane.Art));
            Assert.AreEqual(7, rewardCards.Count(card => card.lane == Lane.Community));
            Assert.AreEqual(7, rewardCards.Count(card => card.lane == Lane.Blockchain));
            Assert.AreEqual(0, rewardCards.Count(card => card.type == GameConstants.Companion));
            Assert.IsTrue(rewardCards.All(card => card.artReference.StartsWith("Art/")));
        }

        [Test]
        public void OfficialCardFrontUsesLaneHeaderAndKeepsOfficialBack()
        {
            GameObject host = new GameObject("OfficialCardFrontTest");
            try
            {
                BattleCardDefinition card = CardCatalog.GetCard("regular_body");
                GameObject front = UIFactory.CreateCardPanel(host.transform, card, compact: true);
                string[] childNames = front.GetComponentsInChildren<Transform>(true).Select(item => item.name).ToArray();
                Assert.Contains("BakedCardFace", childNames);
                Assert.IsFalse(childNames.Contains("LaneStrengths"), "Production card values must be baked into the face, not layered as live UI.");
                Assert.IsFalse(childNames.Contains("CardNameStrip"), "Production card names must be baked into the face, not layered as live UI.");
                Image template = front.GetComponentsInChildren<Image>(true).Single(image => image.gameObject.name == "BakedCardFace");
                Assert.IsNotNull(template.sprite, "The generated production card face must render as one texture.");
                Assert.IsFalse(childNames.Contains("CardTypeStrip"), "Retired card-front strips must not be used.");

                GameObject back = UIFactory.CreateCardBackPanel(host.transform);
                Image backImage = back.GetComponentsInChildren<Image>(true).Single(image => image.gameObject.name == "OfficialCardBackArt");
                Assert.IsNotNull(backImage.sprite);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void EveryActiveCardHasOneBakedProductionFace()
        {
            foreach (BattleCardDefinition card in CardCatalog.AllCards)
            {
                Sprite face = AppreciatorsTcg.Cards.CardArtResolver.LoadCardFaceSprite(card);
                Assert.IsNotNull(face, $"Missing baked card face for {card.id}.");
                Assert.AreEqual(2f / 3f, face.rect.width / face.rect.height, 0.001f, $"{card.id} must keep the official 2:3 card ratio.");
            }
        }

        [Test]
        public void PrototypeAudioMappingsHaveAllRuntimeClips()
        {
            string[] clips =
            {
                "Blip01", "Blip03", "Coin01", "Coin03", "Explosion01", "Explosion02",
                "Sci-Fi_Beem", "Sci-Fi_Landing", "Sci-Fi_LasorGun", "Sci-Fi_Signal", "Smack01"
            };

            foreach (string clip in clips)
            {
                Assert.IsNotNull(Resources.Load<AudioClip>($"Audio/Battle/{clip}"), $"Audio mapping is missing {clip}.");
            }
        }

        [Test]
        public void NamedDeckCanBeSavedSelectedAndDeleted()
        {
            PlayerDeckProfile original = PlayerDeckService.GetActiveDeck();
            Assert.IsTrue(PlayerDeckService.SaveNamedDeck(
                string.Empty,
                "Automated Test Deck",
                CardCatalog.StarterDeckIds(),
                out PlayerDeckProfile saved,
                out string saveMessage), saveMessage);
            Assert.AreEqual("Automated Test Deck", PlayerDeckService.GetActiveDeck().name);
            Assert.AreEqual(GameConstants.DeckSize, saved.cardIds.Count);

            Assert.IsTrue(PlayerDeckService.DeleteNamedDeck(saved.id, out string deleteMessage), deleteMessage);
            Assert.IsNull(PlayerDeckService.GetDeck(saved.id));
            Assert.IsTrue(PlayerDeckService.SelectDeck(original.id, out string restoreMessage), restoreMessage);
        }

        [Test]
        public void DeckBuilderCreatesSearchableCatalogAndGroupedThirtyCardWorkbench()
        {
            PlayerDeckProfile original = PlayerDeckService.GetActiveDeck();
            PlayerDeckService.SelectDeck(PlayerDeckService.StarterDeckId, out _);
            Canvas[] before = Object.FindObjectsOfType<Canvas>();
            GameObject host = new GameObject("DeckBuilderTestHost");

            try
            {
                DeckBuilderScreenController controller = host.AddComponent<DeckBuilderScreenController>();
                InvokeLifecycle(controller, "Awake", typeof(ScreenControllerBase));
                InvokeLifecycle(controller, "Start", typeof(DeckBuilderScreenController));
                Canvas canvas = Object.FindObjectsOfType<Canvas>().Except(before).Single();
                Transform[] objects = canvas.GetComponentsInChildren<Transform>(true);

                Assert.IsTrue(objects.Any(item => item.name == "CollectionLane"));
                Assert.IsTrue(objects.Any(item => item.name == "DeckLane"));
                Assert.IsTrue(objects.Any(item => item.name == "CollectionToolbar"));
                Assert.IsTrue(objects.Any(item => item.name == "CollectionSearch"));
                Assert.IsTrue(objects.Any(item => item.name == "DeckDropWell"));
                Assert.AreEqual(3, objects.Count(item => item.name.StartsWith("Filter_")));
                Assert.AreEqual(0, objects.Count(item => item.name.StartsWith("EmptySlot")));
                Assert.IsTrue(canvas.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("DROP CARDS HERE")));

                GridLayoutGroup collectionGrid = objects.Single(item => item.name == "CollectionScroll")
                    .GetComponentInChildren<GridLayoutGroup>(true);
                Assert.IsNotNull(collectionGrid);
                Assert.AreEqual(4, collectionGrid.constraintCount);

                Button loadStarter = canvas.GetComponentsInChildren<Button>(true)
                    .Single(button => button.GetComponentInChildren<Text>(true)?.text == "LOAD STARTER");
                loadStarter.onClick.Invoke();
                Transform[] loadedObjects = canvas.GetComponentsInChildren<Transform>(true);
                Assert.IsTrue(loadedObjects.Any(item => item.name == $"DeckGroup_{GameConstants.Original}"));
                Assert.IsFalse(loadedObjects.Any(item => item.name == $"DeckGroup_{GameConstants.Companion}"));
                Assert.IsTrue(loadedObjects.Any(item => item.name == $"DeckGroup_{GameConstants.Item}"));
                Assert.IsTrue(canvas.GetComponentsInChildren<Text>(true).Any(text => text.text == $"{GameConstants.DeckSize} CARDS SELECTED"));
                Assert.IsFalse(canvas.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("SIDEBOARD") || text.text.Contains("MANA")));

                LayoutElement identity = loadedObjects.Single(item => item.name == "DeckIdentity").GetComponent<LayoutElement>();
                LayoutElement saved = loadedObjects.Single(item => item.name == "SavedDecks").GetComponent<LayoutElement>();
                LayoutElement actions = loadedObjects.Single(item => item.name == "Actions").GetComponent<LayoutElement>();
                LayoutElement collection = loadedObjects.Single(item => item.name == "CollectionLane").GetComponent<LayoutElement>();
                LayoutElement deck = loadedObjects.Single(item => item.name == "DeckLane").GetComponent<LayoutElement>();
                InputField nameInput = canvas.GetComponentsInChildren<InputField>(true)
                    .Single(input => input.gameObject.name != "CollectionSearch");

                Assert.LessOrEqual(identity.preferredHeight, 48f);
                Assert.LessOrEqual(saved.preferredHeight, 48f);
                Assert.LessOrEqual(actions.preferredHeight, 40f);
                Assert.Greater(collection.flexibleWidth, 0f);
                Assert.Greater(deck.flexibleWidth, 0f);
                Assert.LessOrEqual(nameInput.GetComponent<LayoutElement>().preferredWidth, 270f);
            }
            finally
            {
                foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>().Except(before))
                {
                    Object.DestroyImmediate(canvas.gameObject);
                }
                Object.DestroyImmediate(host);
                PlayerDeckService.SelectDeck(original.id, out _);
            }
        }

        [Test]
        public void SharedUiColorsMatchTheOfficialAppreciatorsPalette()
        {
            Assert.AreEqual("0F0A46", ColorUtility.ToHtmlStringRGB(UIFactory.Background));
            Assert.AreEqual("FFC700", ColorUtility.ToHtmlStringRGB(UIFactory.Accent));
            Assert.AreEqual("00BEE1", ColorUtility.ToHtmlStringRGB(UIFactory.Blue));
            Assert.AreEqual("46CB37", ColorUtility.ToHtmlStringRGB(UIFactory.Green));
            Assert.AreEqual("FF2314", ColorUtility.ToHtmlStringRGB(UIFactory.Red));
            Assert.AreEqual("7841AA", ColorUtility.ToHtmlStringRGB(UIFactory.PortalViolet));
        }

        [Test]
        public void CasualPlayOpensNamedDeckQueueSelector()
        {
            Canvas[] before = Object.FindObjectsOfType<Canvas>();
            GameObject host = new GameObject("MainMenuTestHost");

            try
            {
                LocalSaveSystem.MarkTutorialCompleted();
                MainMenuController controller = host.AddComponent<MainMenuController>();
                InvokeLifecycle(controller, "Awake", typeof(ScreenControllerBase));
                InvokeLifecycle(controller, "Start", typeof(MainMenuController));
                Canvas canvas = Object.FindObjectsOfType<Canvas>().Except(before).Single();
                Button playCasual = canvas.GetComponentsInChildren<Button>(true)
                    .Single(button => button.GetComponentInChildren<Text>(true)?.text == "PLAY CASUAL");
                Assert.IsTrue(canvas.GetComponentsInChildren<Button>(true)
                    .Any(button => button.GetComponentInChildren<Text>(true)?.text.StartsWith("TURN TUTORIAL") == true));
                playCasual.onClick.Invoke();

                DeckChoicePanel choice = canvas.GetComponentInChildren<DeckChoicePanel>(true);
                Assert.IsNotNull(choice);
                Assert.IsTrue(choice.gameObject.activeInHierarchy);
                Assert.IsTrue(canvas.GetComponentsInChildren<Text>(true).Any(text => text.text.Contains("QUEUE WITH SELECTED DECK")));
            }
            finally
            {
                foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>().Except(before))
                {
                    Object.DestroyImmediate(canvas.gameObject);
                }
                Object.DestroyImmediate(host);
                LocalSaveSystem.ResetTutorialProgress();
            }
        }

        [Test]
        public void CardsHaveFinalArtSlots()
        {
            foreach (var card in CardCatalog.AllCards)
            {
                Assert.AreEqual(card.id, card.artKey);
                StringAssert.StartsWith("Art/", card.EffectiveArtPath());
                StringAssert.EndsWith(card.id, card.EffectiveArtPath());
            }
        }

        [Test]
        public void OriginalsMetadataCatalogLoadsApprovedOnChainMappings()
        {
            OriginalsTraitCatalogDocument catalog = OriginalsMetadataCatalog.Catalog;

            Assert.AreEqual(33139, catalog.chainId);
            Assert.AreEqual(6666, catalog.totalSupply);
            Assert.AreEqual(6666, catalog.importedTokenCount);
            Assert.AreEqual(28, catalog.approvedGameplayTraits.Count);

            OriginalsGameplayTraitMapping devilDog = OriginalsMetadataCatalog.GetGameplayTrait("devil_dog_companion");
            Assert.IsNotNull(devilDog);
            Assert.IsTrue(devilDog.IsOnChainMatch);
            Assert.AreEqual(76, devilDog.tokenCount);
            Assert.IsFalse(catalog.approvedGameplayTraits.Any(mapping =>
                mapping.displayName.ToLowerInvariant().Contains("dreaded ape")));
        }

        [Test]
        public void BattleCompletesAtTwoHundredOrAfterTheElevenTurnArc()
        {
            BattleGame game = new BattleGame("Tester", PlayerDeckService.LoadDeckOrStarter());
            game.Start();

            while (!game.IsComplete)
            {
                SimpleAiPlayer.PlayTurn(game, OwnerSide.Player, new System.Random(game.Turn));
                game.EndPlayerTurnAndRunAi();
            }

            Assert.LessOrEqual(game.Turn, GameConstants.MaxTurn);
            Assert.IsNotNull(MatchResultData.LastResult);
            Assert.AreEqual(1, MatchResultData.LastResult.laneScores.Length);
            Assert.AreEqual(game.Player.GrowthScore, MatchResultData.LastResult.playerGrowth);
        }

        private static void InvokeLifecycle(object target, string methodName, System.Type declaringType)
        {
            MethodInfo method = declaringType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing lifecycle method {declaringType.Name}.{methodName}");
            method.Invoke(target, null);
        }
    }
}
