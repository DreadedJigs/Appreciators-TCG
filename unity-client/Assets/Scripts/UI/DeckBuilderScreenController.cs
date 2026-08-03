using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class DeckBuilderScreenController : ScreenControllerBase
    {
        private readonly List<string> deckIds = new List<string>();
        private string editingDeckId;
        private InputField deckNameInput;
        private InputField collectionSearchInput;
        private Text countText;
        private Text messageText;
        private Text deckLaneTitle;
        private Text deckCompositionText;
        private RectTransform cardsContent;
        private RectTransform deckContent;
        private Transform savedDecksContent;
        private ScrollRect deckScroll;
        private readonly Dictionary<string, Button> collectionFilterButtons = new Dictionary<string, Button>();
        private string collectionFilter = "ALL";
        private int animatedSlot = -1;

        private void Start()
        {
            GameObject screen = CreateFullScreenStack("Deck Builder");
            Text pageTitle = screen.transform.GetChild(0).GetComponent<Text>();
            LayoutElement pageTitleLayout = pageTitle.gameObject.AddComponent<LayoutElement>();
            pageTitleLayout.minHeight = 38;
            pageTitleLayout.preferredHeight = 42;
            pageTitleLayout.flexibleHeight = 0;

            GameObject identity = UIFactory.CreateHorizontalStack(screen.transform, "DeckIdentity", UIFactory.Panel, 8, 6);
            HorizontalLayoutGroup identityGroup = identity.GetComponent<HorizontalLayoutGroup>();
            identityGroup.childForceExpandWidth = false;
            identityGroup.childForceExpandHeight = false;
            identityGroup.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement identityLayout = identity.AddComponent<LayoutElement>();
            identityLayout.minHeight = 44;
            identityLayout.preferredHeight = 48;
            identityLayout.flexibleHeight = 0;

            Text nameLabel = UIFactory.CreateText(identity.transform, "DECK NAME", 14, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            LayoutElement nameLabelLayout = nameLabel.gameObject.AddComponent<LayoutElement>();
            nameLabelLayout.minWidth = 82;
            nameLabelLayout.preferredWidth = 90;
            nameLabelLayout.flexibleWidth = 0;

            deckNameInput = UIFactory.CreateInputField(identity.transform, "NAME YOUR DECK", string.Empty);
            deckNameInput.characterLimit = 24;
            deckNameInput.textComponent.fontSize = 17;
            if (deckNameInput.placeholder is Text namePlaceholder)
            {
                namePlaceholder.fontSize = 17;
            }
            LayoutElement inputLayout = deckNameInput.gameObject.GetComponent<LayoutElement>();
            inputLayout.minWidth = 220;
            inputLayout.preferredWidth = 270;
            inputLayout.flexibleWidth = 0;
            inputLayout.minHeight = 36;
            inputLayout.preferredHeight = 40;

            ConfigureCompactButton(UIFactory.CreateButton(identity.transform, "NEW EMPTY", BeginNewDeck, UIFactory.Blue), 112, 38);
            ConfigureCompactButton(UIFactory.CreateButton(identity.transform, "LOAD STARTER", LoadStarterDraft, UIFactory.PortalViolet), 126, 38);

            GameObject saved = UIFactory.CreateHorizontalStack(screen.transform, "SavedDecks", UIFactory.MenuInset, 8, 6);
            HorizontalLayoutGroup savedGroup = saved.GetComponent<HorizontalLayoutGroup>();
            savedGroup.childForceExpandWidth = false;
            savedGroup.childForceExpandHeight = false;
            savedGroup.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement savedLayout = saved.AddComponent<LayoutElement>();
            savedLayout.minHeight = 44;
            savedLayout.preferredHeight = 48;
            savedLayout.flexibleHeight = 0;
            Text savedLabel = UIFactory.CreateText(saved.transform, "SAVED DECKS", 14, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            LayoutElement savedLabelLayout = savedLabel.gameObject.AddComponent<LayoutElement>();
            savedLabelLayout.minWidth = 104;
            savedLabelLayout.preferredWidth = 112;
            savedLabelLayout.flexibleWidth = 0;
            savedDecksContent = UIFactory.CreateScrollContent(saved.transform, "SavedDeckStrip", true, out ScrollRect savedScroll, true);
            LayoutElement savedScrollLayout = savedScroll.GetComponent<LayoutElement>();
            savedScrollLayout.minHeight = 34;
            savedScrollLayout.preferredHeight = 36;
            savedScrollLayout.flexibleHeight = 0;
            savedScrollLayout.flexibleWidth = 1;

            GameObject status = UIFactory.CreateHorizontalStack(screen.transform, "BuilderStatus", Color.clear, 8, 0);
            HorizontalLayoutGroup statusGroup = status.GetComponent<HorizontalLayoutGroup>();
            statusGroup.childForceExpandWidth = false;
            LayoutElement statusLayout = status.AddComponent<LayoutElement>();
            statusLayout.minHeight = 22;
            statusLayout.preferredHeight = 24;
            statusLayout.flexibleHeight = 0;
            countText = UIFactory.CreateText(status.transform, string.Empty, 17, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            LayoutElement countLayout = countText.gameObject.AddComponent<LayoutElement>();
            countLayout.minWidth = 132;
            countLayout.preferredWidth = 146;
            messageText = UIFactory.CreateText(status.transform, "Drag or tap collection cards into the deck lane.", 14, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);
            LayoutElement messageLayout = messageText.gameObject.AddComponent<LayoutElement>();
            messageLayout.flexibleWidth = 1;

            GameObject columns = UIFactory.CreateHorizontalStack(screen.transform, "DeckColumns", Color.clear, 10, 0);
            LayoutElement columnsLayout = columns.AddComponent<LayoutElement>();
            columnsLayout.flexibleHeight = 1;

            GameObject collectionLane = UIFactory.CreateVerticalStack(columns.transform, "CollectionLane", UIFactory.Panel, 7, 8);
            LayoutElement collectionLayout = collectionLane.AddComponent<LayoutElement>();
            collectionLayout.flexibleWidth = 1.18f;
            UIFactory.CreateText(collectionLane.transform, $"MY COLLECTION  |  {CardCatalog.AllCards.Count} CARDS", 19, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);

            GameObject collectionToolbar = UIFactory.CreateHorizontalStack(collectionLane.transform, "CollectionToolbar", UIFactory.MenuInset, 6, 5);
            HorizontalLayoutGroup toolbarGroup = collectionToolbar.GetComponent<HorizontalLayoutGroup>();
            toolbarGroup.childForceExpandWidth = false;
            toolbarGroup.childForceExpandHeight = false;
            toolbarGroup.childAlignment = TextAnchor.MiddleLeft;
            LayoutElement toolbarLayout = collectionToolbar.AddComponent<LayoutElement>();
            toolbarLayout.minHeight = 42;
            toolbarLayout.preferredHeight = 44;
            toolbarLayout.flexibleHeight = 0;

            collectionSearchInput = UIFactory.CreateInputField(collectionToolbar.transform, "SEARCH CARDS", string.Empty);
            collectionSearchInput.gameObject.name = "CollectionSearch";
            collectionSearchInput.characterLimit = 40;
            collectionSearchInput.textComponent.fontSize = 14;
            if (collectionSearchInput.placeholder is Text searchPlaceholder)
            {
                searchPlaceholder.fontSize = 14;
            }
            LayoutElement searchLayout = collectionSearchInput.GetComponent<LayoutElement>();
            searchLayout.minWidth = 180;
            searchLayout.preferredWidth = 224;
            searchLayout.flexibleWidth = 1;
            searchLayout.minHeight = 32;
            searchLayout.preferredHeight = 34;
            collectionSearchInput.onValueChanged.AddListener(_ => RebuildCollection());

            CreateCollectionFilterButton(collectionToolbar.transform, "ALL", "ALL", 54);
            CreateCollectionFilterButton(collectionToolbar.transform, "ORIGINAL", GameConstants.Original, 88);
            CreateCollectionFilterButton(collectionToolbar.transform, "ITEM", GameConstants.Item, 60);
            cardsContent = UIFactory.CreateGridScrollContent(collectionLane.transform, "CollectionScroll", new Vector2(194f, 232f), 4, out _);

            GameObject deckLane = UIFactory.CreateVerticalStack(columns.transform, "DeckLane", UIFactory.MenuInset, 7, 8);
            LayoutElement deckLayout = deckLane.AddComponent<LayoutElement>();
            deckLayout.flexibleWidth = 0.82f;
            DeckBuilderDropZone laneDropZone = deckLane.AddComponent<DeckBuilderDropZone>();
            laneDropZone.Controller = this;
            deckLaneTitle = UIFactory.CreateText(deckLane.transform, string.Empty, 19, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            deckCompositionText = UIFactory.CreateText(deckLane.transform, string.Empty, 13, TextAnchor.MiddleLeft, UIFactory.MutedTextColor, FontStyle.Bold);
            LayoutElement compositionLayout = deckCompositionText.gameObject.AddComponent<LayoutElement>();
            compositionLayout.minHeight = 18;
            compositionLayout.preferredHeight = 20;
            compositionLayout.flexibleHeight = 0;
            deckContent = UIFactory.CreateScrollContent(deckLane.transform, "DeckScroll", false, out deckScroll);
            DeckBuilderDropZone scrollDropZone = deckScroll.gameObject.AddComponent<DeckBuilderDropZone>();
            scrollDropZone.Controller = this;

            GameObject actions = UIFactory.CreateHorizontalStack(screen.transform, "Actions", Color.clear, 10, 0);
            HorizontalLayoutGroup actionsGroup = actions.GetComponent<HorizontalLayoutGroup>();
            actionsGroup.childForceExpandWidth = false;
            actionsGroup.childForceExpandHeight = false;
            actionsGroup.childAlignment = TextAnchor.MiddleRight;
            LayoutElement actionsLayout = actions.AddComponent<LayoutElement>();
            actionsLayout.minHeight = 36;
            actionsLayout.preferredHeight = 40;
            actionsLayout.flexibleHeight = 0;
            ConfigureCompactButton(UIFactory.CreateButton(actions.transform, "SAVE DECK", SaveDeck, UIFactory.Green), 118, 36);
            ConfigureCompactButton(UIFactory.CreateButton(actions.transform, "DELETE", DeleteDeck, UIFactory.Red), 88, 36);
            ConfigureCompactButton(BackButton(actions.transform), 78, 36);

            PlayerDeckProfile active = PlayerDeckService.GetActiveDeck();
            if (active.id == PlayerDeckService.StarterDeckId)
            {
                BeginNewDeck();
            }
            else
            {
                LoadDeckForEditing(active);
            }
        }

        public bool CanAddCard()
        {
            return deckIds.Count < GameConstants.DeckSize;
        }

        public bool CanAddCard(string cardId)
        {
            CardDefinition card = CardCatalog.GetCard(cardId);
            return CanAddCard() && card != null && deckIds.Count(id => id == cardId) < PlayerDeckService.MaxCopies(card);
        }

        public void AddCardFromDrop(string cardId)
        {
            AddCard(cardId, true);
        }

        private static void ConfigureCompactButton(Button button, float width, float height)
        {
            if (button == null)
            {
                return;
            }

            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
                layout.flexibleWidth = 0;
                layout.minHeight = height;
                layout.preferredHeight = height;
                layout.flexibleHeight = 0;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.fontSize = 15;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 11;
                label.resizeTextMaxSize = 15;
            }
        }

        private void CreateCollectionFilterButton(Transform parent, string label, string filter, float width)
        {
            Button button = UIFactory.CreateButton(parent, label, () => SetCollectionFilter(filter), UIFactory.PanelAlt);
            button.gameObject.name = $"Filter_{filter}";
            ConfigureCompactButton(button, width, 34);
            collectionFilterButtons[filter] = button;
        }

        private void SetCollectionFilter(string filter)
        {
            collectionFilter = string.IsNullOrWhiteSpace(filter) ? "ALL" : filter;
            RefreshCollectionFilterButtons();
            RebuildCollection();
        }

        private void RefreshCollectionFilterButtons()
        {
            foreach (KeyValuePair<string, Button> pair in collectionFilterButtons)
            {
                bool selected = pair.Key == collectionFilter;
                Color color = selected ? UIFactory.Accent : UIFactory.PanelAlt;
                Image image = pair.Value.GetComponent<Image>();
                if (image != null)
                {
                    image.color = color;
                }

                ColorBlock colors = pair.Value.colors;
                colors.normalColor = color;
                colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
                colors.pressedColor = Color.Lerp(color, Color.black, 0.16f);
                pair.Value.colors = colors;
            }
        }

        private void RebuildLists()
        {
            countText.text = $"{deckIds.Count}/{GameConstants.DeckSize} CARDS";
            countText.color = deckIds.Count == GameConstants.DeckSize ? UIFactory.Green : UIFactory.Accent;
            deckLaneTitle.text = deckIds.Count == 0
                ? "YOUR DECK  |  EMPTY - DROP CARDS HERE"
                : $"YOUR DECK  |  {deckIds.Count}/{GameConstants.DeckSize}";

            int originals = CountDeckType(GameConstants.Original);
            int items = CountDeckType(GameConstants.Item);
            deckCompositionText.text = $"ORIGINALS {originals}   ITEMS {items}";

            RefreshCollectionFilterButtons();
            RebuildCollection();
            RebuildDeckContents();

            animatedSlot = -1;
            RebuildSavedDecks();
        }

        private int CountDeckType(string type)
        {
            return deckIds.Select(CardCatalog.GetCard).Count(card => card != null && card.IsType(type));
        }

        private void RebuildCollection()
        {
            if (cardsContent == null)
            {
                return;
            }

            UIFactory.ClearChildren(cardsContent);
            string search = collectionSearchInput == null ? string.Empty : collectionSearchInput.text.Trim();
            IEnumerable<CardDefinition> cards = CardCatalog.AllCards
                .Where(card => collectionFilter == "ALL" || card.IsType(collectionFilter))
                .Where(card => CardMatchesSearch(card, search))
                .OrderBy(card => TypeOrder(card.type))
                .ThenBy(card => card.name);

            foreach (CardDefinition card in cards)
            {
                CardDefinition captured = card;
                GameObject panel = UIFactory.CreateCardPanel(cardsContent, captured, () => AddCard(captured.id, false), false, "TAP OR DRAG TO ADD", true);
                Button button = panel.GetComponent<Button>();
                if (button != null)
                {
                    button.interactable = CanAddCard(captured.id);
                }

                DeckBuilderCardDrag drag = panel.AddComponent<DeckBuilderCardDrag>();
                drag.Controller = this;
                drag.Card = captured;
                CardInspectionTrigger inspection = panel.AddComponent<CardInspectionTrigger>();
                inspection.Card = captured;
            }
        }

        private static bool CardMatchesSearch(CardDefinition card, string search)
        {
            if (card == null)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return Contains(card.name, search)
                || Contains(card.type, search)
                || Contains(card.rarity, search)
                || Contains(card.laneAffinity, search)
                || Contains(card.effectText, search);
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int TypeOrder(string type)
        {
            if (type == GameConstants.Original)
            {
                return 0;
            }
            if (type == GameConstants.Companion)
            {
                return 1;
            }
            if (type == GameConstants.Item)
            {
                return 2;
            }
            return 3;
        }

        private void RebuildDeckContents()
        {
            if (deckContent == null)
            {
                return;
            }

            string animatedCardId = animatedSlot >= 0 && animatedSlot < deckIds.Count ? deckIds[animatedSlot] : string.Empty;
            UIFactory.ClearChildren(deckContent);
            CardDefinition[] cards = deckIds.Select(CardCatalog.GetCard).Where(card => card != null).ToArray();

            foreach (string type in new[] { GameConstants.Original, GameConstants.Companion, GameConstants.Item })
            {
                CardDefinition[] typeCards = cards.Where(card => card.IsType(type)).ToArray();
                if (typeCards.Length > 0)
                {
                    CreateDeckTypeGroup(type, typeCards, animatedCardId);
                }
            }

            string[] missingIds = deckIds.Where(id => CardCatalog.GetCard(id) == null).ToArray();
            if (missingIds.Length > 0)
            {
                GameObject warning = UIFactory.CreatePanel(deckContent, "MissingCardWarning", UIFactory.Red);
                LayoutElement warningLayout = warning.AddComponent<LayoutElement>();
                warningLayout.minHeight = 46;
                warningLayout.preferredHeight = 50;
                Text warningText = UIFactory.CreateText(warning.transform, $"MISSING CARD DATA: {string.Join(", ", missingIds)}", 13, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
                UIFactory.Stretch(warningText.rectTransform);
            }

            CreateDeckDropWell();
        }

        private void CreateDeckTypeGroup(string type, IEnumerable<CardDefinition> cards, string animatedCardId)
        {
            CardDefinition[] cardArray = cards.ToArray();
            GameObject group = UIFactory.CreateVerticalStack(deckContent, $"DeckGroup_{type}", UIFactory.Panel, 4, 5);
            LayoutElement groupLayout = group.AddComponent<LayoutElement>();
            groupLayout.minHeight = 42 + cardArray.Select(card => card.id).Distinct().Count() * 76;
            groupLayout.flexibleHeight = 0;
            DeckBuilderDropZone groupDropZone = group.AddComponent<DeckBuilderDropZone>();
            groupDropZone.Controller = this;

            GameObject header = UIFactory.CreateHorizontalStack(group.transform, $"{type}_HEADER", UIFactory.ColorForType(type), 5, 6);
            HorizontalLayoutGroup headerGroup = header.GetComponent<HorizontalLayoutGroup>();
            headerGroup.childForceExpandWidth = false;
            headerGroup.childForceExpandHeight = false;
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.minHeight = 30;
            headerLayout.preferredHeight = 32;
            headerLayout.flexibleHeight = 0;
            Text headerText = UIFactory.CreateText(header.transform, type, 14, TextAnchor.MiddleLeft, UIFactory.Ink, FontStyle.Bold);
            LayoutElement headerTextLayout = headerText.gameObject.AddComponent<LayoutElement>();
            headerTextLayout.flexibleWidth = 1;
            Text total = UIFactory.CreateText(header.transform, cardArray.Length.ToString(), 14, TextAnchor.MiddleRight, UIFactory.Ink, FontStyle.Bold);
            LayoutElement totalLayout = total.gameObject.AddComponent<LayoutElement>();
            totalLayout.minWidth = 30;
            totalLayout.flexibleWidth = 0;

            foreach (IGrouping<string, CardDefinition> cardGroup in cardArray.GroupBy(card => card.id).OrderBy(grouping => grouping.First().cost).ThenBy(grouping => grouping.First().name))
            {
                CardDefinition card = cardGroup.First();
                int quantity = cardGroup.Count();
                GameObject row = UIFactory.CreateHorizontalStack(group.transform, $"DeckCard_{card.id}", UIFactory.MenuInset, 6, 5);
                HorizontalLayoutGroup rowGroup = row.GetComponent<HorizontalLayoutGroup>();
                rowGroup.childForceExpandWidth = false;
                rowGroup.childForceExpandHeight = false;
                rowGroup.childAlignment = TextAnchor.MiddleLeft;
                LayoutElement rowLayout = row.AddComponent<LayoutElement>();
                rowLayout.minHeight = 68;
                rowLayout.preferredHeight = 72;
                rowLayout.flexibleHeight = 0;
                DeckBuilderDropZone rowDropZone = row.AddComponent<DeckBuilderDropZone>();
                rowDropZone.Controller = this;

                GameObject miniCard = UIFactory.CreateCardArtThumbnail(row.transform, card, 62, 58);
                CardInspectionTrigger inspection = miniCard.AddComponent<CardInspectionTrigger>();
                inspection.Card = card;

                Text details = UIFactory.CreateText(row.transform, $"{card.name}\nCOST {card.cost}  |  POW {card.power}  |  APP {card.appreciation}", 13, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
                LayoutElement detailsLayout = details.gameObject.AddComponent<LayoutElement>();
                detailsLayout.flexibleWidth = 1;

                Text quantityText = UIFactory.CreateText(row.transform, $"x{quantity}", 16, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
                LayoutElement quantityLayout = quantityText.gameObject.AddComponent<LayoutElement>();
                quantityLayout.minWidth = 38;
                quantityLayout.preferredWidth = 42;
                quantityLayout.flexibleWidth = 0;

                Button remove = UIFactory.CreateButton(row.transform, "-", () => RemoveCardById(card.id), UIFactory.Red);
                remove.gameObject.name = $"Remove_{card.id}";
                ConfigureCompactButton(remove, 36, 32);

                if (card.id == animatedCardId)
                {
                    StartCoroutine(AnimateDeckDrop(row.transform));
                }
            }
        }

        private void CreateDeckDropWell()
        {
            bool complete = deckIds.Count == GameConstants.DeckSize;
            GameObject well = UIFactory.CreateVerticalStack(deckContent, "DeckDropWell", complete ? UIFactory.Green : UIFactory.PortalViolet, 2, 8);
            LayoutElement wellLayout = well.AddComponent<LayoutElement>();
            wellLayout.minHeight = 70;
            wellLayout.preferredHeight = 76;
            wellLayout.flexibleHeight = 0;
            DeckBuilderDropZone dropZone = well.AddComponent<DeckBuilderDropZone>();
            dropZone.Controller = this;
            UIFactory.CreateText(well.transform, complete ? "DECK READY" : "DROP CARDS HERE", 17, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            UIFactory.CreateText(well.transform, complete ? $"{GameConstants.DeckSize} CARDS SELECTED" : $"{GameConstants.DeckSize - deckIds.Count} OPEN SLOTS", 13, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
        }

        private void RemoveCardById(string id)
        {
            int index = deckIds.FindLastIndex(cardId => cardId == id);
            RemoveCard(index);
        }

        private void RebuildSavedDecks()
        {
            if (savedDecksContent == null)
            {
                return;
            }

            UIFactory.ClearChildren(savedDecksContent);
            IReadOnlyList<PlayerDeckProfile> decks = PlayerDeckService.GetNamedDecks();
            if (decks.Count == 0)
            {
                UIFactory.CreateText(savedDecksContent, "No named decks yet", 15, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);
                return;
            }

            string activeId = PlayerDeckService.GetActiveDeck().id;
            foreach (PlayerDeckProfile deck in decks)
            {
                PlayerDeckProfile captured = deck;
                bool selected = captured.id == activeId;
                Button button = UIFactory.CreateButton(
                    savedDecksContent,
                    captured.name,
                    () => LoadDeckForEditing(captured),
                    selected ? UIFactory.Accent : UIFactory.PanelAlt);
                LayoutElement layout = button.gameObject.GetComponent<LayoutElement>();
                layout.minWidth = 118;
                layout.preferredWidth = Mathf.Clamp(captured.name.Length * 9 + 34, 118, 220);
                layout.minHeight = 30;
                layout.preferredHeight = 32;
                layout.flexibleWidth = 0;
                layout.flexibleHeight = 0;
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.fontSize = 14;
                    label.resizeTextForBestFit = true;
                    label.resizeTextMinSize = 10;
                    label.resizeTextMaxSize = 14;
                }
            }
        }

        private void AddCard(string id, bool fromDrop)
        {
            if (!CanAddCard())
            {
                SetMessage("Deck is already full.", UIFactory.Red);
                return;
            }

            CardDefinition card = CardCatalog.GetCard(id);
            if (card == null)
            {
                SetMessage($"Card '{id}' is unavailable.", UIFactory.Red);
                return;
            }

            int copyLimit = PlayerDeckService.MaxCopies(card);
            if (deckIds.Count(cardId => cardId == id) >= copyLimit)
            {
                SetMessage(copyLimit == 1 ? "Legendary, Mythic, and Crown cards are limited to one copy." : "Normal cards are limited to two copies.", UIFactory.Red);
                return;
            }

            deckIds.Add(id);
            animatedSlot = deckIds.Count - 1;
            SetMessage(fromDrop ? "Card dropped into deck." : "Card added to deck.", UIFactory.Accent);
            RebuildLists();
            StartCoroutine(ScrollDeckToLatest());
        }

        private void RemoveCard(int index)
        {
            if (index < 0 || index >= deckIds.Count)
            {
                return;
            }

            deckIds.RemoveAt(index);
            SetMessage("Card returned to collection.", UIFactory.MutedTextColor);
            RebuildLists();
        }

        private void BeginNewDeck()
        {
            editingDeckId = string.Empty;
            deckIds.Clear();
            if (deckNameInput != null)
            {
                deckNameInput.text = string.Empty;
            }
            SetMessage($"New empty deck ready. Name it and add {GameConstants.DeckSize} cards.", UIFactory.NeonCyan);
            RebuildLists();
        }

        private void LoadStarterDraft()
        {
            editingDeckId = string.Empty;
            deckIds.Clear();
            deckIds.AddRange(CardCatalog.StarterDeckIds());
            deckNameInput.text = "Starter Remix";
            SetMessage("Starter cards loaded as a new editable deck.", UIFactory.Accent);
            RebuildLists();
        }

        private void LoadDeckForEditing(PlayerDeckProfile deck)
        {
            if (deck == null || !PlayerDeckService.ValidateDeck(deck.cardIds))
            {
                SetMessage("That deck could not be loaded.", UIFactory.Red);
                return;
            }

            editingDeckId = deck.id;
            deckIds.Clear();
            deckIds.AddRange(deck.cardIds);
            deckNameInput.text = deck.name;
            PlayerDeckService.SelectDeck(deck.id, out _);
            SetMessage($"{deck.name} loaded and selected for battle.", UIFactory.Green);
            RebuildLists();
        }

        private void SaveDeck()
        {
            if (PlayerDeckService.SaveNamedDeck(editingDeckId, deckNameInput.text, deckIds, out PlayerDeckProfile saved, out string message))
            {
                editingDeckId = saved.id;
                deckNameInput.text = saved.name;
                SetMessage(message, UIFactory.Green);
            }
            else
            {
                SetMessage(message, UIFactory.Red);
            }
            RebuildLists();
        }

        private void DeleteDeck()
        {
            if (string.IsNullOrWhiteSpace(editingDeckId))
            {
                SetMessage("This new deck has not been saved yet.", UIFactory.Red);
                return;
            }

            if (PlayerDeckService.DeleteNamedDeck(editingDeckId, out string message))
            {
                BeginNewDeck();
                SetMessage(message, UIFactory.Accent);
            }
            else
            {
                SetMessage(message, UIFactory.Red);
            }
        }

        private void SetMessage(string message, Color color)
        {
            if (messageText == null)
            {
                return;
            }
            messageText.text = message;
            messageText.color = color;
        }

        private IEnumerator AnimateDeckDrop(Transform target)
        {
            const float duration = 0.18f;
            float elapsed = 0f;
            target.localScale = new Vector3(0.88f, 0.88f, 1f);
            while (target != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                target.localScale = Vector3.Lerp(new Vector3(0.88f, 0.88f, 1f), Vector3.one, eased);
                yield return null;
            }
            if (target != null)
            {
                target.localScale = Vector3.one;
            }
        }

        private IEnumerator ScrollDeckToLatest()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (deckScroll != null)
            {
                deckScroll.verticalNormalizedPosition = 0f;
            }
        }
    }
}
