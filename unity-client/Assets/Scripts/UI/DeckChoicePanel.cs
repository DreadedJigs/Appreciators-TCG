using System;
using System.Linq;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class DeckChoicePanel : MonoBehaviour
    {
        private Transform optionsRoot;
        private Text selectedText;
        private Action<PlayerDeckProfile> onSelectionChanged;
        private bool selectionInteractable = true;

        public static DeckChoicePanel Create(Transform parent, string title, Action<PlayerDeckProfile> onSelectionChanged = null)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "DeckChoice", UIFactory.MenuInset, 5, 8);
            LayoutElement panelLayout = panel.AddComponent<LayoutElement>();
            panelLayout.minHeight = 84;
            panelLayout.preferredHeight = 92;
            panelLayout.flexibleHeight = 0;

            GameObject header = UIFactory.CreateHorizontalStack(panel.transform, "DeckChoiceHeader", Color.clear, 8, 0);
            HorizontalLayoutGroup headerGroup = header.GetComponent<HorizontalLayoutGroup>();
            headerGroup.childForceExpandWidth = false;
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 26;

            Text titleText = UIFactory.CreateText(header.transform, title.ToUpperInvariant(), 17, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            LayoutElement titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1;

            DeckChoicePanel control = panel.AddComponent<DeckChoicePanel>();
            control.selectedText = UIFactory.CreateText(header.transform, string.Empty, 16, TextAnchor.MiddleRight, UIFactory.Accent, FontStyle.Bold);
            control.onSelectionChanged = onSelectionChanged;

            RectTransform options = UIFactory.CreateScrollContent(panel.transform, "DeckOptions", true, out ScrollRect scrollRect, true);
            LayoutElement optionsLayout = scrollRect.GetComponent<LayoutElement>();
            optionsLayout.minHeight = 44;
            optionsLayout.preferredHeight = 48;
            optionsLayout.flexibleHeight = 0;
            control.optionsRoot = options;
            control.Refresh();
            return control;
        }

        public void Refresh()
        {
            if (optionsRoot == null)
            {
                return;
            }

            PlayerDeckProfile active = PlayerDeckService.GetActiveDeck();
            selectedText.text = $"SELECTED: {active.name}";
            UIFactory.ClearChildren(optionsRoot);

            foreach (PlayerDeckProfile deck in PlayerDeckService.GetSelectableDecks())
            {
                PlayerDeckProfile captured = deck;
                bool selected = captured.id == active.id;
                Button button = UIFactory.CreateButton(
                    optionsRoot,
                    $"{captured.name}  |  {captured.cardIds.Count} CARDS",
                    () => Select(captured.id),
                    selected ? UIFactory.Accent : UIFactory.PanelAlt);
                button.interactable = selectionInteractable;
                LayoutElement layout = button.gameObject.GetComponent<LayoutElement>();
                layout.minWidth = 190;
                layout.preferredWidth = Math.Max(190, captured.name.Length * 11 + 110);
                layout.minHeight = 38;
                layout.preferredHeight = 42;
                layout.flexibleWidth = 0;
                Text label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.fontSize = 15;
                }
            }
        }

        public void SetInteractable(bool interactable)
        {
            selectionInteractable = interactable;
            Refresh();
        }

        private void Select(string deckId)
        {
            if (!PlayerDeckService.SelectDeck(deckId, out string message))
            {
                Debug.LogError($"Could not select battle deck: {message}");
                return;
            }

            Refresh();
            onSelectionChanged?.Invoke(PlayerDeckService.GetActiveDeck());
        }
    }
}
