using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.Packs;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class MainMenuController : ScreenControllerBase
    {
        private GameObject mainPanel;
        private GameObject casualQueuePanel;
        private DeckChoicePanel casualDeckChoice;
        private Text casualQueueStatus;
        private Button ritualButton;
        private Text economyText;
        private RectTransform menuHeader;
        private RectTransform themeButtonRect;
        private RectTransform homeButtonRect;
        private RectTransform playColumnRect;
        private RectTransform growColumnRect;
        private RectTransform futureColumnRect;
        private Vector2Int lastResponsiveSize;
        private Text menuMarquee;
        private Button themeButton;
        private Button tutorialButton;
        private readonly List<Button> tutorialLockedButtons = new List<Button>();
        private readonly Dictionary<Button, string> tutorialLockedLabels = new Dictionary<Button, string>();

        private void Start()
        {
            RectTransform playmat = UIFactory.CreateBrandMenuRoot(Root);
            mainPanel = new GameObject("MainBoardMenu", typeof(RectTransform));
            mainPanel.transform.SetParent(playmat, false);
            UIFactory.Stretch(mainPanel.GetComponent<RectTransform>());

            GameObject header = UIFactory.CreateVerticalStack(mainPanel.transform, "MenuHeader", UIFactory.GlassPanel, 3, 12);
            menuHeader = header.GetComponent<RectTransform>();
            UIFactory.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0.24f, 0.785f), new Vector2(0.76f, 0.955f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(header, UIFactory.NeonCyan);
            UIFactory.ApplyBrandStarfield(header);
            menuMarquee = UIFactory.CreateText(header.transform, "A P P R E C I A T O R S   T C G", 39, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.CreateText(header.transform, "LEARN  •  BUILD  •  GROW APPRECIATION", 17, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            economyText = UIFactory.CreateText(header.transform, "SYNCING UNOPENED PACKS AND APPRECIATION SHARDS...", 17, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);

            string themeLabel = ThemeService.IsDark ? "SWITCH TO LIGHT MODE" : "SWITCH TO DARK MODE";
            themeButton = UIFactory.CreateButton(mainPanel.transform, themeLabel, ToggleTheme, UIFactory.Blue);
            themeButtonRect = themeButton.GetComponent<RectTransform>();
            UIFactory.SetAnchors(themeButton.GetComponent<RectTransform>(), new Vector2(0.78f, 0.925f), new Vector2(0.985f, 0.985f), Vector2.zero, Vector2.zero);

            Button homeButton = UIFactory.CreateButton(mainPanel.transform, "HOME / QR", OpenHomeScreen, UIFactory.Accent);
            homeButtonRect = homeButton.GetComponent<RectTransform>();
            UIFactory.SetAnchors(homeButtonRect, new Vector2(0.015f, 0.925f), new Vector2(0.220f, 0.985f), Vector2.zero, Vector2.zero);

            GameObject playColumn = CreateMenuColumn(mainPanel.transform, "PLAY", "Commit one card, command one lane.", new Rect(0.055f, 0.285f, 0.275f, 0.43f));
            playColumnRect = playColumn.GetComponent<RectTransform>();
            tutorialButton = UIFactory.CreateButton(playColumn.transform, "TURN TUTORIAL", StartTutorial, UIFactory.Blue);
            RegisterTutorialLocked(UIFactory.CreateButton(playColumn.transform, "PLAY CASUAL", OpenCasualQueue, UIFactory.Green), "PLAY CASUAL");
            RegisterTutorialLocked(UIFactory.CreateButton(playColumn.transform, "INVITE 1V1", () => SceneManager.LoadScene("InviteMatchScene"), UIFactory.Accent), "INVITE 1V1");
            RegisterTutorialLocked(UIFactory.CreateButton(playColumn.transform, "BOSS BATTLES", () => SceneManager.LoadScene("BossBattleScene"), UIFactory.Red), "BOSS BATTLES");

            GameObject growColumn = CreateMenuColumn(mainPanel.transform, "GROW", "Open packs, collect, and prepare.", new Rect(0.3625f, 0.285f, 0.275f, 0.43f));
            growColumnRect = growColumn.GetComponent<RectTransform>();
            ritualButton = UIFactory.CreateButton(growColumn.transform, "APPRECIATION RITUAL\nSYNCING UNOPENED PACKS...", () => SceneManager.LoadScene("PackOpeningScene"), UIFactory.PortalViolet);
            RegisterTutorialLocked(ritualButton, "APPRECIATION RITUAL");
            RegisterTutorialLocked(UIFactory.CreateButton(growColumn.transform, "COLLECTION", () => SceneManager.LoadScene("CollectionScene"), UIFactory.PanelAlt), "COLLECTION");
            RegisterTutorialLocked(UIFactory.CreateButton(growColumn.transform, "DECK BUILDER", () => SceneManager.LoadScene("DeckBuilderScene"), UIFactory.PanelAlt), "DECK BUILDER");

            GameObject futureColumn = CreateMenuColumn(mainPanel.transform, "CONNECT", "Alpha access and upcoming formats.", new Rect(0.67f, 0.285f, 0.275f, 0.43f));
            futureColumnRect = futureColumn.GetComponent<RectTransform>();
            RegisterTutorialLocked(UIFactory.CreateButton(futureColumn.transform, "WALLET / WEB3", () => SceneManager.LoadScene("Web3MockScene"), UIFactory.Blue), "WALLET / WEB3");
            CreateDisabled(futureColumn.transform, "RANKED — COMING SOON");

            casualQueuePanel = CreateCenteredPanel("Casual Queue", 38);
            UIFactory.CreateText(casualQueuePanel.transform, "CHOOSE YOUR BATTLE DECK", 23, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            casualDeckChoice = DeckChoicePanel.Create(casualQueuePanel.transform, "Available Decks", deck =>
            {
                casualQueueStatus.text = $"{deck.name} ready. {deck.cardIds.Count} cards validated.";
                casualQueueStatus.color = UIFactory.Green;
            });
            casualQueueStatus = UIFactory.CreateText(
                casualQueuePanel.transform,
                string.Empty,
                21,
                TextAnchor.MiddleCenter,
                UIFactory.MutedTextColor,
                FontStyle.Bold);
            GameObject queueActions = UIFactory.CreateHorizontalStack(casualQueuePanel.transform, "CasualQueueActions", Color.clear, 10, 0);
            UIFactory.CreateButton(queueActions.transform, "QUEUE WITH SELECTED DECK", QueueCasual, UIFactory.Green);
            UIFactory.CreateButton(queueActions.transform, "EDIT DECKS", () => SceneManager.LoadScene("DeckBuilderScene"), UIFactory.Blue);
            UIFactory.CreateButton(queueActions.transform, "CANCEL", CloseCasualQueue, UIFactory.PanelAlt);
            casualQueuePanel.SetActive(false);
            RefreshTutorialGate();
            ApplyResponsiveMenuLayout(true);
            StartCoroutine(RefreshAccountEconomy());
        }

        private void LateUpdate()
        {
            ApplyResponsiveMenuLayout(false);
        }

        private void ApplyResponsiveMenuLayout(bool force)
        {
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (!force && size == lastResponsiveSize) return;
            lastResponsiveSize = size;

            bool phone = ResponsiveCanvasScaler.IsPhoneLayout;
            SetRect(menuHeader, phone ? new Rect(0.12f, 0.785f, 0.68f, 0.19f) : new Rect(0.24f, 0.785f, 0.52f, 0.17f));
            SetRect(themeButtonRect, phone ? new Rect(0.82f, 0.865f, 0.17f, 0.105f) : new Rect(0.78f, 0.925f, 0.205f, 0.06f));
            SetRect(homeButtonRect, phone ? new Rect(0.015f, 0.865f, 0.17f, 0.105f) : new Rect(0.015f, 0.925f, 0.205f, 0.06f));
            SetRect(playColumnRect, phone ? new Rect(0.025f, 0.055f, 0.305f, 0.705f) : new Rect(0.055f, 0.285f, 0.275f, 0.43f));
            SetRect(growColumnRect, phone ? new Rect(0.3475f, 0.055f, 0.305f, 0.705f) : new Rect(0.3625f, 0.285f, 0.275f, 0.43f));
            SetRect(futureColumnRect, phone ? new Rect(0.67f, 0.055f, 0.305f, 0.705f) : new Rect(0.67f, 0.285f, 0.275f, 0.43f));

            if (menuMarquee != null)
            {
                menuMarquee.fontSize = phone ? 32 : 39;
                menuMarquee.resizeTextForBestFit = phone;
                menuMarquee.resizeTextMinSize = phone ? 18 : 32;
                menuMarquee.resizeTextMaxSize = phone ? 32 : 39;
                menuMarquee.verticalOverflow = VerticalWrapMode.Truncate;
            }
            ConfigureCompactButtonText(themeButton, phone, 16);
            ConfigureCompactButtonText(homeButtonRect == null ? null : homeButtonRect.GetComponent<Button>(), phone, 16);

            if (!phone) return;
            foreach (RectTransform column in new[] { playColumnRect, growColumnRect, futureColumnRect })
            {
                VerticalLayoutGroup group = column == null ? null : column.GetComponent<VerticalLayoutGroup>();
                if (group != null)
                {
                    group.spacing = 4f;
                    group.padding = new RectOffset(8, 8, 8, 8);
                }
                if (column == null) continue;
                foreach (Button button in column.GetComponentsInChildren<Button>(true))
                {
                    LayoutElement layout = button.GetComponent<LayoutElement>();
                    if (layout == null) continue;
                    layout.minHeight = 58f;
                    layout.preferredHeight = 64f;
                }
            }
        }

        private static void ConfigureCompactButtonText(Button button, bool compact, int compactSize)
        {
            Text label = button == null ? null : button.GetComponentInChildren<Text>();
            if (label == null) return;
            label.fontSize = compact ? compactSize : 25;
            label.resizeTextForBestFit = compact;
            label.resizeTextMinSize = compact ? 11 : 18;
            label.resizeTextMaxSize = compact ? compactSize : 25;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void SetRect(RectTransform rect, Rect normalized)
        {
            if (rect == null) return;
            UIFactory.SetAnchors(rect, new Vector2(normalized.xMin, normalized.yMin), new Vector2(normalized.xMax, normalized.yMax), Vector2.zero, Vector2.zero);
        }

        private IEnumerator RefreshAccountEconomy()
        {
            string playerId = LocalSaveSystem.LoadOrCreatePlayerId();
            BackendApiClient apiClient = gameObject.AddComponent<BackendApiClient>();
            PackInventoryResponse response = null;
            string requestError = null;
            yield return apiClient.GetPackInventory(playerId, value => response = value, error => requestError = error);

            int unopenedPacks;
            int shards;
            if (response?.inventory != null)
            {
                new PackInventoryService(new PackSaveService()).ReplaceWithAuthoritativeSnapshot(response.inventory);
                LocalSaveSystem.ApplyAccountProgress(response.inventory.progress);
                unopenedPacks = response.inventory.packs?.Sum(entry => entry == null ? 0 : Math.Max(0, entry.count)) ?? 0;
                shards = response.inventory.appreciationShards;
                economyText.color = UIFactory.Green;
            }
            else
            {
                PackInventoryService local = new PackInventoryService(new PackSaveService());
                unopenedPacks = local.State.packs?.Sum(entry => entry == null ? 0 : Math.Max(0, entry.count)) ?? 0;
                shards = local.AppreciationShards;
                economyText.color = UIFactory.Accent;
                Debug.LogWarning($"[Account] Main-menu inventory sync deferred: {requestError}");
            }

            economyText.text = $"{LocalSaveSystem.LoadPlayerName().ToUpperInvariant()}  •  {unopenedPacks} UNOPENED PACKS  •  {shards:N0} APPRECIATION SHARDS";
            SetButtonLabel(ritualButton, $"APPRECIATION RITUAL\n{unopenedPacks} UNOPENED PACK{(unopenedPacks == 1 ? string.Empty : "S")}");
            RefreshTutorialGate();
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button == null ? null : button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = 20;
            }
        }

        private void RegisterTutorialLocked(Button button, string unlockedLabel)
        {
            if (button == null) return;
            tutorialLockedButtons.Add(button);
            tutorialLockedLabels[button] = unlockedLabel;
        }

        private void RefreshTutorialGate()
        {
            // The tutorial is optional; no play, collection, or wallet action
            // is locked behind it.
            bool completed = true;
            foreach (Button button in tutorialLockedButtons)
            {
                if (button == null) continue;
                button.interactable = completed;
                if (completed && button == ritualButton) continue;
                if (tutorialLockedLabels.TryGetValue(button, out string label))
                {
                    SetButtonLabel(button, completed ? label : $"{label}\nLOCKED • COMPLETE TUTORIAL");
                }
            }

            if (tutorialButton != null)
            {
                SetButtonLabel(tutorialButton, completed ? "TURN TUTORIAL\nREPLAY ANYTIME" : "TURN TUTORIAL\nSTART HERE");
            }
        }

        private static GameObject CreateMenuColumn(Transform parent, string title, string subtitle, Rect normalizedRect)
        {
            GameObject column = UIFactory.CreateVerticalStack(parent, $"{title}MenuColumn", UIFactory.GlassPanel, 9, 14);
            UIFactory.SetAnchors(column.GetComponent<RectTransform>(),
                new Vector2(normalizedRect.xMin, normalizedRect.yMin),
                new Vector2(normalizedRect.xMax, normalizedRect.yMax),
                Vector2.zero,
                Vector2.zero);
            Color accent = title == "PLAY" ? UIFactory.Green : title == "GROW" ? UIFactory.Accent : UIFactory.NeonCyan;
            UIFactory.MakeDimensionalPanel(column, accent);
            UIFactory.CreateText(column.transform, title, 27, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.CreateText(column.transform, subtitle, 17, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Normal);
            return column;
        }

        private static void ToggleTheme()
        {
            ThemeService.Toggle();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private static void OpenHomeScreen()
        {
            // LoginScene is the branded home screen and owns the mobile-access QR.
            SceneManager.LoadScene("LoginScene");
        }

        private void OpenCasualQueue()
        {
            PlayerDeckProfile active = PlayerDeckService.GetActiveDeck();
            casualDeckChoice.Refresh();
            casualQueueStatus.text = $"{active.name} selected. Choose another deck or queue now.";
            casualQueueStatus.color = UIFactory.MutedTextColor;
            mainPanel.SetActive(false);
            casualQueuePanel.SetActive(true);
        }

        private void CloseCasualQueue()
        {
            casualQueuePanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void QueueCasual()
        {
            PlayerDeckProfile active = PlayerDeckService.GetActiveDeck();
            if (!PlayerDeckService.ValidateDeck(active.cardIds))
            {
                casualQueueStatus.text = "Selected deck is invalid. Choose another deck.";
                casualQueueStatus.color = UIFactory.Red;
                return;
            }

            LocalSaveSystem.ClearPendingMatchContext();
            SceneManager.LoadScene("MatchScene");
        }

        private void StartTutorial()
        {
            LocalSaveSystem.SavePendingMatchContext("Tutorial", string.Empty, string.Empty, "Guide", string.Empty, string.Empty);
            SceneManager.LoadScene("MatchScene");
        }

        private static void CreateDisabled(Transform parent, string label)
        {
            Button button = UIFactory.CreateButton(parent, label, () => { }, UIFactory.PanelAlt);
            button.interactable = false;
        }
    }
}
