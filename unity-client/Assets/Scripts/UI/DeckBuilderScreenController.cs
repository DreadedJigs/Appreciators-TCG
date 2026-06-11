using System.Collections.Generic;
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
        private Text countText;
        private Text messageText;
        private RectTransform cardsContent;
        private RectTransform deckContent;

        private void Start()
        {
            deckIds.AddRange(PlayerDeckService.LoadDeckIdsOrStarter());

            GameObject screen = CreateFullScreenStack("Deck Builder");
            countText = UIFactory.CreateText(screen.transform, string.Empty, 24, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            messageText = UIFactory.CreateText(screen.transform, "Build a 12-card deck and save it locally.", 20, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);

            GameObject columns = UIFactory.CreateHorizontalStack(screen.transform, "DeckColumns", Color.clear, 14, 0);
            LayoutElement columnsLayout = columns.AddComponent<LayoutElement>();
            columnsLayout.flexibleHeight = 1;

            GameObject left = UIFactory.CreateVerticalStack(columns.transform, "AvailableCards", UIFactory.Panel, 8, 10);
            UIFactory.CreateText(left.transform, "Available Cards", 26, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            cardsContent = UIFactory.CreateScrollContent(left.transform, "AvailableScroll", false, out _);

            GameObject right = UIFactory.CreateVerticalStack(columns.transform, "CurrentDeck", UIFactory.Panel, 8, 10);
            UIFactory.CreateText(right.transform, "Current Deck", 26, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            deckContent = UIFactory.CreateScrollContent(right.transform, "DeckScroll", false, out _);

            GameObject actions = UIFactory.CreateHorizontalStack(screen.transform, "Actions", Color.clear, 10, 0);
            UIFactory.CreateButton(actions.transform, "Save Deck", SaveDeck, UIFactory.Green);
            UIFactory.CreateButton(actions.transform, "Starter Deck", ResetToStarter, UIFactory.PanelAlt);
            BackButton(actions.transform);

            RebuildLists();
        }

        private void RebuildLists()
        {
            countText.text = $"{deckIds.Count}/{GameConstants.DeckSize} cards";

            UIFactory.ClearChildren(cardsContent);
            foreach (CardDefinition card in CardCatalog.AllCards)
            {
                string id = card.id;
                GameObject cardPanel = UIFactory.CreateCardPanel(cardsContent, card, () => AddCard(id), false, "Tap to add", true);
                Button addButton = cardPanel.GetComponent<Button>();
                if (addButton != null)
                {
                    addButton.interactable = deckIds.Count < GameConstants.DeckSize;
                }
            }

            UIFactory.ClearChildren(deckContent);
            for (int i = 0; i < deckIds.Count; i++)
            {
                int removeIndex = i;
                CardDefinition card = CardCatalog.GetCard(deckIds[i]);
                if (card == null)
                {
                    UIFactory.CreateButton(deckContent, $"{removeIndex + 1}. {deckIds[i]}\nRemove", () => RemoveCard(removeIndex), UIFactory.Red);
                    continue;
                }

                UIFactory.CreateCardPanel(deckContent, card, () => RemoveCard(removeIndex), false, $"{removeIndex + 1}. Tap to remove", true);
            }
        }

        private void AddCard(string id)
        {
            if (deckIds.Count >= GameConstants.DeckSize)
            {
                messageText.text = "Deck is already full.";
                return;
            }

            deckIds.Add(id);
            messageText.text = "Card added.";
            RebuildLists();
        }

        private void RemoveCard(int index)
        {
            if (index < 0 || index >= deckIds.Count)
            {
                return;
            }

            deckIds.RemoveAt(index);
            messageText.text = "Card removed.";
            RebuildLists();
        }

        private void ResetToStarter()
        {
            deckIds.Clear();
            deckIds.AddRange(CardCatalog.StarterDeckIds());
            messageText.text = "Starter deck loaded.";
            RebuildLists();
        }

        private void SaveDeck()
        {
            if (PlayerDeckService.SaveDeck(deckIds, out string message))
            {
                messageText.color = UIFactory.Accent;
            }
            else
            {
                messageText.color = UIFactory.Red;
            }

            messageText.text = message;
            RebuildLists();
        }
    }
}
