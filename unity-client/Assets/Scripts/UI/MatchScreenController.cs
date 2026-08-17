using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Audio;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class MatchScreenController : ScreenControllerBase
    {
        private enum TutorialStep
        {
            Objective,
            TurnSequence,
            Draw,
            Learn,
            BuildOrDiscard,
            HarmfulDiscard,
            EndTurn,
            Discard,
            PublicDiscard,
            BoardPresence,
            Combat,
            BuffsAndNerfs,
            DirectAttack,
            AutoAttack,
            GatherGrowth,
            Winning,
            Complete
        }

        private const int MatchHandCardWidth = 127;
        private const int MatchHandCardHeight = 190;
        // Phone hands retain the official 2:3 silhouette while preserving a
        // clear gutter to the battlefield and outer playmat edge.
        private const int MobileHandCardWidth = 102;
        private const int MobileHandCardHeight = 153;
        private const int PlayerBoardCardWidth = 92;
        private const int PlayerBoardCardHeight = 138;
        private const int OpponentBoardCardWidth = PlayerBoardCardWidth;
        private const int OpponentBoardCardHeight = PlayerBoardCardHeight;
        // The discard well is deliberately smaller than a hand card. This keeps a
        // three-card public stack inside the well on a landscape phone without
        // colliding with the adjacent Appreciation meter.
        private const int DiscardCardWidth = 72;
        private const int DiscardCardHeight = 102;
        private const int DiscardCardArtHeight = 38;

        private BattleGame game;
        private BackendApiClient apiClient;
        private Text messageText;
        private Button endTurnButton;
        private Button quitButton;
        private GameObject quitDialog;
        private Text quitDialogText;
        private Button quitConfirmButton;
        private Button quitCancelButton;
        private RectTransform opponentHudContent;
        private RectTransform playerHudContent;
        private RectTransform resourceContent;
        private Text opponentLeaderText;
        private Text playerLeaderText;
        private RectTransform opponentHandContent;
        private RectTransform lanesContent;
        private RectTransform battlefieldDropRect;
        private GameObject battlefieldDropHint;
        private RectTransform handContent;
        private RectTransform opponentShardContent;
        private RectTransform playerShardContent;
        private RectTransform opponentDiscardContent;
        private RectTransform playerDiscardContent;
        private GameObject discardStackOverlay;
        private RectTransform playmatRoot;
        private ScrollRect handScrollRect;
        private BattleCombatAnimationController combatAnimator;
        private BattleAudioController battleAudio;
        private string opponentLabel = "AI";
        private string matchIntro;
        private bool inviteMatch;
        private bool tutorialMatch;
        private string inviteCode;
        private string invitePlayerId;
        private string invitePlayerRole;
        private InviteMatchState latestInviteState;
        private int lastInviteActionSequence;
        private int lastInviteStateVersion = -1;
        private int localActionCounter;
        private int selectedHandIndex = -1;
        private bool localInviteTurnEnded;
        private bool remoteInviteTurnEnded;
        private int remotePlayedCards;
        private int lastClickedHandIndex = -1;
        private float lastClickedHandTime = -1f;
        private string pendingTerminationDecision = "request";
        private bool terminationRequestInFlight;
        private bool leavingMatch;
        private bool combatAnimating;
        private string matchRewardId;
        private string matchMode = "Casual";
        private RectTransform matchTableRoot;
        private GameObject playChoiceDialog;
        private int pendingPlayChoiceHandIndex = -1;
        private TutorialStep tutorialStep;
        private GameObject tutorialPanel;
        private GameObject tutorialHighlight;
        private Text tutorialTitle;
        private Text tutorialCaption;
        private Text tutorialBody;
        private Button tutorialNextButton;
        private Button tutorialPreviousButton;
        private Button tutorialRestartButton;
        private Button tutorialSkipButton;
        private RectTransform tutorialPanelRect;
        private Vector2 tutorialExpandedMin = new Vector2(0.055f, 0.300f);
        private Vector2 tutorialExpandedMax = new Vector2(0.945f, 0.960f);
        private Vector2 tutorialCollapsedMin = new Vector2(0.255f, 0.018f);
        private Vector2 tutorialCollapsedMax = new Vector2(0.745f, 0.112f);
        private bool tutorialAwaitingTally;
        private bool tutorialCoreDemonstrated;
        private PhaseAnnouncementController phaseAnnouncer;
        private CombatPlannerOverlay combatPlanner;
        private List<BattleAttackOrder> pendingCombatPlan;
        private AppreciationLiquidMeter playerAppreciationMeter;
        private AppreciationLiquidMeter opponentAppreciationMeter;
        private GameObject discardConfirmationDialog;
        private readonly HashSet<int> seenBoardCardIds = new HashSet<int>();
        private RectTransform deckDrawSource;
        private RectTransform opponentDeckDrawSource;
        private bool drawPresentationActive;
        private int presentedPlayerHandCount;
        private int presentedOpponentHandCount;
        private int activeDrawSlot = -1;
        private Coroutine tutorialAutoplayRoutine;
        private bool tutorialStepPlaying;
        private bool tutorialDiscardDemonstrated;
        private bool tutorialCombatDemonstrated;
        private bool tutorialTallyDemonstrated;
        private Button phaseModeButton;
        private Button matchVolumeButton;
        private Button phaseNextButton;
        private Outline phaseNextGlowOutline;
        private Shadow phaseNextGlowShadow;
        private Button matchThemeButton;
        private GameObject matchSettingsMenu;
        private GameObject battleLedgerPanel;
        private Text battleLedgerText;
        private ScrollRect battleLedgerScroll;
        private readonly List<string> battleLedgerEntries = new List<string>();
        private bool autoPlayPhases = true;
        private bool phaseAdvanceRequested;
        private bool waitingForPhaseAdvance;
        private bool mandatoryDiscardReviewActive;
        private Vector2Int lastResponsiveLayoutSize;

        private void Start()
        {
            string pendingMode = LocalSaveSystem.LoadPendingMatchMode();
            matchMode = string.IsNullOrWhiteSpace(pendingMode) ? "Casual" : pendingMode;
            inviteCode = LocalSaveSystem.LoadPendingInviteCode();
            string pendingMatchId = LocalSaveSystem.LoadPendingMatchId();
            matchRewardId = string.IsNullOrWhiteSpace(pendingMatchId) ? $"local_{Guid.NewGuid():N}" : pendingMatchId;
            string pendingOpponent = LocalSaveSystem.LoadPendingOpponentName();
            invitePlayerId = LocalSaveSystem.LoadPendingPlayerId();
            invitePlayerRole = LocalSaveSystem.LoadPendingPlayerRole();
            inviteMatch = pendingMode == "Invite 1v1";
            tutorialMatch = pendingMode == "Tutorial";
            LocalSaveSystem.ClearPendingMatchContext();

            if (inviteMatch)
            {
                apiClient = gameObject.AddComponent<BackendApiClient>();
                opponentLabel = string.IsNullOrWhiteSpace(pendingOpponent) ? "Opponent" : pendingOpponent;
                matchIntro = $"Invite 1v1 started. Code {inviteCode}. Match {pendingMatchId}.";
            }
            else if (tutorialMatch)
            {
                opponentLabel = "Guide";
                matchIntro = "Guided first turn. Follow the highlighted table areas.";
            }

            PlayerDeckProfile selectedDeck = PlayerDeckService.GetActiveDeck();
            game = new BattleGame(LocalSaveSystem.LoadPlayerName(), CardCatalog.GetCards(selectedDeck.cardIds), inviteMatch);
            matchIntro = inviteMatch || tutorialMatch
                ? $"{matchIntro} Deck: {selectedDeck.name}."
                : $"Casual battle. Deck: {selectedDeck.name}.";
            matchIntro = $"{matchIntro}\nDraw 2, choose 1, Battle, Appreciate. First to {GameConstants.AppreciationVictoryTarget} Appreciation wins.";
            game.Start();
            drawPresentationActive = true;
            presentedPlayerHandCount = 0;
            presentedOpponentHandCount = 0;
            combatAnimator = gameObject.AddComponent<BattleCombatAnimationController>();
            battleAudio = gameObject.AddComponent<BattleAudioController>();

            playmatRoot = UIFactory.CreateOfficialPlaymatRoot(Root);
            GameObject screen = CreateMatchTable(playmatRoot, inviteMatch ? "Invite 1v1" : tutorialMatch ? "Tutorial" : "Casual");
            matchTableRoot = screen.GetComponent<RectTransform>();
            phaseAnnouncer = gameObject.AddComponent<PhaseAnnouncementController>();
            phaseAnnouncer.Configure(matchTableRoot);
            CreatePlaymatControlZones(screen.transform);
            CreateDeckPileVisual(screen.transform);
            CreateLeaderReadouts(screen.transform);
            opponentDiscardContent = CreateAnchoredHorizontal(
                screen.transform,
                "OpponentDiscardCards",
                new Vector2(0.818f, 0.724f),
                new Vector2(0.929f, 0.938f),
                -48,
                TextAnchor.MiddleCenter);
            playerDiscardContent = CreateAnchoredHorizontal(
                screen.transform,
                "PlayerDiscardCards",
                new Vector2(0.062f, 0.052f),
                new Vector2(0.178f, 0.258f),
                -48,
                TextAnchor.MiddleCenter);
            ConfigureDiscardStackControl(playerDiscardContent, true);
            ConfigureDiscardStackControl(opponentDiscardContent, false);
            opponentHandContent = CreateAnchoredHorizontal(
                screen.transform,
                "OpponentHandCards",
                new Vector2(0.383f, 0.795f),
                new Vector2(0.800f, 0.950f),
                3,
                TextAnchor.MiddleCenter);
            opponentHudContent = CreateAnchoredHorizontal(
                screen.transform,
                "OpponentHudSlot",
                new Vector2(0.385f, 0.735f),
                new Vector2(0.760f, 0.795f),
                0,
                TextAnchor.MiddleCenter);

            lanesContent = CreateAnchoredRoot(
                screen.transform,
                "Lanes",
                Vector2.zero,
                Vector2.one);

            messageText = UIFactory.CreateText(screen.transform, string.Empty, 12, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.SetAnchors(messageText.rectTransform, new Vector2(0.245f, 0.265f), new Vector2(0.790f, 0.292f), Vector2.zero, Vector2.zero);
            messageText.resizeTextForBestFit = true;
            messageText.resizeTextMinSize = 8;
            messageText.resizeTextMaxSize = 12;
            messageText.gameObject.SetActive(false);

            playerHudContent = CreateAnchoredHorizontal(
                screen.transform,
                "PlayerHudSlot",
                new Vector2(0.235f, 0.292f),
                new Vector2(0.790f, 0.355f),
                0,
                TextAnchor.MiddleCenter);
            CreateAppreciationMeters(screen.transform);

            handContent = UIFactory.CreateScrollContent(screen.transform, "Hand", true, out handScrollRect, true);
            UIFactory.SetAnchors(handScrollRect.GetComponent<RectTransform>(), new Vector2(0.390f, 0.042f), new Vector2(0.800f, 0.258f), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup handLayout = handContent.GetComponent<HorizontalLayoutGroup>();
            if (handLayout != null)
            {
                handLayout.spacing = 8;
                handLayout.padding = new RectOffset(8, 8, 2, 2);
            }
            Image handImage = handScrollRect.GetComponent<Image>();
            if (handImage != null)
            {
                handImage.color = Color.clear;
            }
            Image handViewportImage = handScrollRect.viewport != null
                ? handScrollRect.viewport.GetComponent<Image>()
                : null;
            if (handViewportImage != null)
            {
                handViewportImage.color = Color.clear;
                handViewportImage.raycastTarget = false;
            }

            Outline handOutline = handScrollRect.GetComponent<Outline>();
            if (handOutline != null)
            {
                handOutline.enabled = false;
            }
            Shadow handShadow = handScrollRect.GetComponent<Shadow>();
            if (handShadow != null)
            {
                handShadow.enabled = false;
            }
            // Cards intentionally rise beyond the shallow hand slot. Do not let the
            // generic scroll-view viewport crop their top or bottom edges.
            foreach (Mask mask in handScrollRect.GetComponentsInChildren<Mask>(true))
            {
                mask.enabled = false;
            }
            foreach (RectMask2D mask in handScrollRect.GetComponentsInChildren<RectMask2D>(true))
            {
                mask.enabled = false;
            }

            quitButton = UIFactory.CreateButton(screen.transform, "OPTIONS", ToggleMatchSettingsMenu, UIFactory.PortalViolet);
            // OPTIONS lives on the left control position; NEXT owns the right-side
            // action position so it reads as the primary continuation affordance.
            UIFactory.SetAnchors(quitButton.GetComponent<RectTransform>(), new Vector2(0.260f, 0.008f), new Vector2(0.365f, 0.052f), Vector2.zero, Vector2.zero);

            CreateMatchSettingsControls(screen.transform);
            RecordBattleLedger(matchIntro);
            CreateFigmaBoardChrome(screen.transform);
            ApplyResponsiveFigmaLayout(true);

            CreateQuitDialog();

            if (tutorialMatch)
            {
                CreateTutorialGuide();
            }

            UpdateScreen();
            StartCoroutine(AnnounceOpeningPhases());

            if (inviteMatch)
            {
                InvokeRepeating(nameof(PollInviteActions), 1.0f, 1.25f);
            }
        }

        private void UpdateScreen()
        {
            ApplyResponsiveFigmaLayout(false);
            UpdateHud();
            RecordBattleLedger(game.LastMessage);
            messageText.text = game.LastMessage;

            if (endTurnButton != null)
            {
                endTurnButton.interactable = !tutorialMatch && !combatAnimating && !game.IsComplete && (!inviteMatch || !localInviteTurnEnded);
            }
            if (phaseNextButton != null)
            {
                phaseNextButton.gameObject.SetActive(!tutorialMatch);
                // A paced phase is deliberately waiting inside the combat coroutine.
                // NEXT must remain usable even though combatAnimating is true.
                phaseNextButton.interactable = waitingForPhaseAdvance ||
                    (!combatAnimating && !game.IsComplete && (!inviteMatch || !localInviteTurnEnded));
                SetButtonText(phaseNextButton, waitingForPhaseAdvance ? "NEXT PHASE" : "NEXT");
            }

            if (quitButton != null)
            {
                quitButton.interactable = !leavingMatch && !terminationRequestInFlight;
            }

            UpdateOpponentHand();
            UpdateDiscardMats();

            UIFactory.ClearChildren(lanesContent);
            CreateLane(LaneType.Community);

            UIFactory.ClearChildren(handContent);
            int visiblePlayerCards = drawPresentationActive
                ? Mathf.Clamp(presentedPlayerHandCount, 0, game.Player.Hand.Count)
                : game.Player.Hand.Count;
            for (int i = 0; i < visiblePlayerCards; i++)
            {
                int handIndex = i;
                CardDefinition card = game.Player.Hand[i];
                CardInspectionTrigger inspectionTrigger = null;
                GameObject cardPanel = UIFactory.CreateMatchHandCardPanel(
                    handContent,
                    card,
                    () =>
                    {
                        if (inspectionTrigger == null || !inspectionTrigger.SuppressesCardPlay)
                        {
                            HandleHandCardClick(handIndex);
                        }
                    },
                    selectedHandIndex == i,
                    $"ATK {card.GetAttack()}  |  DEF {card.GetDefense()}");
                ApplyHandCardSizing(cardPanel);
                CreateCombatStatsBadge(cardPanel.transform, card);
                Button handButton = cardPanel.GetComponent<Button>();
                if (handButton != null && tutorialMatch)
                {
                    handButton.enabled = false;
                }
                inspectionTrigger = cardPanel.AddComponent<CardInspectionTrigger>();
                inspectionTrigger.Card = card;
                MatchHandCardInput dragInput = cardPanel.AddComponent<MatchHandCardInput>();
                dragInput.Controller = this;
                dragInput.HandIndex = handIndex;
                dragInput.Card = card;
                dragInput.enabled = !tutorialMatch;
                UiCardMotion motion = cardPanel.AddComponent<UiCardMotion>();
                motion.ConfigureHandPosition(i, visiblePlayerCards, false);
                motion.ConfigureInteractionScale(1.02f, 1.04f);
                motion.SetSelected(selectedHandIndex == i);
                if (drawPresentationActive && i == activeDrawSlot)
                {
                    motion.ConfigureDrawFromDeck(deckDrawSource, false);
                }
            }
        }

        private IEnumerator AnnounceOpeningPhases()
        {
            if (tutorialMatch) yield break;
            yield return PlayDrawSequence(true);
        }

        private IEnumerator PlayDrawSequence(bool announceLearn)
        {
            drawPresentationActive = true;
            presentedPlayerHandCount = 0;
            presentedOpponentHandCount = 0;
            activeDrawSlot = -1;
            UpdateScreen();
            yield return PlayPacedPhase(BattleTurnPhase.Draw);

            int drawCount = Mathf.Max(game.Player.Hand.Count, game.Opponent.Hand.Count);
            drawCount = Mathf.Min(GameConstants.CardsDrawnPerTurn, drawCount);
            for (int slot = 0; slot < drawCount; slot++)
            {
                activeDrawSlot = slot;
                presentedPlayerHandCount = Mathf.Min(slot + 1, game.Player.Hand.Count);
                presentedOpponentHandCount = Mathf.Min(slot + 1, game.Opponent.Hand.Count);
                UpdateScreen();
                UiAudioService.PlayCardDraw();
                yield return new WaitForSecondsRealtime(ThemeService.ReducedMotion ? 0.18f : 0.88f);
            }

            activeDrawSlot = -1;
            drawPresentationActive = false;
            UpdateScreen();
            if (announceLearn)
            {
                yield return PlayPacedPhase(BattleTurnPhase.Learn);
            }
        }

        private void CreateMatchSettingsControls(Transform parent)
        {
            phaseNextButton = UIFactory.CreateButton(parent, "NEXT PHASE  ▶", AdvancePausedPhase, UIFactory.Green);
            phaseNextButton.onClick.RemoveAllListeners();
            phaseNextButton.onClick.AddListener(HandleNextButton);
            SetButtonText(phaseNextButton, "NEXT");
            RectTransform endTurnRect = endTurnButton == null ? null : endTurnButton.GetComponent<RectTransform>();
            UIFactory.SetAnchors(
                phaseNextButton.GetComponent<RectTransform>(),
                endTurnRect == null ? new Vector2(0.755f, 0.012f) : endTurnRect.anchorMin,
                endTurnRect == null ? new Vector2(0.865f, 0.060f) : endTurnRect.anchorMax,
                Vector2.zero,
                Vector2.zero);
            phaseNextGlowOutline = phaseNextButton.GetComponent<Outline>() ?? phaseNextButton.gameObject.AddComponent<Outline>();
            phaseNextGlowShadow = phaseNextButton.GetComponent<Shadow>() ?? phaseNextButton.gameObject.AddComponent<Shadow>();
            phaseNextGlowOutline.enabled = true;
            phaseNextGlowOutline.effectColor = new Color(UIFactory.NeonCyan.r, UIFactory.NeonCyan.g, UIFactory.NeonCyan.b, 0.62f);
            phaseNextGlowOutline.effectDistance = new Vector2(3.5f, -3.5f);
            phaseNextGlowShadow.enabled = false;
            phaseNextButton.gameObject.SetActive(true);

            matchSettingsMenu = UIFactory.CreateVerticalStack(parent, "StarSettingsMenu", UIFactory.GlassPanel, 10, 16);
            UIFactory.SetAnchors(matchSettingsMenu.GetComponent<RectTransform>(), new Vector2(0.345f, 0.080f), new Vector2(0.655f, 0.920f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(matchSettingsMenu, UIFactory.NeonCyan);
            Text optionsTitle = UIFactory.CreateText(matchSettingsMenu.transform, "OPTIONS", 25, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            LayoutElement optionsTitleLayout = optionsTitle.gameObject.AddComponent<LayoutElement>();
            optionsTitleLayout.minHeight = 30;
            optionsTitleLayout.preferredHeight = 34;
            Text optionsSubtitle = UIFactory.CreateText(matchSettingsMenu.transform, "Display, audio, pacing, and match controls", 14, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
            LayoutElement optionsSubtitleLayout = optionsSubtitle.gameObject.AddComponent<LayoutElement>();
            optionsSubtitleLayout.minHeight = 24;
            optionsSubtitleLayout.preferredHeight = 28;
            matchThemeButton = UIFactory.CreateButton(matchSettingsMenu.transform, ThemeService.IsDark ? "THEME: DARK" : "THEME: LIGHT", ToggleMatchTheme, UIFactory.PortalViolet);
            float savedVolume = PlayerPrefs.GetFloat("appreciators_master_volume", 1f);
            AudioListener.volume = Mathf.Clamp01(savedVolume);
            matchVolumeButton = UIFactory.CreateButton(matchSettingsMenu.transform, VolumeLabel(), CycleMatchVolume, UIFactory.Green);
            phaseModeButton = UIFactory.CreateButton(matchSettingsMenu.transform, "▶  PHASES: AUTO", TogglePhasePacing, UIFactory.Blue);
            phaseModeButton.gameObject.SetActive(!tutorialMatch);
            UIFactory.CreateButton(matchSettingsMenu.transform, "BATTLE LEDGER", OpenBattleLedger, UIFactory.Accent);
            UIFactory.CreateButton(matchSettingsMenu.transform, "QUIT TO MAIN MENU", OpenQuitFromOptions, UIFactory.Red);
            UIFactory.CreateButton(matchSettingsMenu.transform, "CLOSE", ToggleMatchSettingsMenu, UIFactory.PanelAlt);
            matchSettingsMenu.SetActive(false);

            CreateBattleLedgerPanel(parent);
        }

        private void CreateBattleLedgerPanel(Transform parent)
        {
            battleLedgerPanel = UIFactory.CreatePanel(parent, "BattleLedgerOverlay", new Color(0.005f, 0.008f, 0.025f, 0.78f));
            UIFactory.Stretch(battleLedgerPanel.GetComponent<RectTransform>());

            GameObject frame = UIFactory.CreateVerticalStack(
                battleLedgerPanel.transform,
                "BattleLedgerFrame",
                ThemeService.IsDark ? new Color(0.025f, 0.018f, 0.105f, 0.985f) : new Color(0.97f, 0.95f, 0.86f, 0.99f),
                10,
                18);
            UIFactory.SetAnchors(frame.GetComponent<RectTransform>(), new Vector2(0.16f, 0.10f), new Vector2(0.84f, 0.90f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(frame, UIFactory.NeonCyan);
            UIFactory.CreateText(frame.transform, "BATTLE LEDGER", 30, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.CreateText(
                frame.transform,
                "Round history, chosen effects, Battle results, and Appreciation changes",
                16,
                TextAnchor.MiddleCenter,
                UIFactory.MutedTextColor,
                FontStyle.Bold);

            RectTransform ledgerContent = UIFactory.CreateScrollContent(frame.transform, "BattleLedgerScroll", false, out battleLedgerScroll);
            ledgerContent.offsetMin = Vector2.zero;
            ledgerContent.offsetMax = Vector2.zero;
            battleLedgerText = UIFactory.CreateText(ledgerContent, string.Empty, 17, TextAnchor.UpperLeft, UIFactory.TextColor, FontStyle.Normal);
            battleLedgerText.horizontalOverflow = HorizontalWrapMode.Wrap;
            battleLedgerText.verticalOverflow = VerticalWrapMode.Overflow;
            battleLedgerText.lineSpacing = 1.08f;
            ContentSizeFitter fitter = battleLedgerText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UIFactory.CreateButton(frame.transform, "BACK TO OPTIONS", CloseBattleLedger, UIFactory.PortalViolet);
            battleLedgerPanel.SetActive(false);
        }

        private void OpenBattleLedger()
        {
            if (matchSettingsMenu != null) matchSettingsMenu.SetActive(false);
            RefreshBattleLedger();
            battleLedgerPanel.SetActive(true);
            battleLedgerPanel.transform.SetAsLastSibling();
            battleAudio?.PlayCardSelected();
        }

        private void CloseBattleLedger()
        {
            if (battleLedgerPanel != null) battleLedgerPanel.SetActive(false);
            if (matchSettingsMenu != null)
            {
                matchSettingsMenu.SetActive(true);
                matchSettingsMenu.transform.SetAsLastSibling();
            }
        }

        private void RecordBattleLedger(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string normalized = message.Trim();
            if (battleLedgerEntries.Count > 0 && battleLedgerEntries[battleLedgerEntries.Count - 1].EndsWith(normalized, StringComparison.Ordinal)) return;

            string phase = game == null ? "SETUP" : PhaseAnnouncementController.GetPhaseLabel(game.Phase);
            int turn = game == null ? 1 : game.Turn;
            battleLedgerEntries.Add($"ROUND {turn}  •  {phase}\n{normalized}");
            if (battleLedgerEntries.Count > 80) battleLedgerEntries.RemoveAt(0);
            if (battleLedgerPanel != null && battleLedgerPanel.activeSelf) RefreshBattleLedger();
        }

        private void RefreshBattleLedger()
        {
            if (battleLedgerText == null) return;
            battleLedgerText.text = battleLedgerEntries.Count == 0
                ? "No battle actions recorded yet."
                : string.Join("\n\n", battleLedgerEntries);
            Canvas.ForceUpdateCanvases();
            if (battleLedgerScroll != null) battleLedgerScroll.verticalNormalizedPosition = 0f;
        }

        private void ToggleMatchSettingsMenu()
        {
            if (matchSettingsMenu == null) return;
            bool show = !matchSettingsMenu.activeSelf;
            matchSettingsMenu.SetActive(show);
            if (show)
            {
                matchSettingsMenu.transform.SetAsLastSibling();
                battleAudio?.PlayCardSelected();
            }
        }

        private void ToggleMatchTheme()
        {
            ThemeService.Toggle();
            SetButtonText(matchThemeButton, ThemeService.IsDark ? "THEME: DARK" : "THEME: LIGHT");
            ApplyLivePlaymatTheme();
        }

        private string VolumeLabel()
        {
            return $"VOLUME: {Mathf.RoundToInt(AudioListener.volume * 100f)}%";
        }

        private void CycleMatchVolume()
        {
            float current = AudioListener.volume;
            float next = current > 0.75f ? 0.5f : current > 0.25f ? 0f : 1f;
            AudioListener.volume = next;
            PlayerPrefs.SetFloat("appreciators_master_volume", next);
            PlayerPrefs.Save();
            SetButtonText(matchVolumeButton, VolumeLabel());
            battleAudio?.PlayCardSelected();
        }

        private void OpenQuitFromOptions()
        {
            if (matchSettingsMenu != null) matchSettingsMenu.SetActive(false);
            OpenQuitDialog();
        }

        private void ApplyLivePlaymatTheme()
        {
            ApplyNativeBoardTheme();
            if (playmatRoot == null) return;
            Transform artTransform = playmatRoot.Find("PlaymatArt");
            Image art = artTransform == null ? null : artTransform.GetComponent<Image>();
            if (art != null)
            {
                art.color = ThemeService.IsDark ? new Color(0.58f, 0.50f, 0.68f, 1f) : Color.white;
            }

            Transform washTransform = playmatRoot.Find("DarkModeWash");
            GameObject wash;
            if (washTransform == null)
            {
                wash = new GameObject("DarkModeWash", typeof(RectTransform), typeof(Image));
                wash.transform.SetParent(playmatRoot, false);
                UIFactory.Stretch(wash.GetComponent<RectTransform>());
                Image washImage = wash.GetComponent<Image>();
                washImage.color = new Color(0.018f, 0.012f, 0.085f, 0.26f);
                washImage.raycastTarget = false;
                wash.transform.SetSiblingIndex(1);
            }
            else
            {
                wash = washTransform.gameObject;
            }
            wash.SetActive(ThemeService.IsDark);
        }

        private void TogglePhasePacing()
        {
            autoPlayPhases = !autoPlayPhases;
            SetButtonText(phaseModeButton, autoPlayPhases ? "▶  PHASES: AUTO" : "⏸  PHASES: PAUSED");
            if (autoPlayPhases && waitingForPhaseAdvance && !mandatoryDiscardReviewActive)
            {
                phaseAdvanceRequested = true;
            }
            ShowMatStatus(autoPlayPhases
                ? "Phase autoplay enabled. The loop will stop when your decision is needed."
                : "Phase pause enabled. Automatic phases wait for NEXT PHASE.");
            if (matchSettingsMenu != null) matchSettingsMenu.SetActive(false);
        }

        private void AdvancePausedPhase()
        {
            phaseAdvanceRequested = true;
        }

        private void HandleNextButton()
        {
            if (waitingForPhaseAdvance)
            {
                AdvancePausedPhase();
                return;
            }

            if (game != null && !game.IsComplete && !game.Player.HasCommittedCardThisTurn &&
                !game.Player.CommitSkippedThisTurn && game.Player.Hand.Count > 0)
            {
                ShowEarlyEndChoiceDialog();
                return;
            }

            EndTurn();
        }

        private void ShowEarlyEndChoiceDialog()
        {
            ClosePlayChoiceDialogImmediate();
            playChoiceDialog = UIFactory.CreatePanel(Root, "EarlyEndChoiceDialog", new Color(0.005f, 0.008f, 0.025f, 0.72f));
            UIFactory.Stretch(playChoiceDialog.GetComponent<RectTransform>());
            playChoiceDialog.transform.SetAsLastSibling();

            GameObject panel = UIFactory.CreateVerticalStack(
                playChoiceDialog.transform,
                "EarlyEndChoicePanel",
                new Color(0.025f, 0.035f, 0.12f, 0.98f),
                12,
                18);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.27f, 0.20f), new Vector2(0.73f, 0.80f), Vector2.zero, Vector2.zero);
            UIFactory.AddNeonFrame(panel, UIFactory.Accent, 0.95f);
            UIFactory.CreateText(panel.transform, "END TURN EARLY", 24, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.CreateText(
                panel.transform,
                "Both cards will be discarded. Choose the one whose Discard ability takes effect.",
                16,
                TextAnchor.MiddleCenter,
                UIFactory.MutedTextColor,
                FontStyle.Bold);

            for (int index = 0; index < game.Player.Hand.Count; index++)
            {
                CardDefinition card = game.Player.Hand[index];
                string cardId = card.id;
                Button choice = UIFactory.CreateButton(
                    panel.transform,
                    $"{card.name.ToUpperInvariant()}\n{card.GetDiscardEffect()}",
                    () => ResolveEarlyEndChoice(cardId),
                    index == 0 ? UIFactory.Blue : UIFactory.PortalViolet);
                SetChoiceButtonHeight(choice, 84);
            }

            Button cancel = UIFactory.CreateButton(panel.transform, "KEEP PLAYING", CancelPlayChoice, UIFactory.PanelAlt);
            SetChoiceButtonHeight(cancel, 48);
        }

        private void ResolveEarlyEndChoice(string cardId)
        {
            int index = game.Player.Hand.FindIndex(card => card.id == cardId);
            string message = string.Empty;
            if (index < 0 || !game.TryDiscardCard(OwnerSide.Player, index, out message))
            {
                battleAudio?.PlayInvalid();
                ShowMatStatus(string.IsNullOrWhiteSpace(message) ? "That Discard ability cannot resolve now." : message);
                return;
            }

            int discardedWithoutEffect = game.DiscardRemainingHandWithoutEffects(OwnerSide.Player);
            ClosePlayChoiceDialogImmediate();
            ShowMatStatus($"{message} {discardedWithoutEffect} other card discarded without resolving its ability.");
            UpdateScreen();
            EndTurn();
        }

        private IEnumerator PlayPacedPhase(BattleTurnPhase phase)
        {
            if (phaseAnnouncer != null)
            {
                yield return phaseAnnouncer.PlayPhase(phase);
            }

            if (tutorialMatch || autoPlayPhases || phase == BattleTurnPhase.Learn ||
                phase == BattleTurnPhase.Discard ||
                phase == BattleTurnPhase.BuildOrDiscard || phase == BattleTurnPhase.Complete)
            {
                yield break;
            }

            waitingForPhaseAdvance = true;
            phaseAdvanceRequested = false;
            if (phaseNextButton != null)
            {
                phaseNextButton.gameObject.SetActive(true);
                phaseNextButton.interactable = true;
                SetButtonText(phaseNextButton, "NEXT PHASE");
                phaseNextButton.transform.SetAsLastSibling();
            }
            ShowMatStatus($"{phase.ToString().ToUpperInvariant()} paused. Click NEXT PHASE to continue.");
            while (!phaseAdvanceRequested && !autoPlayPhases)
            {
                yield return null;
            }
            waitingForPhaseAdvance = false;
            phaseAdvanceRequested = false;
            if (phaseNextButton != null)
            {
                phaseNextButton.gameObject.SetActive(true);
                SetButtonText(phaseNextButton, "NEXT");
            }
        }

        private static void CreateCombatStatsBadge(Transform cardPanel, CardDefinition card)
        {
            GameObject badge = UIFactory.CreatePanel(cardPanel, "CombatStatsBadge", new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.90f));
            RectTransform rect = badge.GetComponent<RectTransform>();
            UIFactory.SetAnchors(rect, new Vector2(0.05f, 0.015f), new Vector2(0.95f, 0.115f), Vector2.zero, Vector2.zero);
            Text text = UIFactory.CreateText(
                badge.transform,
                $"ATK {card.GetAttack()}   •   DEF {card.GetDefense()}",
                10,
                TextAnchor.MiddleCenter,
                UIFactory.Accent,
                FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 7;
            text.resizeTextMaxSize = 10;
            text.raycastTarget = false;
        }

        private GameObject CreateMatchTable(Transform parent, string modeLabel)
        {
            GameObject table = new GameObject("MatchTable", typeof(RectTransform));
            table.transform.SetParent(parent, false);
            UIFactory.Stretch(table.GetComponent<RectTransform>());

            Text mode = UIFactory.CreateText(table.transform, modeLabel.ToUpperInvariant(), 13, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.SetAnchors(mode.rectTransform, new Vector2(0.900f, 0.645f), new Vector2(0.975f, 0.690f), Vector2.zero, Vector2.zero);
            return table;
        }

        private RectTransform CreateAnchoredHorizontal(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int spacing,
            TextAnchor alignment)
        {
            GameObject row = UIFactory.CreateHorizontalStack(parent, name, Color.clear, spacing, 0);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = alignment;
            UIFactory.SetAnchors(row.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            return row.GetComponent<RectTransform>();
        }

        private static RectTransform CreateAnchoredRoot(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            UIFactory.SetAnchors(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            return rect;
        }

        private void CreatePlaymatControlZones(Transform parent)
        {
            // The printed three-star rails are the native settings affordance from
            // either player's viewing side. Both open the same playmat menu.
            UIFactory.CreatePlaymatZoneButton(parent, "PlayerStarMenuZone", new Rect(0.012f, 0.036f, 0.046f, 0.239f), ToggleMatchSettingsMenu);
            UIFactory.CreatePlaymatZoneButton(parent, "OpponentStarMenuZone", new Rect(0.942f, 0.711f, 0.046f, 0.241f), ToggleMatchSettingsMenu);
            // Keep the top controls exactly mirrored with the player side. The
            // old zones were left over from the pre-mirror board and made the
            // opponent deck/discard tap areas conflict with the Appreciation well.
            UIFactory.CreatePlaymatZoneButton(parent, "OpponentDeckZone", new Rect(0.880f, 0.720f, 0.095f, 0.185f), () => ShowMatStatus($"Opponent deck: {game.Opponent.DrawPile.Count} cards."));
            UIFactory.CreatePlaymatZoneButton(parent, "OpponentAbilityZone", new Rect(0.383f, 0.711f, 0.417f, 0.241f), () => ShowMatStatus($"Opponent ability: {game.Opponent.Leader?.Name} - {game.Opponent.Leader?.RulesText}"));
            UIFactory.CreatePlaymatZoneButton(parent, "OpponentDiscardZone", new Rect(0.025f, 0.720f, 0.115f, 0.185f), () => ShowMatStatus($"Opponent discard: {game.Opponent.DiscardPile.Count} cards."));

            UIFactory.CreatePlaymatZoneButton(parent, "PlayerDiscardZone", new Rect(0.054f, 0.036f, 0.134f, 0.239f), () => ShowMatStatus($"Your discard: {game.Player.DiscardPile.Count} cards."));
            UIFactory.CreatePlaymatZoneButton(parent, "PlayerAbilityZone", new Rect(0.383f, 0.038f, 0.417f, 0.237f), UseLeaderAbility);

            GameObject endTurnZone = UIFactory.CreatePlaymatZoneButton(
                parent,
                "END TURN",
                new Rect(0.809f, 0.036f, 0.127f, 0.239f),
                EndTurn);
            endTurnButton = endTurnZone.GetComponent<Button>();
        }

        private void CreateDeckPileVisual(Transform parent)
        {
            opponentDeckDrawSource = CreateDeckPile(parent, "OpponentFaceDownDeck", new Rect(0.890f, 0.735f, 0.080f, 0.165f), true);
            deckDrawSource = CreateDeckPile(parent, "PlayerFaceDownDeck", new Rect(0.890f, 0.100f, 0.080f, 0.165f), false);
        }

        private static RectTransform CreateDeckPile(Transform parent, string name, Rect anchors, bool opponent)
        {
            GameObject pile = new GameObject(name, typeof(RectTransform));
            pile.transform.SetParent(parent, false);
            RectTransform pileRect = pile.GetComponent<RectTransform>();
            UIFactory.SetAnchors(pileRect, new Vector2(anchors.xMin, anchors.yMin), new Vector2(anchors.xMax, anchors.yMax), Vector2.zero, Vector2.zero);

            RectTransform topCard = null;
            for (int layer = 0; layer < 3; layer++)
            {
                GameObject back = UIFactory.CreateCardBackPanel(pile.transform, string.Empty, 94, 140);
                back.name = layer == 2 ? "TopDrawCard" : $"DeckCardLayer{layer + 1}";
                RectTransform rect = back.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(94f, 140f);
                rect.anchoredPosition = new Vector2(layer * 3f - 5f, layer * 3f - 4f);
                rect.localRotation = Quaternion.Euler(0f, 0f, -3f + layer * 1.7f);
                foreach (Graphic graphic in back.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
                if (layer == 2)
                {
                    topCard = rect;
                }
            }

            pileRect.localRotation = Quaternion.Euler(0f, 0f, opponent ? 180f : 0f);
            return topCard;
        }

        private void CreateLeaderReadouts(Transform parent)
        {
            opponentLeaderText = UIFactory.CreateText(parent, string.Empty, 11, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.SetAnchors(opponentLeaderText.rectTransform, new Vector2(0.464f, 0.724f), new Vector2(0.760f, 0.780f), Vector2.zero, Vector2.zero);
            opponentLeaderText.resizeTextForBestFit = true;
            opponentLeaderText.resizeTextMinSize = 8;
            opponentLeaderText.resizeTextMaxSize = 11;
            opponentLeaderText.raycastTarget = false;

            playerLeaderText = UIFactory.CreateText(parent, string.Empty, 12, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.SetAnchors(playerLeaderText.rectTransform, new Vector2(0.464f, 0.048f), new Vector2(0.760f, 0.108f), Vector2.zero, Vector2.zero);
            playerLeaderText.resizeTextForBestFit = true;
            playerLeaderText.resizeTextMinSize = 8;
            playerLeaderText.resizeTextMaxSize = 12;
            playerLeaderText.raycastTarget = false;
        }

        private void HandlePlayerResourceMatClick()
        {
            if (selectedHandIndex >= 0)
            {
                DiscardHandCard(selectedHandIndex);
                return;
            }

            ShowMatStatus("Select one of your two cards, then choose Discard to reveal and resolve its public Discard Effect.");
        }

        private void ShowMatStatus(string message)
        {
            RecordBattleLedger(message);
            if (messageText != null)
            {
                messageText.text = message;
            }
        }

        private void CreateTutorialGuide()
        {
            tutorialHighlight = new GameObject("TutorialHighlight", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Outline));
            tutorialHighlight.transform.SetParent(Root, false);
            Image highlightImage = tutorialHighlight.GetComponent<Image>();
            highlightImage.color = new Color(UIFactory.Accent.r, UIFactory.Accent.g, UIFactory.Accent.b, 0.07f);
            highlightImage.raycastTarget = false;
            Outline outline = tutorialHighlight.GetComponent<Outline>();
            outline.effectColor = UIFactory.Accent;
            outline.effectDistance = new Vector2(5f, -5f);

            tutorialPanel = UIFactory.CreateVerticalStack(
                Root,
                "TutorialGuide",
                new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.96f),
                8,
                14);
            tutorialPanelRect = tutorialPanel.GetComponent<RectTransform>();
            UIFactory.SetAnchors(
                tutorialPanelRect,
                tutorialExpandedMin,
                tutorialExpandedMax,
                Vector2.zero,
                Vector2.zero);
            UIFactory.AddNeonFrame(tutorialPanel, UIFactory.NeonCyan, 0.96f);
            tutorialTitle = UIFactory.CreateText(tutorialPanel.transform, string.Empty, 38, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            LayoutElement titleLayout = tutorialTitle.gameObject.AddComponent<LayoutElement>();
            titleLayout.minHeight = 58;
            titleLayout.preferredHeight = 64;
            tutorialCaption = UIFactory.CreateText(tutorialPanel.transform, string.Empty, 24, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            LayoutElement captionLayout = tutorialCaption.gameObject.AddComponent<LayoutElement>();
            captionLayout.minHeight = 30;
            captionLayout.preferredHeight = 34;
            tutorialCaption.gameObject.SetActive(false);
            tutorialBody = UIFactory.CreateText(tutorialPanel.transform, string.Empty, 46, TextAnchor.MiddleLeft, UIFactory.Cream, FontStyle.Bold);
            LayoutElement bodyLayout = tutorialBody.gameObject.AddComponent<LayoutElement>();
            bodyLayout.flexibleHeight = 1f;
            tutorialBody.resizeTextForBestFit = true;
            tutorialBody.resizeTextMinSize = 22;
            tutorialBody.resizeTextMaxSize = 46;
            tutorialBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            tutorialBody.verticalOverflow = VerticalWrapMode.Truncate;
            tutorialBody.lineSpacing = 0.90f;
            GameObject navigation = UIFactory.CreateHorizontalStack(tutorialPanel.transform, "TutorialNavigation", Color.clear, 5, 0);
            tutorialPreviousButton = UIFactory.CreateButton(navigation.transform, "BACK", ShowPreviousTutorialExplanation, UIFactory.PanelAlt);
            tutorialNextButton = UIFactory.CreateButton(navigation.transform, "NEXT - PLAY STEP", AdvanceTutorial, UIFactory.Blue);
            tutorialRestartButton = UIFactory.CreateButton(navigation.transform, "RESTART", RestartTutorial, UIFactory.PortalViolet);
            tutorialSkipButton = UIFactory.CreateButton(navigation.transform, "SKIP", SkipTutorial, UIFactory.PanelAlt);
            foreach (Button button in new[] { tutorialPreviousButton, tutorialNextButton, tutorialRestartButton, tutorialSkipButton })
            {
                SetChoiceButtonHeight(button, 64);
            }

            tutorialCoreDemonstrated = false;
            SetTutorialStep(TutorialStep.Objective);
            StartCoroutine(PulseTutorialHighlight());
        }

        private void AdvanceTutorial()
        {
            if (tutorialStep >= TutorialStep.Winning)
            {
                FinishTutorial();
                return;
            }

            StartTutorialAutoplay();
        }

        private void ShowPreviousTutorialExplanation()
        {
            if (tutorialStep <= TutorialStep.Objective) return;
            NavigateTutorial((TutorialStep)Mathf.Max(0, (int)tutorialStep - 1));
        }

        private void NavigateTutorial(TutorialStep step)
        {
            if (tutorialAutoplayRoutine != null)
            {
                StopCoroutine(tutorialAutoplayRoutine);
                tutorialAutoplayRoutine = null;
            }
            tutorialStepPlaying = false;
            phaseAnnouncer?.Clear();
            SetTutorialPanelExpandedImmediate(true);
            if (step > TutorialStep.Draw && drawPresentationActive)
            {
                drawPresentationActive = false;
                activeDrawSlot = -1;
                presentedPlayerHandCount = game.Player.Hand.Count;
                presentedOpponentHandCount = game.Opponent.Hand.Count;
                UpdateScreen();
            }
            SetTutorialStep(step);
        }

        private void StartTutorialAutoplay()
        {
            if (!tutorialMatch || tutorialStep == TutorialStep.Complete)
            {
                return;
            }
            tutorialAutoplayRoutine = StartCoroutine(RunTutorialAutoplay());
        }

        private IEnumerator RunTutorialAutoplay()
        {
            if (!tutorialMatch || tutorialStep == TutorialStep.Complete)
            {
                yield break;
            }

            TutorialStep playing = tutorialStep;
            tutorialStepPlaying = true;
            SetTutorialNavigationInteractable(false);
            yield return AnimateTutorialPanel(false);
            yield return PlayTutorialStep(playing);

            if (!tutorialMatch || tutorialStep != playing)
            {
                tutorialStepPlaying = false;
                tutorialAutoplayRoutine = null;
                yield break;
            }

            if (playing == TutorialStep.Winning)
            {
                tutorialCoreDemonstrated = true;
                LocalSaveSystem.SaveTutorialProgress((int)playing, true);
                tutorialSkipButton.gameObject.SetActive(true);
            }
            else
            {
                SetTutorialStep((TutorialStep)((int)playing + 1));
            }

            yield return AnimateTutorialPanel(true);
            tutorialStepPlaying = false;
            SetTutorialNavigationInteractable(true);
            tutorialAutoplayRoutine = null;
        }

        private void SetTutorialNavigationInteractable(bool interactable)
        {
            if (tutorialPreviousButton != null)
                tutorialPreviousButton.interactable = tutorialStep > TutorialStep.Objective;
            if (tutorialNextButton != null)
                tutorialNextButton.interactable = interactable;
            if (tutorialRestartButton != null)
                tutorialRestartButton.interactable = true;
            if (tutorialSkipButton != null)
                tutorialSkipButton.interactable = interactable;
        }

        private IEnumerator AnimateTutorialPanel(bool expand)
        {
            if (tutorialPanelRect == null)
            {
                yield break;
            }

            if (expand)
            {
                tutorialTitle.gameObject.SetActive(true);
                if (tutorialCaption != null && !string.IsNullOrWhiteSpace(tutorialCaption.text)) tutorialCaption.gameObject.SetActive(true);
                tutorialBody.gameObject.SetActive(true);
            }
            else
            {
                SetButtonText(tutorialNextButton, "PLAYING STEP...");
            }

            Vector2 startMin = tutorialPanelRect.anchorMin;
            Vector2 startMax = tutorialPanelRect.anchorMax;
            Vector2 targetMin = expand ? tutorialExpandedMin : tutorialCollapsedMin;
            Vector2 targetMax = expand ? tutorialExpandedMax : tutorialCollapsedMax;
            float duration = ThemeService.ReducedMotion ? 0.08f : 0.24f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                tutorialPanelRect.anchorMin = Vector2.Lerp(startMin, targetMin, t);
                tutorialPanelRect.anchorMax = Vector2.Lerp(startMax, targetMax, t);
                tutorialPanelRect.offsetMin = Vector2.zero;
                tutorialPanelRect.offsetMax = Vector2.zero;
                yield return null;
            }

            tutorialPanelRect.anchorMin = targetMin;
            tutorialPanelRect.anchorMax = targetMax;
            tutorialPanelRect.offsetMin = Vector2.zero;
            tutorialPanelRect.offsetMax = Vector2.zero;
            if (!expand)
            {
                tutorialTitle.gameObject.SetActive(false);
                if (tutorialCaption != null) tutorialCaption.gameObject.SetActive(false);
                tutorialBody.gameObject.SetActive(false);
            }
        }

        private void SetTutorialPanelExpandedImmediate(bool expanded)
        {
            if (tutorialPanelRect == null) return;
            tutorialPanelRect.anchorMin = expanded ? tutorialExpandedMin : tutorialCollapsedMin;
            tutorialPanelRect.anchorMax = expanded ? tutorialExpandedMax : tutorialCollapsedMax;
            tutorialPanelRect.offsetMin = Vector2.zero;
            tutorialPanelRect.offsetMax = Vector2.zero;
            tutorialTitle?.gameObject.SetActive(expanded);
            if (tutorialCaption != null) tutorialCaption.gameObject.SetActive(expanded && !string.IsNullOrWhiteSpace(tutorialCaption.text));
            tutorialBody?.gameObject.SetActive(expanded);
        }

        private IEnumerator PlayTutorialStep(TutorialStep step)
        {
            switch (step)
            {
                case TutorialStep.Draw:
                    yield return PlayDrawSequence(false);
                    break;
                case TutorialStep.Learn:
                    yield return PlayPacedPhase(BattleTurnPhase.Learn);
                    break;
                case TutorialStep.BuildOrDiscard:
                    if (drawPresentationActive) yield return PlayDrawSequence(false);
                    yield return PlayPacedPhase(BattleTurnPhase.BuildOrDiscard);
                    if (!game.Player.HasCommittedCardThisTurn && game.Player.Hand.Count > 0)
                    {
                        bool built = game.TryBuildCard(OwnerSide.Player, 0, out string buildMessage);
                        if (built) battleAudio?.PlayCardPlaced();
                        ShowMatStatus(buildMessage);
                        UpdateScreen();
                        yield return new WaitForSecondsRealtime(1.1f);
                    }
                    break;
                case TutorialStep.EndTurn:
                    if (!game.Player.HasCommittedCardThisTurn && game.Player.Hand.Count > 0)
                    {
                        if (game.TryBuildCard(OwnerSide.Player, 0, out _)) battleAudio?.PlayCardPlaced();
                    }
                    game.RunAiTurn();
                    battleAudio?.PlayCardPlaced();
                    UpdateScreen();
                    yield return new WaitForSecondsRealtime(1.0f);
                    game.BeginEndTurnPhase();
                    yield return PlayPacedPhase(BattleTurnPhase.EndTurn);
                    break;
                case TutorialStep.Discard:
                    if (!game.Opponent.HasCommittedCardThisTurn)
                    {
                        game.RunAiTurn();
                        battleAudio?.PlayCardPlaced();
                    }
                    game.BeginEndTurnPhase();
                    yield return PlayPacedPhase(BattleTurnPhase.Discard);
                    if (!tutorialDiscardDemonstrated)
                    {
                        game.ResolveForcedDiscardPhase();
                        tutorialDiscardDemonstrated = true;
                        UpdateScreen();
                        if (game.LastPlayerForcedDiscard != null)
                            yield return PlayDiscardResolution(game.LastPlayerForcedDiscard, game.LastPlayerForcedDiscardMessage);
                        if (game.LastOpponentForcedDiscard != null)
                            yield return PlayDiscardResolution(game.LastOpponentForcedDiscard, game.LastOpponentForcedDiscardMessage);
                    }
                    break;
                case TutorialStep.Combat:
                    if (!tutorialDiscardDemonstrated)
                    {
                        game.RunAiTurn();
                        battleAudio?.PlayCardPlaced();
                        game.BeginEndTurnPhase();
                        game.ResolveForcedDiscardPhase();
                        tutorialDiscardDemonstrated = true;
                    }
                    yield return PlayPacedPhase(BattleTurnPhase.Combat);
                    if (!tutorialCombatDemonstrated)
                    {
                        game.ResolveCombatPlans(game.BuildAutoAttackPlan(OwnerSide.Player), game.BuildAutoAttackPlan(OwnerSide.Opponent), out string combatMessage);
                        ShowMatStatus(combatMessage);
                        tutorialCombatDemonstrated = true;
                        if (combatAnimator != null && game.LastCombatEvents.Count > 0)
                        {
                            yield return combatAnimator.PlaySequence(game.LastCombatEvents, lanesContent,
                                item => { battleAudio?.PlayAttack(); ShowMatStatus(item.Summary()); },
                                null,
                                GetDiscardAnimationTarget);
                        }
                        UpdateScreen();
                    }
                    break;
                case TutorialStep.GatherGrowth:
                    yield return PlayPacedPhase(BattleTurnPhase.GatherGrowth);
                    if (!tutorialTallyDemonstrated)
                    {
                        game.ResolveGrowthTallyAndAdvanceTurn();
                        tutorialTallyDemonstrated = true;
                        drawPresentationActive = true;
                        presentedPlayerHandCount = 0;
                        presentedOpponentHandCount = 0;
                        UpdateScreen();
                        if (game.LastPlayerTally != null)
                            yield return PlayTallyFlash(game.LastPlayerTally, game.Player.DisplayName, UIFactory.Green, false);
                        if (game.LastOpponentTally != null)
                            yield return PlayTallyFlash(game.LastOpponentTally, opponentLabel, UIFactory.Red, true);
                    }
                    break;
                default:
                    yield return new WaitForSecondsRealtime(ThemeService.ReducedMotion ? 0.15f : 0.55f);
                    break;
            }
        }

        private void RestartTutorial()
        {
            LocalSaveSystem.ResetTutorialProgress();
            LocalSaveSystem.SavePendingMatchContext("Tutorial", string.Empty, string.Empty, "Guide", string.Empty, string.Empty);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void SetTutorialStep(TutorialStep step)
        {
            if (!tutorialMatch || tutorialPanel == null)
            {
                return;
            }

            tutorialStep = step;
            LocalSaveSystem.SaveTutorialProgress((int)step, tutorialCoreDemonstrated);
            tutorialNextButton.gameObject.SetActive(true);
            SetButtonText(tutorialNextButton, "NEXT - PLAY STEP");
            tutorialPreviousButton.interactable = !tutorialStepPlaying && step > TutorialStep.Objective;
            tutorialSkipButton.gameObject.SetActive(tutorialCoreDemonstrated);
            SetTutorialCaption(null);

            switch (step)
            {
                case TutorialStep.Objective:
                    tutorialTitle.text = "START HERE  •  TWO WAYS TO WIN";
                    tutorialBody.text = $"Welcome to the mirrored single-lane board. You control the lesson with Next, Back, and Restart. Win by reaching {GameConstants.AppreciationVictoryTarget} Appreciation or reducing the opponent to zero HP.";
                    SetTutorialHighlight(new Rect(0.020f, 0.025f, 0.960f, 0.950f));
                    break;
                case TutorialStep.TurnSequence:
                    tutorialTitle.text = "LESSON 1  •  THE ROUND";
                    tutorialBody.text = "DRAW 2 → COMMIT 1 → BATTLE → APPRECIATE. Inspection is always available. Your unused card clears without an effect.";
                    SetTutorialHighlight(new Rect(0.020f, 0.635f, 0.960f, 0.040f));
                    break;
                case TutorialStep.Draw:
                    tutorialTitle.text = "LESSON 1  •  DRAW TWO";
                    tutorialBody.text = "Both mirrored hands begin empty. Watch two cards move from each face-down deck into the centered hand areas. Every new turn performs this draw automatically.";
                    SetTutorialHighlight(new Rect(0.370f, 0.095f, 0.605f, 0.185f));
                    break;
                case TutorialStep.Learn:
                    tutorialTitle.text = "LESSON 1  •  PLAN YOUR PLAY";
                    SetTutorialCaption("INSPECT ANYTIME");
                    tutorialBody.text = "Examine the field, cards & discard piles. Holding or clicking any revealed card opens its complete Build and Discard text before you commit.";
                    SetTutorialHighlight(new Rect(0.365f, 0.095f, 0.270f, 0.180f));
                    break;
                case TutorialStep.BuildOrDiscard:
                    tutorialTitle.text = "LESSON 1  •  COMMIT";
                    tutorialBody.text = "Choose one drawn card and one mode. BUILD creates a permanent. DISCARD triggers an immediate effect. The other card clears with no effect.";
                    SetTutorialHighlight(new Rect(0.020f, 0.335f, 0.960f, 0.285f));
                    break;
                case TutorialStep.HarmfulDiscard:
                    tutorialTitle.text = "LESSON 1  •  ONE ACTION";
                    tutorialBody.text = "Discard abilities display timing, targets, and costs before resolving. Only your actively chosen card can produce an effect this round.";
                    SetTutorialHighlight(new Rect(0.365f, 0.095f, 0.270f, 0.180f));
                    break;
                case TutorialStep.EndTurn:
                    tutorialTitle.text = "LESSON 1  •  LOCK IN";
                    tutorialBody.text = "LOCK IN confirms your Commit. The opponent commits, unused cards clear quietly, and Battle begins. In paused pacing, Next occupies this control rail.";
                    SetTutorialHighlight(new Rect(0.250f, 0.010f, 0.720f, 0.055f));
                    break;
                case TutorialStep.Discard:
                    tutorialTitle.text = "LESSON 1  •  UNUSED CARD";
                    tutorialBody.text = "The second card goes to the mirrored discard pile automatically and never triggers its Discard ability. You do not need to manage this step.";
                    SetTutorialHighlight(new Rect(0.025f, 0.095f, 0.115f, 0.810f));
                    break;
                case TutorialStep.PublicDiscard:
                    tutorialTitle.text = "LESSON 1  •  REVIEW THE DISCARD";
                    tutorialBody.text = "Both discard wells stack cards face-up. Click either player's stack to enlarge and review every revealed card without covering the Appreciation well beside it.";
                    SetTutorialHighlight(new Rect(0.025f, 0.095f, 0.115f, 0.810f));
                    break;
                case TutorialStep.BoardPresence:
                    tutorialTitle.text = "LESSON 3  •  BUILD THE BATTLEFIELD";
                    tutorialBody.text = "One Battlefield holds two opposing five-card rows. Built cards defend, attack, and contribute Appreciation. If your row is full, Build lets you replace one of your own cards.";
                    SetTutorialHighlight(new Rect(0.020f, 0.335f, 0.960f, 0.285f));
                    break;
                case TutorialStep.Combat:
                    tutorialTitle.text = "LESSON 2  •  BATTLE";
                    tutorialBody.text = "Ready cards may attack once. Select an attacker; legal targets glow. If no defender remains, attack the opponent directly. Attacking exhausts that card.";
                    SetTutorialHighlight(new Rect(0.020f, 0.335f, 0.960f, 0.285f));
                    break;
                case TutorialStep.BuffsAndNerfs:
                    tutorialTitle.text = "LESSON 2  •  CURRENT STATS";
                    tutorialBody.text = "Printed Attack and Defense remain visible while current values reflect modifiers and damage. Enlarged cards show the active source and duration; Combat always uses current values.";
                    SetTutorialHighlight(new Rect(0.300f, 0.355f, 0.400f, 0.250f));
                    break;
                case TutorialStep.DirectAttack:
                    tutorialTitle.text = "LESSON 2  •  DIRECT ATTACK";
                    tutorialBody.text = "If the opposing side has no eligible defender, an attacker crosses the shared lane and reduces the opponent's HP. Both mirrored HUDs update immediately.";
                    SetTutorialHighlight(new Rect(0.260f, 0.940f, 0.480f, 0.048f));
                    break;
                case TutorialStep.AutoAttack:
                    tutorialTitle.text = "LESSON 2  •  FIGHT OR SCORE";
                    tutorialBody.text = "An attack exhausts its card, so it will not score during Appreciate. Auto Plan previews legal attacks and the projected Appreciation before you confirm.";
                    SetTutorialHighlight(new Rect(0.180f, 0.335f, 0.640f, 0.285f));
                    break;
                case TutorialStep.GatherGrowth:
                    tutorialTitle.text = "LESSON 3  •  APPRECIATE";
                    SetTutorialCaption("READY CARDS SCORE");
                    tutorialBody.text = "Ready cards add their Growth as Appreciation. Links add +2 per neighboring match. Unity adds +3 for Art, Community, and Blockchain together. Then cards refresh.";
                    SetTutorialHighlight(new Rect(0.160f, 0.095f, 0.110f, 0.810f));
                    break;
                case TutorialStep.Winning:
                    tutorialTitle.text = "YOU ARE READY";
                    tutorialBody.text = $"Reach {GameConstants.AppreciationVictoryTarget} Appreciation during Appreciate or reduce the enemy to zero HP. Finish to receive the one-time 50 Appreciation Shard tutorial reward.";
                    SetButtonText(tutorialNextButton, "FINISH TUTORIAL");
                    SetTutorialHighlight(new Rect(0.160f, 0.095f, 0.110f, 0.810f));
                    break;
            }

            tutorialHighlight.transform.SetAsLastSibling();
            tutorialPanel.transform.SetAsLastSibling();
        }

        private void SetTutorialCaption(string value)
        {
            if (tutorialCaption == null) return;
            bool hasCaption = !string.IsNullOrWhiteSpace(value);
            tutorialCaption.gameObject.SetActive(hasCaption);
            tutorialCaption.text = hasCaption ? value : string.Empty;
        }

        private void SetTutorialHighlight(Rect normalizedRect)
        {
            if (tutorialHighlight == null)
            {
                return;
            }

            UIFactory.SetAnchors(
                tutorialHighlight.GetComponent<RectTransform>(),
                new Vector2(normalizedRect.xMin, normalizedRect.yMin),
                new Vector2(normalizedRect.xMax, normalizedRect.yMax),
                new Vector2(-5f, -5f),
                new Vector2(5f, 5f));
        }

        private IEnumerator PulseTutorialHighlight()
        {
            CanvasGroup group = tutorialHighlight == null ? null : tutorialHighlight.GetComponent<CanvasGroup>();
            float elapsed = 0f;
            while (tutorialMatch && tutorialHighlight != null && group != null)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(0.55f, 1f, (Mathf.Sin(elapsed * 4.2f) + 1f) * 0.5f);
                yield return null;
            }
        }

        private void FinishTutorial()
        {
            tutorialStep = TutorialStep.Complete;
            LocalSaveSystem.SaveTutorialProgress((int)TutorialStep.Complete, true);
            LocalSaveSystem.MarkTutorialCompleted();
            tutorialMatch = false;
            tutorialAwaitingTally = false;
            if (tutorialHighlight != null)
            {
                Destroy(tutorialHighlight);
            }
            if (tutorialPanel != null)
            {
                Destroy(tutorialPanel);
            }
            ShowMatStatus("Tutorial complete. Claiming 50 Appreciation Shards...");
            StartCoroutine(ClaimTutorialCompletionReward());
        }

        private void SkipTutorial()
        {
            tutorialStep = TutorialStep.Complete;
            tutorialMatch = false;
            tutorialAwaitingTally = false;
            if (tutorialHighlight != null) Destroy(tutorialHighlight);
            if (tutorialPanel != null) Destroy(tutorialPanel);
            ShowMatStatus("Tutorial skipped. Complete all tutorial steps later to earn the one-time 50 Appreciation Shard reward.");
        }

        private IEnumerator ClaimTutorialCompletionReward()
        {
            TutorialRewardResponse reward = null;
            string rewardError = null;
            string playerId = LocalSaveSystem.LoadOrCreatePlayerId();
            yield return apiClient.ClaimTutorialCompletionReward(
                playerId,
                response => reward = response,
                error => rewardError = error);

            if (reward?.inventory != null)
            {
                new AppreciatorsTcg.Packs.PackInventoryService(new AppreciatorsTcg.Packs.PackSaveService())
                    .ReplaceWithAuthoritativeSnapshot(reward.inventory);
            }

            if (reward != null && reward.success)
            {
                ShowMatStatus(reward.idempotentReplay
                    ? $"Tutorial complete. The one-time reward was already claimed. Balance: {reward.totalShardBalance:N0} Appreciation Shards."
                    : $"Tutorial complete! +{reward.shardsAwarded:N0} Appreciation Shards. Balance: {reward.totalShardBalance:N0}.");
            }
            else
            {
                ShowMatStatus($"Tutorial complete. The 50-Shard reward could not be synced yet: {rewardError}");
            }
        }

        private void ShowPlayChoiceDialog(int handIndex)
        {
            if (!CanStartCardDrag(handIndex))
            {
                battleAudio?.PlayInvalid();
                return;
            }

            ClosePlayChoiceDialogImmediate();
            pendingPlayChoiceHandIndex = handIndex;
            selectedHandIndex = handIndex;
            RefreshTransientHandUi();
            CardInspectionOverlay.Hide();

            CardDefinition card = game.Player.Hand[handIndex];
            playChoiceDialog = UIFactory.CreatePanel(Root, "PlayChoiceDialog", Color.clear);
            UIFactory.Stretch(playChoiceDialog.GetComponent<RectTransform>());
            playChoiceDialog.transform.SetAsLastSibling();
            CanvasGroup dialogGroup = playChoiceDialog.AddComponent<CanvasGroup>();

            GameObject choicePanel = UIFactory.CreateVerticalStack(
                playChoiceDialog.transform,
                "PlayChoicePanel",
                new Color(0.035f, 0.025f, 0.13f, 0.98f),
                8,
                14);
            RectTransform choiceRect = choicePanel.GetComponent<RectTransform>();
            UIFactory.SetAnchors(choiceRect, new Vector2(0.29f, 0.16f), new Vector2(0.71f, 0.84f), Vector2.zero, Vector2.zero);
            UIFactory.AddNeonFrame(choicePanel, UIFactory.Accent, 0.92f);

            Text title = UIFactory.CreateText(choicePanel.transform, "CHOOSE ONE ACTION", 24, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minHeight = 34;
            titleLayout.preferredHeight = 38;

            GameObject body = UIFactory.CreateHorizontalStack(choicePanel.transform, "PlayChoiceBody", Color.clear, 14, 0);
            LayoutElement bodyLayout = body.AddComponent<LayoutElement>();
            bodyLayout.minHeight = 248;
            bodyLayout.preferredHeight = 258;
            bodyLayout.flexibleHeight = 1;
            HorizontalLayoutGroup bodyGroup = body.GetComponent<HorizontalLayoutGroup>();
            bodyGroup.childForceExpandWidth = false;

            GameObject preview = UIFactory.CreateCardPanel(body.transform, card, compact: true);
            LayoutElement previewLayout = preview.GetComponent<LayoutElement>();
            previewLayout.minWidth = 176;
            previewLayout.preferredWidth = 194;
            previewLayout.flexibleWidth = 0;
            foreach (Graphic graphic in preview.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            GameObject options = UIFactory.CreateVerticalStack(body.transform, "PlayChoiceOptions", Color.clear, 8, 0);
            LayoutElement optionsLayout = options.AddComponent<LayoutElement>();
            optionsLayout.minWidth = 230;
            optionsLayout.preferredWidth = 250;
            optionsLayout.flexibleWidth = 1;

            Text cardName = UIFactory.CreateText(options.transform, card.name.ToUpperInvariant(), 20, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            LayoutElement cardNameLayout = cardName.gameObject.AddComponent<LayoutElement>();
            cardNameLayout.minHeight = 30;
            Text stats = UIFactory.CreateText(options.transform, $"ATTACK {card.GetAttack()}   •   DEFENSE {card.GetDefense()}", 17, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            LayoutElement statsLayout = stats.gameObject.AddComponent<LayoutElement>();
            statsLayout.minHeight = 28;

            Button build = UIFactory.CreateButton(
                options.transform,
                $"BUILD • PERMANENT\nA {card.GetAttack()} / D {card.GetDefense()} • APP +{card.GetBaseGrowth()}\n{card.GetBuildEffect()}",
                () => ConfirmPlayChoice(true),
                UIFactory.Green);
            SetChoiceButtonHeight(build, 76);
            Button discard = UIFactory.CreateButton(
                options.transform,
                $"DISCARD • INSTANT\n{card.GetDiscardEffect()}",
                () => ConfirmPlayChoice(false),
                UIFactory.PortalViolet);
            SetChoiceButtonHeight(discard, 82);

            Button cancel = UIFactory.CreateButton(choicePanel.transform, "PUT CARD BACK", CancelPlayChoice, UIFactory.PortalViolet);
            SetChoiceButtonHeight(cancel, 50);

            if (endTurnButton != null)
            {
                endTurnButton.interactable = false;
            }

            if (Application.isPlaying)
            {
                StartCoroutine(AnimatePlayChoiceIn(choiceRect, dialogGroup));
            }
            else
            {
                dialogGroup.alpha = 1f;
                choiceRect.localScale = Vector3.one;
            }
        }

        private void ConfirmPlayChoice(bool buildOnBoard)
        {
            int handIndex = pendingPlayChoiceHandIndex;
            CardDefinition chosen = game != null && handIndex >= 0 && handIndex < game.Player.Hand.Count
                ? game.Player.Hand[handIndex]
                : null;
            ClosePlayChoiceDialogImmediate();
            if (chosen == null)
            {
                selectedHandIndex = -1;
                RefreshTransientHandUi();
                return;
            }

            if (!buildOnBoard && chosen.IsHarmfulDiscard())
            {
                ShowDiscardConfirmation(handIndex, chosen);
                return;
            }

            selectedHandIndex = handIndex;
            if (buildOnBoard)
            {
                if (!game.MainLane.HasSpace(OwnerSide.Player))
                {
                    ShowRebuildChoice(handIndex, chosen);
                    return;
                }
                PlaySelectedCard(LaneType.Community);
            }
            else
            {
                DiscardHandCard(handIndex);
            }
        }

        private void ShowRebuildChoice(int handIndex, CardDefinition chosen)
        {
            playChoiceDialog = UIFactory.CreatePanel(Root, "RebuildChoiceDialog", Color.clear);
            UIFactory.Stretch(playChoiceDialog.GetComponent<RectTransform>());
            playChoiceDialog.transform.SetAsLastSibling();
            GameObject panel = UIFactory.CreateVerticalStack(playChoiceDialog.transform, "RebuildChoicePanel", UIFactory.Panel, 10, 16);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.28f, 0.22f), new Vector2(0.72f, 0.78f), Vector2.zero, Vector2.zero);
            UIFactory.AddNeonFrame(panel, UIFactory.Green, 0.96f);
            UIFactory.CreateText(panel.transform, $"REBUILD WITH {chosen.name.ToUpperInvariant()}", 21, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.CreateText(panel.transform, "Choose a Battlefield card to replace. Replaced cards go to discard without an effect.", 15, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Normal);
            GameObject choices = UIFactory.CreateVerticalStack(panel.transform, "RebuildTargets", Color.clear, 6, 0);
            foreach (BattleCardInstance instance in game.MainLane.PlayerCards.ToList())
            {
                BattleCardInstance captured = instance;
                Button replace = UIFactory.CreateButton(choices.transform,
                    $"REPLACE {captured.Definition.name.ToUpperInvariant()}  •  A{captured.CurrentAttack} D{captured.CurrentDefense}",
                    () => ConfirmRebuild(handIndex, captured.InstanceId), UIFactory.Green);
                SetChoiceButtonHeight(replace, 42);
            }
            Button cancel = UIFactory.CreateButton(panel.transform, "CANCEL", CancelPlayChoice, UIFactory.PortalViolet);
            SetChoiceButtonHeight(cancel, 42);
        }

        private void ConfirmRebuild(int handIndex, int replaceInstanceId)
        {
            CardInspectionOverlay.Hide();
            bool played = game.TryBuildCard(OwnerSide.Player, handIndex, replaceInstanceId, out string message);
            ClosePlayChoiceDialogImmediate();
            selectedHandIndex = -1;
            if (played) battleAudio?.PlayCardPlaced();
            else battleAudio?.PlayInvalid();
            UpdateScreen();
            ShowMatStatus(message);
        }

        private void ShowDiscardConfirmation(int handIndex, CardDefinition card)
        {
            pendingPlayChoiceHandIndex = handIndex;
            discardConfirmationDialog = UIFactory.CreatePanel(Root, "HarmfulDiscardConfirmation", Color.clear);
            UIFactory.Stretch(discardConfirmationDialog.GetComponent<RectTransform>());
            discardConfirmationDialog.transform.SetAsLastSibling();

            GameObject panel = UIFactory.CreateVerticalStack(discardConfirmationDialog.transform, "DiscardWarning", UIFactory.Panel, 12, 18);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.31f, 0.31f), new Vector2(0.69f, 0.69f), Vector2.zero, Vector2.zero);
            UIFactory.AddNeonFrame(panel, UIFactory.Red, 0.96f);
            UIFactory.CreateText(panel.transform, "CONFIRM HARMFUL DISCARD", 24, TextAnchor.MiddleCenter, UIFactory.Red, FontStyle.Bold);
            UIFactory.CreateText(panel.transform, card.GetDiscardConfirmation(), 19, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.CreateText(panel.transform, card.GetDiscardEffect(), 15, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Normal);
            GameObject actions = UIFactory.CreateHorizontalStack(panel.transform, "WarningActions", Color.clear, 8, 0);
            UIFactory.CreateButton(actions.transform, "CONTINUE — DISCARD", ConfirmHarmfulDiscard, UIFactory.Red);
            UIFactory.CreateButton(actions.transform, "CANCEL — KEEP CARD", CancelHarmfulDiscard, UIFactory.Blue);
        }

        private void ConfirmHarmfulDiscard()
        {
            int handIndex = pendingPlayChoiceHandIndex;
            CloseDiscardConfirmation();
            DiscardHandCard(handIndex);
        }

        private void CancelHarmfulDiscard()
        {
            int handIndex = pendingPlayChoiceHandIndex;
            CloseDiscardConfirmation();
            if (game != null && handIndex >= 0 && handIndex < game.Player.Hand.Count)
            {
                ShowPlayChoiceDialog(handIndex);
            }
        }

        private void CloseDiscardConfirmation()
        {
            pendingPlayChoiceHandIndex = -1;
            if (discardConfirmationDialog == null) return;
            if (Application.isPlaying) Destroy(discardConfirmationDialog);
            else DestroyImmediate(discardConfirmationDialog);
            discardConfirmationDialog = null;
        }

        private void CancelPlayChoice()
        {
            UiAudioService.PlayCancel();
            pendingPlayChoiceHandIndex = -1;
            selectedHandIndex = -1;
            RefreshTransientHandUi();
            RestoreEndTurnInteractable();
            ShowMatStatus("Card returned to your hand. No play was committed.");
            GameObject dialog = playChoiceDialog;
            playChoiceDialog = null;
            if (dialog == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                StartCoroutine(AnimatePlayChoiceOut(dialog));
            }
            else
            {
                DestroyImmediate(dialog);
            }
        }

        private void ClosePlayChoiceDialogImmediate()
        {
            pendingPlayChoiceHandIndex = -1;
            if (playChoiceDialog == null)
            {
                return;
            }

            playChoiceDialog.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(playChoiceDialog);
            }
            else
            {
                DestroyImmediate(playChoiceDialog);
            }

            playChoiceDialog = null;
        }

        private static void SetChoiceButtonHeight(Button button, float height)
        {
            LayoutElement layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static void SetPreferredHeight(GameObject target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static IEnumerator AnimatePlayChoiceIn(RectTransform panel, CanvasGroup group)
        {
            group.alpha = 0f;
            panel.localScale = Vector3.one * 0.88f;
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration && panel != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                group.alpha = eased;
                panel.localScale = Vector3.LerpUnclamped(Vector3.one * 0.88f, Vector3.one, eased);
                yield return null;
            }

            if (panel != null)
            {
                group.alpha = 1f;
                panel.localScale = Vector3.one;
            }
        }

        private static IEnumerator AnimatePlayChoiceOut(GameObject dialog)
        {
            CanvasGroup group = dialog.GetComponent<CanvasGroup>();
            RectTransform panel = dialog.transform.Find("PlayChoicePanel") as RectTransform;
            float elapsed = 0f;
            const float duration = 0.16f;
            while (elapsed < duration && dialog != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (group != null)
                {
                    group.alpha = 1f - t;
                }
                if (panel != null)
                {
                    panel.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.92f, t);
                }
                yield return null;
            }

            if (dialog != null)
            {
                Destroy(dialog);
            }
        }

        private void RefreshTransientHandUi()
        {
            if (handContent != null)
            {
                foreach (MatchHandCardInput input in handContent.GetComponentsInChildren<MatchHandCardInput>(true))
                {
                    bool selected = input.HandIndex == selectedHandIndex;
                    UiCardMotion motion = input.GetComponent<UiCardMotion>();
                    motion?.SetSelected(selected);
                    Image image = input.GetComponent<Image>();
                    if (image != null)
                    {
                        image.color = selected ? new Color(UIFactory.PortalViolet.r, UIFactory.PortalViolet.g, UIFactory.PortalViolet.b, 0.24f) : Color.clear;
                    }
                }
            }

            UpdateShardStacks();
        }

        private void RestoreEndTurnInteractable()
        {
            if (endTurnButton != null)
            {
                endTurnButton.interactable = !combatAnimating && !game.IsComplete && (!inviteMatch || !localInviteTurnEnded);
            }
        }

        private void UseLeaderAbility()
        {
            if (combatAnimating || game == null || game.IsComplete || !CanPlayDuringInviteTurn())
            {
                battleAudio?.PlayInvalid();
                return;
            }

            BattleLeaderDefinition leader = game.Player.Leader;
            bool used = game.TryUseLeaderAbility(OwnerSide.Player, out string message);
            if (!used)
            {
                battleAudio?.PlayInvalid();
                ShowMatStatus(message);
                UpdateScreen();
                return;
            }

            battleAudio?.PlayRally();
            if (inviteMatch && leader != null)
            {
                RecordInviteAction("leader-ability", leader.Id, leader.FocusLane.ToString());
            }

            UpdateScreen();
            ShowMatStatus(message);
            if (leader != null)
            {
                StartCoroutine(PlayLeaderFlash(OwnerSide.Player, leader.FocusLane, leader.AbilityName));
            }
        }

        private IEnumerator PlayDiscardResolution(CardDefinition card, string resolution)
        {
            UiAudioService.PlayDiscard();
            bool phoneLayout = ResponsiveCanvasScaler.IsPhoneLayout;
            GameObject panel = UIFactory.CreateHorizontalStack(matchTableRoot, "DiscardResolutionArea", ThemeService.IsDark
                ? new Color(0.025f, 0.018f, 0.105f, 0.97f)
                : new Color(0.97f, 0.95f, 0.86f, 0.98f), 12, 14);
            RectTransform rect = panel.GetComponent<RectTransform>();
            UIFactory.SetAnchors(
                rect,
                phoneLayout ? new Vector2(0.14f, 0.275f) : new Vector2(0.29f, 0.34f),
                phoneLayout ? new Vector2(0.86f, 0.725f) : new Vector2(0.71f, 0.66f),
                Vector2.zero,
                Vector2.zero);
            CanvasGroup group = panel.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            UIFactory.AddNeonFrame(panel, UIFactory.PortalViolet, 0.95f);

            GameObject preview = UIFactory.CreateMiniCardPanel(panel.transform, card, "FACE-UP DISCARD", true, phoneLayout ? 100 : 118, phoneLayout ? 148 : 174, phoneLayout ? 40 : 48);
            LayoutElement previewLayout = preview.GetComponent<LayoutElement>();
            previewLayout.flexibleWidth = 0;
            previewLayout.flexibleHeight = 0;
            foreach (Graphic graphic in preview.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            GameObject copy = UIFactory.CreateVerticalStack(panel.transform, "DiscardResolutionCopy", Color.clear, 6, 4);
            LayoutElement copyLayout = copy.AddComponent<LayoutElement>();
            copyLayout.flexibleWidth = 1;
            copyLayout.minWidth = phoneLayout ? 260 : 200;
            Text heading = UIFactory.CreateText(copy.transform, "DISCARD EFFECT", phoneLayout ? 21 : 22, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            SetPreferredHeight(heading.gameObject, phoneLayout ? 29 : 28);
            Text cardName = UIFactory.CreateText(copy.transform, card.name.ToUpperInvariant(), phoneLayout ? 18 : 19, TextAnchor.MiddleLeft, UIFactory.Cream, FontStyle.Bold);
            cardName.resizeTextForBestFit = true;
            cardName.resizeTextMinSize = 13;
            cardName.resizeTextMaxSize = phoneLayout ? 18 : 19;
            SetPreferredHeight(cardName.gameObject, phoneLayout ? 32 : 27);
            Text detail = UIFactory.CreateText(copy.transform, $"{card.GetDiscardEffect()}\n\n{resolution}", phoneLayout ? 16 : 14, TextAnchor.UpperLeft, UIFactory.TextColor, FontStyle.Normal);
            detail.resizeTextForBestFit = true;
            detail.resizeTextMinSize = phoneLayout ? 12 : 10;
            detail.resizeTextMaxSize = phoneLayout ? 16 : 14;
            detail.verticalOverflow = VerticalWrapMode.Truncate;
            LayoutElement detailLayout = detail.gameObject.AddComponent<LayoutElement>();
            detailLayout.flexibleHeight = 1;
            panel.transform.SetAsLastSibling();

            float transitionDuration = ThemeService.ReducedMotion ? 0.10f : 0.22f;
            float elapsed = 0f;
            group.alpha = 0f;
            rect.localScale = ThemeService.ReducedMotion ? Vector3.one : Vector3.one * 0.94f;
            while (elapsed < transitionDuration && panel != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                group.alpha = t;
                rect.localScale = ThemeService.ReducedMotion ? Vector3.one : Vector3.one * Mathf.Lerp(0.94f, 1f, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            if (panel != null)
            {
                group.alpha = 1f;
                rect.localScale = Vector3.one;
            }

            if (tutorialMatch)
            {
                yield return new WaitForSecondsRealtime(ThemeService.ReducedMotion ? 0.45f : 1.35f);
            }
            else
            {
                mandatoryDiscardReviewActive = true;
                waitingForPhaseAdvance = true;
                phaseAdvanceRequested = false;
                if (phaseNextButton != null)
                {
                    phaseNextButton.gameObject.SetActive(true);
                    phaseNextButton.interactable = true;
                    SetButtonText(phaseNextButton, "NEXT EFFECT");
                    phaseNextButton.transform.SetAsLastSibling();
                }
                ShowMatStatus("Discard effect revealed. Read the card and result, then click NEXT EFFECT to continue.");
                while (!phaseAdvanceRequested)
                {
                    yield return null;
                }
                mandatoryDiscardReviewActive = false;
                waitingForPhaseAdvance = false;
                phaseAdvanceRequested = false;
                if (phaseNextButton != null)
                {
                    SetButtonText(phaseNextButton, "NEXT");
                }
            }

            elapsed = 0f;
            while (elapsed < transitionDuration && panel != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionDuration);
                group.alpha = 1f - t;
                yield return null;
            }
            if (panel != null) Destroy(panel);
        }

        private void CreateQuitDialog()
        {
            quitDialog = UIFactory.CreatePanel(Root, "QuitMatchDialog", new Color(0f, 0f, 0f, 0.72f));
            UIFactory.Stretch(quitDialog.GetComponent<RectTransform>());

            GameObject panel = UIFactory.CreateVerticalStack(quitDialog.transform, "QuitMatchPanel", new Color(0.035f, 0.025f, 0.070f, 0.98f), 12, 22);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.31f, 0.32f), new Vector2(0.69f, 0.68f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(panel.transform, "LEAVE MATCH", 30, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            quitDialogText = UIFactory.CreateText(panel.transform, string.Empty, 20, TextAnchor.MiddleCenter, UIFactory.TextColor, FontStyle.Bold);

            GameObject actions = UIFactory.CreateHorizontalStack(panel.transform, "QuitDialogActions", Color.clear, 10, 0);
            LayoutElement actionLayout = actions.AddComponent<LayoutElement>();
            actionLayout.minHeight = 58;
            actionLayout.preferredHeight = 64;
            quitConfirmButton = UIFactory.CreateButton(actions.transform, "CONFIRM", HandleQuitConfirm, UIFactory.Red);
            quitCancelButton = UIFactory.CreateButton(actions.transform, "STAY", HandleQuitCancel, UIFactory.PanelAlt);
            quitDialog.transform.SetAsLastSibling();
            quitDialog.SetActive(false);
        }

        private void OpenQuitDialog()
        {
            if (leavingMatch || terminationRequestInFlight || quitDialog == null)
            {
                return;
            }

            quitDialog.SetActive(true);
            quitDialog.transform.SetAsLastSibling();
            RefreshQuitDialog();
        }

        private void RefreshQuitDialog()
        {
            if (quitDialogText == null || quitConfirmButton == null || quitCancelButton == null)
            {
                return;
            }

            quitConfirmButton.interactable = !terminationRequestInFlight;
            quitCancelButton.interactable = !terminationRequestInFlight;
            if (!inviteMatch)
            {
                pendingTerminationDecision = "local";
                quitDialogText.text = "End this casual match and return to the main menu?";
                SetButtonText(quitConfirmButton, "QUIT TO MENU");
                SetButtonText(quitCancelButton, "STAY IN MATCH");
                return;
            }

            InviteTerminationState termination = latestInviteState?.termination;
            bool pending = string.Equals(termination?.status, "pending", StringComparison.OrdinalIgnoreCase);
            bool requestedBySelf = pending && termination.requestedByPlayerId == invitePlayerId;
            if (!pending)
            {
                pendingTerminationDecision = "request";
                quitDialogText.text = "Both players must agree to terminate an online match. Send a termination request?";
                SetButtonText(quitConfirmButton, "REQUEST EXIT");
                SetButtonText(quitCancelButton, "STAY IN MATCH");
                return;
            }

            if (requestedBySelf)
            {
                pendingTerminationDecision = "decline";
                quitDialogText.text = "Waiting for your opponent to agree. The match remains active until both players accept.";
                SetButtonText(quitConfirmButton, "CANCEL REQUEST");
                SetButtonText(quitCancelButton, "RETURN TO MATCH");
            }
            else
            {
                pendingTerminationDecision = "accept";
                string requester = string.IsNullOrWhiteSpace(termination.requestedByUsername) ? "Your opponent" : termination.requestedByUsername;
                quitDialogText.text = $"{requester} requests mutual match termination. Agree and return both players to the main menu?";
                SetButtonText(quitConfirmButton, "AGREE & EXIT");
                SetButtonText(quitCancelButton, "KEEP PLAYING");
            }
        }

        private void HandleQuitConfirm()
        {
            if (pendingTerminationDecision == "local")
            {
                LeaveForMainMenu();
                return;
            }

            SubmitTerminationDecision(pendingTerminationDecision);
        }

        private void HandleQuitCancel()
        {
            InviteTerminationState termination = latestInviteState?.termination;
            bool incomingRequest = inviteMatch &&
                string.Equals(termination?.status, "pending", StringComparison.OrdinalIgnoreCase) &&
                termination.requestedByPlayerId != invitePlayerId;
            if (incomingRequest)
            {
                SubmitTerminationDecision("decline");
                return;
            }

            quitDialog?.SetActive(false);
        }

        private void SubmitTerminationDecision(string decision)
        {
            if (!inviteMatch || apiClient == null || terminationRequestInFlight || string.IsNullOrWhiteSpace(inviteCode))
            {
                return;
            }

            terminationRequestInFlight = true;
            RefreshQuitDialog();
            StartCoroutine(apiClient.RespondInviteTermination(inviteCode, invitePlayerId, decision, response =>
            {
                terminationRequestInFlight = false;
                if (response?.matchState != null)
                {
                    latestInviteState = response.matchState;
                }
                else if (response?.room?.matchState != null)
                {
                    latestInviteState = response.room.matchState;
                }

                if (!ApplyTerminationState())
                {
                    RefreshQuitDialog();
                    UpdateScreen();
                }
            }, error =>
            {
                terminationRequestInFlight = false;
                Debug.LogWarning($"Invite termination request failed: {error}");
                if (messageText != null)
                {
                    messageText.text = "Could not update the termination request. The match remains active.";
                }
                RefreshQuitDialog();
            }));
        }

        private bool ApplyTerminationState()
        {
            InviteTerminationState termination = latestInviteState?.termination;
            if (string.Equals(latestInviteState?.status, "terminated", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(termination?.status, "agreed", StringComparison.OrdinalIgnoreCase))
            {
                LeaveForMainMenu();
                return true;
            }

            if (string.Equals(termination?.status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                if (termination.requestedByPlayerId != invitePlayerId && quitDialog != null)
                {
                    quitDialog.SetActive(true);
                    quitDialog.transform.SetAsLastSibling();
                }
                RefreshQuitDialog();
            }
            else if (string.Equals(termination?.status, "declined", StringComparison.OrdinalIgnoreCase))
            {
                quitDialog?.SetActive(false);
                if (!string.IsNullOrWhiteSpace(termination.declinedByUsername))
                {
                    matchIntro = $"{termination.declinedByUsername} chose to continue the match.";
                }
            }

            return false;
        }

        private void LeaveForMainMenu()
        {
            if (leavingMatch)
            {
                return;
            }

            leavingMatch = true;
            CancelInvoke(nameof(PollInviteActions));
            CardInspectionOverlay.Hide();
            SceneManager.LoadScene("MainMenuScene");
        }

        private static void SetButtonText(Button button, string label)
        {
            Text text = button == null ? null : button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private void LateUpdate()
        {
            ApplyResponsiveFigmaLayout(false);
            UpdateNextButtonAttention();
        }

        private void CreateFigmaBoardChrome(Transform parent)
        {
            CreateNativeMirroredBoardChrome(parent);
            // The official playmat already supplies the board chrome and zones.
            // Do not cover it with additional opaque responsive panels.
            return;
#pragma warning disable CS0162
            GameObject wash = UIFactory.CreatePanel(parent, "FigmaBoardNavy", new Color(0.024f, 0.078f, 0.192f, 0.94f));
            UIFactory.SetAnchors(wash.GetComponent<RectTransform>(), new Vector2(0.006f, 0.008f), new Vector2(0.994f, 0.992f), Vector2.zero, Vector2.zero);
            Image washImage = wash.GetComponent<Image>();
            washImage.raycastTarget = false;
            Outline washOutline = wash.GetComponent<Outline>() ?? wash.AddComponent<Outline>();
            washOutline.effectColor = UIFactory.PortalViolet;
            washOutline.effectDistance = new Vector2(6f, -6f);
            wash.transform.SetAsFirstSibling();

            CreateFigmaZonePanel(parent, "OpponentZoneChrome", new Rect(0.245f, 0.655f, 0.510f, 0.270f));
            CreateFigmaZonePanel(parent, "CombatLaneChrome", new Rect(0.245f, 0.365f, 0.510f, 0.225f));
            CreateFigmaZonePanel(parent, "PlayerZoneChrome", new Rect(0.245f, 0.075f, 0.510f, 0.270f));

            Text marquee = UIFactory.CreateText(parent, "★  APPRECIATORS TCG  ★", 22, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            marquee.name = "FigmaBoardMarquee";
            UIFactory.SetAnchors(marquee.rectTransform, new Vector2(0.30f, 0.925f), new Vector2(0.70f, 0.970f), Vector2.zero, Vector2.zero);
            marquee.raycastTarget = false;
#pragma warning restore CS0162
        }

        private void CreateNativeMirroredBoardChrome(Transform parent)
        {
            CreateNativeBoardPanel(parent, "NativeBoardSurface", new Rect(0.008f, 0.012f, 0.984f, 0.976f), Brand("FAFAD2"), Brand("0F0A46"), string.Empty);
            CreateNativeBoardPanel(parent, "NativeOpponentZone", new Rect(0.020f, 0.690f, 0.960f, 0.285f), Brand("FFFFFF"), Brand("7841AA"), "OPPONENT");
            CreateNativeBoardPanel(parent, "NativePlayerZone", new Rect(0.020f, 0.025f, 0.960f, 0.285f), Brand("FFFFFF"), Brand("7841AA"), "PLAYER");

            CreateNativeBoardPanel(parent, "NativeMainBattlefield", new Rect(0.020f, 0.325f, 0.960f, 0.350f), Brand("C8FAFA"), Brand("00BEE1"), "BATTLEFIELD  •  DROP TO BUILD");

            CreateNativeBoardPanel(parent, "OpponentDiscardWell", new Rect(0.025f, 0.720f, 0.115f, 0.185f), Brand("D7C3EB"), Brand("7841AA"), "DISCARD");
            CreateNativeBoardPanel(parent, "PlayerDiscardWell", new Rect(0.025f, 0.095f, 0.115f, 0.185f), Brand("D7C3EB"), Brand("7841AA"), "DISCARD");
            CreateNativeBoardPanel(parent, "OpponentAppreciationWell", new Rect(0.160f, 0.720f, 0.110f, 0.185f), Brand("FAFAD2"), Brand("FFC700"), "APPRECIATION");
            CreateNativeBoardPanel(parent, "PlayerAppreciationWell", new Rect(0.160f, 0.095f, 0.110f, 0.185f), Brand("FAFAD2"), Brand("FFC700"), "APPRECIATION");
            CreateNativeBoardPanel(parent, "OpponentDeckWell", new Rect(0.880f, 0.720f, 0.095f, 0.185f), Brand("C8FAFA"), Brand("00BEE1"), "DECK");
            CreateNativeBoardPanel(parent, "PlayerDeckWell", new Rect(0.880f, 0.095f, 0.095f, 0.185f), Brand("C8FAFA"), Brand("00BEE1"), "DECK");

            Transform surface = parent.Find("NativeBoardSurface");
            if (surface != null) surface.SetAsFirstSibling();
            CreateNativeBoardArtwork(parent);
            CreateNativeCenterFace(parent);

            string[] chromeOrder =
            {
                "NativeBoardSurface",
                "NativeOpponentZone",
                "NativePlayerZone",
                "NativeMainBattlefield",
                "NativeCenterFaceLogo",
                "OpponentDiscardWell",
                "PlayerDiscardWell",
                "OpponentAppreciationWell",
                "PlayerAppreciationWell",
                "OpponentDeckWell",
                "PlayerDeckWell"
            };
            for (int index = 0; index < chromeOrder.Length; index++)
            {
                Transform chrome = parent.Find(chromeOrder[index]);
                if (chrome != null)
                {
                    chrome.SetSiblingIndex(Mathf.Min(index, parent.childCount - 1));
                }
            }
            ApplyNativeBoardTheme();
        }

        private static void CreateNativeBoardArtwork(Transform parent)
        {
            Texture2D motif = Resources.Load<Texture2D>("Art/Official/Backgrounds/appreciators_starfield_motif_v2_8k");
            if (motif == null) return;

            Transform opponentZone = parent.Find("NativeOpponentZone");
            Transform playerZone = parent.Find("NativePlayerZone");
            Transform battlefield = parent.Find("NativeMainBattlefield");

            CreateNativeArtworkCrop(opponentZone, "OpponentHandMotif", motif, new Rect(0f, 0f, 1f, 0.255f), new Rect(0.275f, 0.08f, 0.585f, 0.80f));
            CreateNativeArtworkCrop(playerZone, "PlayerHandMotif", motif, new Rect(0f, 0f, 1f, 0.255f), new Rect(0.275f, 0.08f, 0.585f, 0.80f));
            CreateNativeArtworkCrop(battlefield, "BattlefieldStarfield", motif, new Rect(0f, 0.27f, 1f, 0.73f), new Rect(0.008f, 0.035f, 0.984f, 0.93f));
        }

        private static void CreateNativeArtworkCrop(Transform parent, string name, Texture texture, Rect uv, Rect anchors)
        {
            if (parent == null || texture == null) return;
            GameObject artwork = new GameObject(name, typeof(RectTransform), typeof(RawImage));
            artwork.transform.SetParent(parent, false);
            RectTransform rect = artwork.GetComponent<RectTransform>();
            UIFactory.SetAnchors(rect, new Vector2(anchors.xMin, anchors.yMin), new Vector2(anchors.xMax, anchors.yMax), Vector2.zero, Vector2.zero);
            RawImage image = artwork.GetComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uv;
            image.color = Color.white;
            image.raycastTarget = false;
            if (name == "OpponentHandMotif" || name == "PlayerHandMotif")
            {
                Shader silhouetteShader = Resources.Load<Shader>("Shaders/AppreciatorsSilhouette");
                if (silhouetteShader != null)
                {
                    Material silhouetteMaterial = new Material(silhouetteShader)
                    {
                        name = name + "Material"
                    };
                    silhouetteMaterial.SetFloat("_Threshold", 0.42f);
                    silhouetteMaterial.SetFloat("_Softness", 0.08f);
                    image.material = silhouetteMaterial;
                }
            }
            artwork.transform.SetAsFirstSibling();
        }

        private static Color Brand(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.white;
        }

        private static void CreateNativeCenterFace(Transform parent)
        {
            GameObject emblem = new GameObject("NativeCenterFaceLogo", typeof(RectTransform));
            emblem.transform.SetParent(parent, false);
            RectTransform rect = emblem.GetComponent<RectTransform>();
            UIFactory.SetAnchors(rect, new Vector2(0.445f, 0.405f), new Vector2(0.555f, 0.555f), Vector2.zero, Vector2.zero);
            rect.localRotation = Quaternion.Euler(0f, 0f, 180f);

            Text face = UIFactory.CreateText(emblem.transform, "☻", 92, TextAnchor.MiddleCenter, Brand("FFC700"), FontStyle.Bold);
            UIFactory.Stretch(face.rectTransform);
            face.resizeTextForBestFit = true;
            face.resizeTextMinSize = 36;
            face.resizeTextMaxSize = 92;
            face.raycastTarget = false;
            Outline outline = face.gameObject.AddComponent<Outline>();
            outline.effectColor = Brand("0F0A46");
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private void ApplyNativeBoardTheme()
        {
            if (matchTableRoot == null) return;
            bool dark = ThemeService.IsDark;
            Color ink = Brand("17151B");
            Color navy = Brand("0F0A46");
            Color cream = Brand("FAFAD2");
            Color chalk = Brand("FFFFFF");
            Color grape = Brand("7841AA");
            Color gold = Brand("FFC700");
            Color wave = Brand("00BEE1");

            StyleNativePanel("NativeBoardSurface", dark ? ink : cream, dark ? grape : navy, dark ? cream : navy);
            StyleNativePanel("NativeOpponentZone", dark ? Brand("17114A") : chalk, grape, dark ? cream : navy);
            StyleNativePanel("NativePlayerZone", dark ? Brand("17114A") : chalk, grape, dark ? cream : navy);
            StyleNativePanel("NativeMainBattlefield", dark ? navy : Brand("C8FAFA"), wave, dark ? cream : navy);
            StyleNativePanel("OpponentDiscardWell", dark ? Brand("3A2255") : Brand("D7C3EB"), grape, dark ? chalk : navy);
            StyleNativePanel("PlayerDiscardWell", dark ? Brand("3A2255") : Brand("D7C3EB"), grape, dark ? chalk : navy);
            StyleNativePanel("OpponentAppreciationWell", dark ? Brand("5A4800") : cream, gold, dark ? chalk : navy);
            StyleNativePanel("PlayerAppreciationWell", dark ? Brand("5A4800") : cream, gold, dark ? chalk : navy);
            StyleNativePanel("OpponentDeckWell", dark ? Brand("073844") : Brand("C8FAFA"), wave, dark ? chalk : navy);
            StyleNativePanel("PlayerDeckWell", dark ? Brand("073844") : Brand("C8FAFA"), wave, dark ? chalk : navy);
            StyleNativePanel("NativePhaseRail", dark ? Brand("25105A") : navy, dark ? wave : grape, cream);
            StyleNativeArtwork("OpponentHandMotif", dark ? cream : navy, dark ? 0.22f : 0.16f);
            StyleNativeArtwork("PlayerHandMotif", dark ? cream : navy, dark ? 0.22f : 0.16f);
            StyleNativeArtwork("BattlefieldStarfield", dark ? 0.94f : 0.72f);

            StyleThemeButton(phaseNextButton, dark ? cream : navy, dark ? navy : chalk);
            StyleThemeButton(quitButton, dark ? Brand("C8FAFA") : grape, dark ? navy : chalk);
            StyleThemeButton(endTurnButton, dark ? Brand("FAD7FA") : Brand("FF2314"), dark ? navy : chalk);
        }

        private void StyleNativePanel(string name, Color fill, Color border, Color labelColor)
        {
            Transform panel = matchTableRoot.Find(name);
            if (panel == null) return;
            Image image = panel.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = fill;
            }
            Outline outline = panel.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = border;
                outline.effectDistance = new Vector2(3f, -3f);
            }
            foreach (Text text in panel.GetComponentsInChildren<Text>(true)) text.color = labelColor;
        }

        private void StyleNativeArtwork(string name, float alpha)
        {
            RawImage artwork = matchTableRoot.GetComponentsInChildren<RawImage>(true).FirstOrDefault(image => image.name == name);
            if (artwork == null) return;
            artwork.color = new Color(1f, 1f, 1f, alpha);
        }

        private void StyleNativeArtwork(string name, Color color, float alpha)
        {
            RawImage artwork = matchTableRoot.GetComponentsInChildren<RawImage>(true).FirstOrDefault(image => image.name == name);
            if (artwork == null) return;
            artwork.color = new Color(color.r, color.g, color.b, alpha);
        }

        private static void StyleThemeButton(Button button, Color background, Color foreground)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = background;
            ColorBlock colors = button.colors;
            colors.normalColor = background;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.18f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            foreach (Text text in button.GetComponentsInChildren<Text>(true)) text.color = foreground;
        }

        private static void CreateNativeBoardPanel(Transform parent, string name, Rect anchors, Color fill, Color accent, string label)
        {
            GameObject panel = UIFactory.CreatePanel(parent, name, fill);
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(anchors.xMin, anchors.yMin), new Vector2(anchors.xMax, anchors.yMax), Vector2.zero, Vector2.zero);
            Image image = panel.GetComponent<Image>();
            image.raycastTarget = false;
            Outline outline = panel.GetComponent<Outline>() ?? panel.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(3f, -3f);
            panel.transform.SetSiblingIndex(Mathf.Min(1, panel.transform.parent.childCount - 1));

            if (string.IsNullOrWhiteSpace(label)) return;
            Text title = UIFactory.CreateText(panel.transform, label, 13, TextAnchor.UpperLeft, UIFactory.Ink, FontStyle.Bold);
            UIFactory.SetAnchors(title.rectTransform, new Vector2(0.018f, 0.88f), new Vector2(0.42f, 0.985f), Vector2.zero, Vector2.zero);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 8;
            title.resizeTextMaxSize = 13;
            title.raycastTarget = false;
        }

        private static void ApplyNativeArt(Transform target, string resourcePath, Color tint)
        {
            if (target == null) return;
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            Image image = target.GetComponent<Image>();
            if (sprite == null || image == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = tint;
        }

        private static void CreateFigmaZonePanel(Transform parent, string name, Rect anchors)
        {
            GameObject panel = UIFactory.CreatePanel(parent, name, new Color(1f, 0.976f, 0.925f, 0.96f));
            UIFactory.SetAnchors(
                panel.GetComponent<RectTransform>(),
                new Vector2(anchors.xMin, anchors.yMin),
                new Vector2(anchors.xMax, anchors.yMax),
                Vector2.zero,
                Vector2.zero);
            Image image = panel.GetComponent<Image>();
            image.raycastTarget = false;
            Outline outline = panel.GetComponent<Outline>() ?? panel.AddComponent<Outline>();
            outline.effectColor = UIFactory.Ink;
            outline.effectDistance = new Vector2(4f, -4f);
            panel.transform.SetSiblingIndex(1);
        }

        private void ApplyResponsiveFigmaLayout(bool force)
        {
            if (matchTableRoot == null || Screen.width <= 0 || Screen.height <= 0) return;

            Vector2Int currentSize = new Vector2Int(Screen.width, Screen.height);
            if (!force && currentSize == lastResponsiveLayoutSize) return;
            lastResponsiveLayoutSize = currentSize;

            float aspect = (float)Screen.width / Screen.height;
            bool portrait = aspect < 0.78f;
            bool compactLayout = ResponsiveCanvasScaler.IsCompactLayout;
            bool phoneLayout = ResponsiveCanvasScaler.IsPhoneLayout;

            ApplyMirroredPlayerZoneLayout(phoneLayout, portrait);

            SetRect(deckDrawSource == null ? null : deckDrawSource.parent as RectTransform, new Rect(0.890f, 0.100f, 0.080f, 0.165f));
            SetRect(opponentDeckDrawSource == null ? null : opponentDeckDrawSource.parent as RectTransform, new Rect(0.890f, 0.735f, 0.080f, 0.165f));
            ApplyMatchReadability(compactLayout, phoneLayout);
            if (tutorialMatch && tutorialPanelRect != null)
            {
                bool expanded = tutorialBody != null && tutorialBody.gameObject.activeSelf;
                if (phoneLayout)
                {
                    tutorialExpandedMin = new Vector2(0.025f, portrait ? 0.13f : 0.16f);
                    tutorialExpandedMax = new Vector2(0.975f, 0.975f);
                    tutorialCollapsedMin = new Vector2(0.055f, 0.012f);
                    tutorialCollapsedMax = new Vector2(0.945f, 0.175f);
                }
                else
                {
                    tutorialExpandedMin = new Vector2(0.055f, 0.300f);
                    tutorialExpandedMax = new Vector2(0.945f, 0.960f);
                    tutorialCollapsedMin = new Vector2(0.255f, 0.018f);
                    tutorialCollapsedMax = new Vector2(0.745f, 0.112f);
                }
                SetTutorialPanelExpandedImmediate(expanded);
            }
            RefreshNativeArtworkCropping();
        }

        private void RefreshNativeArtworkCropping()
        {
            if (matchTableRoot == null) return;
            foreach (RawImage artwork in matchTableRoot.GetComponentsInChildren<RawImage>(true))
            {
                bool isHandMotif = artwork.name == "OpponentHandMotif" || artwork.name == "PlayerHandMotif";
                bool isBattlefield = artwork.name == "BattlefieldStarfield";
                if ((!isHandMotif && !isBattlefield) || artwork.texture == null) continue;

                Rect rect = artwork.rectTransform.rect;
                if (rect.width <= 1f || rect.height <= 1f) continue;
                float destinationAspect = Mathf.Abs(rect.width / rect.height);
                float textureAspect = (float)artwork.texture.width / artwork.texture.height;
                float uvHeight = Mathf.Clamp(textureAspect / destinationAspect, 0.16f, isBattlefield ? 0.58f : 0.55f);

                if (isBattlefield)
                {
                    // Keep the crop wholly above the illustrative motif band so
                    // no rainbow, target, skull, or character fragments enter play.
                    const float starfieldFloor = 0.38f;
                    float availableHeight = 1f - starfieldFloor;
                    uvHeight = Mathf.Min(uvHeight, availableHeight);
                    float centeredY = starfieldFloor + (availableHeight - uvHeight) * 0.5f;
                    artwork.uvRect = new Rect(0f, centeredY, 1f, uvHeight);
                }
                else
                {
                    // The UV height is derived from the live destination aspect.
                    // This keeps the illustrated hand rail proportional at every
                    // supported screen size instead of stretching it to the slot.
                    artwork.uvRect = new Rect(0f, 0f, 1f, uvHeight);
                }
            }
        }

        private void ApplyMirroredPlayerZoneLayout(bool phoneLayout, bool portrait)
        {
            float controlHeight = phoneLayout ? 0.105f : 0.048f;
            float handHeight = phoneLayout ? 0.122f : 0.105f;
            float handY = phoneLayout ? 0.118f : 0.160f;
            float opponentHandY = phoneLayout ? 0.760f : 0.735f;
            SetRect(opponentHudContent, phoneLayout
                ? new Rect(0.205f, 0.908f, 0.590f, 0.078f)
                : new Rect(0.260f, 0.940f, 0.480f, 0.048f));
            SetRect(playerHudContent, phoneLayout
                ? new Rect(0.315f, 0.018f, 0.475f, 0.078f)
                : new Rect(0.370f, 0.012f, 0.370f, 0.048f));

            SetRect(opponentDiscardContent, phoneLayout ? new Rect(0.030f, 0.742f, 0.086f, 0.148f) : new Rect(0.030f, 0.735f, 0.105f, 0.165f));
            SetRect(playerDiscardContent, phoneLayout ? new Rect(0.030f, 0.110f, 0.086f, 0.148f) : new Rect(0.030f, 0.100f, 0.105f, 0.165f));

            // The liquid reservoir follows the printed Appreciation well exactly.
            // Its prior left offset belonged to the old board crop and made the
            // fill appear beside the button instead of inside it on phones.
            SetRect(opponentAppreciationMeter, new Rect(0.160f, 0.720f, 0.110f, 0.185f));
            SetRect(playerAppreciationMeter, new Rect(0.160f, 0.095f, 0.110f, 0.185f));

            SetRect(opponentHandContent, new Rect(phoneLayout ? 0.365f : 0.390f, opponentHandY, phoneLayout ? 0.270f : 0.220f, handHeight));
            SetRect(handScrollRect, new Rect(phoneLayout ? 0.365f : 0.390f, handY, phoneLayout ? 0.270f : 0.220f, handHeight));

            SetRect(endTurnButton, new Rect(phoneLayout ? 0.800f : 0.820f, 0.012f, phoneLayout ? 0.185f : 0.150f, controlHeight));
            float nextControlHeight = phoneLayout ? 0.078f : 0.044f;
            // Swap the secondary Options control with the primary Next control.
            // The latter now sits at the right edge and becomes the visual cue for
            // phase advancement, while Options remains clear of the hand/meter.
            SetRect(phaseNextButton, new Rect(phoneLayout ? 0.665f : 0.755f, 0.012f, phoneLayout ? 0.125f : 0.110f, nextControlHeight));
            SetRect(quitButton, new Rect(phoneLayout ? 0.280f : 0.260f, 0.008f, phoneLayout ? 0.090f : 0.105f, controlHeight));
            SetRect(messageText, new Rect(phoneLayout ? 0.405f : 0.610f, phoneLayout ? 0.622f : 0.638f, phoneLayout ? 0.565f : 0.350f, phoneLayout ? 0.066f : 0.033f));
            messageText.alignment = TextAnchor.MiddleRight;
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void ApplyMatchReadability(bool compactLayout, bool phoneLayout)
        {
            if (messageText != null)
            {
                messageText.fontSize = phoneLayout ? 14 : compactLayout ? 15 : 13;
                messageText.resizeTextMinSize = phoneLayout ? 10 : 11;
                messageText.resizeTextMaxSize = messageText.fontSize;
            }

            SetMinimumFontSize(opponentHudContent, phoneLayout ? 16 : compactLayout ? 14 : 12);
            SetMinimumFontSize(playerHudContent, phoneLayout ? 16 : compactLayout ? 14 : 12);
            Transform phaseRail = matchTableRoot.Find("NativePhaseRail");
            SetMinimumFontSize(phaseRail, phoneLayout ? 17 : compactLayout ? 15 : 13);
            ConfigureCompactMatchButton(quitButton, phoneLayout, "OPTIONS");
            ConfigureCompactMatchButton(phaseNextButton, phoneLayout, "NEXT");
            ConfigureCompactMatchButton(endTurnButton, phoneLayout, null);
        }

        private void UpdateNextButtonAttention()
        {
            if (phaseNextButton == null) return;

            // Only call attention to NEXT when the game is genuinely waiting for
            // an acknowledgement. It stays quiet while a player is choosing a
            // card, target, or other decision.
            bool needsNext = !tutorialMatch &&
                phaseNextButton.gameObject.activeInHierarchy &&
                phaseNextButton.interactable &&
                (waitingForPhaseAdvance || mandatoryDiscardReviewActive);

            if (phaseNextGlowOutline == null)
            {
                phaseNextGlowOutline = phaseNextButton.GetComponent<Outline>();
            }
            if (phaseNextGlowShadow == null)
            {
                phaseNextGlowShadow = phaseNextButton.GetComponent<Shadow>();
            }

            RectTransform rect = phaseNextButton.GetComponent<RectTransform>();
            if (!needsNext)
            {
                if (phaseNextGlowOutline != null)
                {
                    Color restingOutline = ThemeService.IsDark ? UIFactory.NeonCyan : UIFactory.PortalViolet;
                    phaseNextGlowOutline.enabled = true;
                    phaseNextGlowOutline.effectColor = new Color(restingOutline.r, restingOutline.g, restingOutline.b, 0.62f);
                    phaseNextGlowOutline.effectDistance = new Vector2(3.5f, -3.5f);
                }
                if (phaseNextGlowShadow != null) phaseNextGlowShadow.enabled = false;
                if (rect != null) rect.localScale = Vector3.one;
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5.4f);
            Color glow = Color.Lerp(Brand("FFC700"), Brand("FFFFFF"), pulse * 0.55f);
            if (phaseNextGlowOutline != null)
            {
                phaseNextGlowOutline.enabled = true;
                phaseNextGlowOutline.effectColor = new Color(glow.r, glow.g, glow.b, 0.95f);
                phaseNextGlowOutline.effectDistance = new Vector2(4f + pulse * 3f, -4f - pulse * 3f);
            }
            if (phaseNextGlowShadow != null)
            {
                phaseNextGlowShadow.enabled = true;
                phaseNextGlowShadow.effectColor = new Color(glow.r, glow.g, glow.b, 0.75f);
                phaseNextGlowShadow.effectDistance = new Vector2(0f, -1f);
            }
            if (rect != null) rect.localScale = Vector3.one * (1.0f + pulse * 0.055f);
        }

        private static void ConfigureCompactMatchButton(Button button, bool compact, string compactLabel)
        {
            Text label = button == null ? null : button.GetComponentInChildren<Text>();
            if (label == null) return;
            if (compact && !string.IsNullOrWhiteSpace(compactLabel)) label.text = compactLabel;
            label.fontSize = compact ? 16 : 25;
            label.resizeTextForBestFit = compact;
            label.resizeTextMinSize = compact ? 11 : 18;
            label.resizeTextMaxSize = compact ? 16 : 25;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void SetMinimumFontSize(Component root, int minimum)
        {
            if (root == null) return;
            SetMinimumFontSize(root.transform, minimum);
        }

        private static void SetMinimumFontSize(Transform root, int minimum)
        {
            if (root == null) return;
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                text.fontSize = Mathf.Max(text.fontSize, minimum);
                if (text.resizeTextForBestFit)
                {
                    text.resizeTextMinSize = Mathf.Max(text.resizeTextMinSize, Mathf.Max(10, minimum - 3));
                    text.resizeTextMaxSize = Mathf.Max(text.resizeTextMaxSize, minimum);
                }
            }
        }

        private void SetResponsiveAnchors(string childName, Rect rect)
        {
            Transform child = matchTableRoot.Find(childName);
            if (child != null) SetRect(child.GetComponent<RectTransform>(), rect);
        }

        private static void SetRect(Component component, Rect rect)
        {
            if (component != null) SetRect(component.GetComponent<RectTransform>(), rect);
        }

        private static void SetRect(RectTransform rectTransform, Rect rect)
        {
            if (rectTransform == null) return;
            UIFactory.SetAnchors(
                rectTransform,
                new Vector2(rect.xMin, rect.yMin),
                new Vector2(rect.xMax, rect.yMax),
                Vector2.zero,
                Vector2.zero);
        }

        private void CreateMatchPerspectiveBackdrop()
        {
            UIFactory.CreateOfficialPlaymatBackdrop(Root);
            CreateDepthBand("PlaymatReadabilityWash", new Color(1f, 1f, 1f, 0.08f), Vector2.zero, Vector2.one, 0f);
            CreateDepthBand("FarBoardShade", new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.08f), new Vector2(0.07f, 0.61f), new Vector2(0.93f, 0.91f), 0f);
            CreateDepthBand("NearHandRail", new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.12f), new Vector2(0.00f, 0.00f), new Vector2(1.00f, 0.29f), 0f);
        }

        private void CreateDepthBand(string name, Color color, Vector2 anchorMin, Vector2 anchorMax, float rotation)
        {
            GameObject band = new GameObject(name, typeof(RectTransform), typeof(Image));
            band.transform.SetParent(Root, false);
            Image image = band.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = band.GetComponent<RectTransform>();
            UIFactory.SetAnchors(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void UpdateHud()
        {
            if (opponentHudContent != null)
            {
                UIFactory.ClearChildren(opponentHudContent);
                UIFactory.CreateCompactMatchHud(opponentHudContent, opponentLabel, game.Opponent.Health, game.Opponent.Appreciation, game.Turn, true);
            }

            if (playerHudContent != null)
            {
                UIFactory.ClearChildren(playerHudContent);
                UIFactory.CreateCompactMatchHud(playerHudContent, game.Player.DisplayName, game.Player.Health, game.Player.Appreciation, game.Turn, false);
            }

            playerAppreciationMeter?.SetValue(game.Player.Appreciation, true);
            opponentAppreciationMeter?.SetValue(game.Opponent.Appreciation, true);

            UpdateShardStacks();
            UpdateLeaderReadouts();
        }

        private void CreateAppreciationMeters(Transform parent)
        {
            GameObject opponentVessel = new GameObject("OpponentAppreciationReservoir", typeof(RectTransform));
            opponentVessel.transform.SetParent(parent, false);
            UIFactory.SetAnchors(opponentVessel.GetComponent<RectTransform>(), new Vector2(0.160f, 0.720f), new Vector2(0.270f, 0.905f), Vector2.zero, Vector2.zero);
            opponentAppreciationMeter = opponentVessel.AddComponent<AppreciationLiquidMeter>();
            opponentAppreciationMeter.Configure(GameConstants.AppreciationVictoryTarget, UIFactory.Red, new Rect(0.160f, 0.720f, 0.110f, 0.185f));

            GameObject playerVessel = new GameObject("PlayerAppreciationReservoir", typeof(RectTransform));
            playerVessel.transform.SetParent(parent, false);
            UIFactory.SetAnchors(playerVessel.GetComponent<RectTransform>(), new Vector2(0.160f, 0.095f), new Vector2(0.270f, 0.280f), Vector2.zero, Vector2.zero);
            playerAppreciationMeter = playerVessel.AddComponent<AppreciationLiquidMeter>();
            playerAppreciationMeter.Configure(GameConstants.AppreciationVictoryTarget, UIFactory.NeonCyan, new Rect(0.160f, 0.095f, 0.110f, 0.185f));
        }

        private void UpdateLeaderReadouts()
        {
            if (opponentLeaderText != null)
            {
                BattleLeaderDefinition leader = game.Opponent.Leader;
                opponentLeaderText.text = leader == null
                    ? "ENEMY ABILITY"
                    : $"{leader.Name.ToUpperInvariant()}  |  {(game.Opponent.LeaderAbilityUsed ? "USED" : "READY")}";
                opponentLeaderText.color = game.Opponent.LeaderAbilityUsed ? UIFactory.MutedTextColor : UIFactory.Red;
            }

            if (playerLeaderText != null)
            {
                BattleLeaderDefinition leader = game.Player.Leader;
                playerLeaderText.text = leader == null
                    ? "ABILITY READY"
                    : $"{leader.Name.ToUpperInvariant()}  |  {(game.Player.LeaderAbilityUsed ? "USED" : "TAP TO USE")}";
                playerLeaderText.color = game.Player.LeaderAbilityUsed ? UIFactory.MutedTextColor : UIFactory.Accent;
            }
        }

        private void UpdateShardStacks()
        {
            if (opponentShardContent != null)
            {
                UIFactory.ClearChildren(opponentShardContent);
                CreateActionMatReadout(opponentShardContent, "OPPONENT ACTION", game.Opponent.HasCommittedCardThisTurn ? "LOCKED IN" : "HIDDEN", UIFactory.Red);
            }

            if (playerShardContent != null)
            {
                UIFactory.ClearChildren(playerShardContent);
                string detail = selectedHandIndex >= 0 && selectedHandIndex < game.Player.Hand.Count
                    ? $"{game.Player.Hand[selectedHandIndex].GetDiscardCategory().ToUpperInvariant()}"
                    : game.Player.HasCommittedCardThisTurn ? "LOCKED IN" : "CHOOSE 1";
                CreateActionMatReadout(playerShardContent, game.Initiative == OwnerSide.Player ? "INITIATIVE • YOU" : "ACTION", detail, UIFactory.Green);
            }
        }

        private static void CreateActionMatReadout(Transform parent, string label, string detail, Color color)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, label, new Color(1f, 1f, 1f, 0.74f), 0, 3);
            panel.GetComponent<Image>().raycastTarget = false;
            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = 160;
            layout.preferredWidth = 170;
            layout.minHeight = 64;
            layout.preferredHeight = 70;
            layout.flexibleWidth = 1;
            Text labelText = UIFactory.CreateText(panel.transform, label, 13, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);
            Text detailText = UIFactory.CreateText(panel.transform, detail, 15, TextAnchor.MiddleCenter, color, FontStyle.Bold);
            labelText.raycastTarget = false;
            detailText.raycastTarget = false;
        }

        private void InvestArtShard()
        {
            if (combatAnimating || (inviteMatch && localInviteTurnEnded))
            {
                battleAudio?.PlayInvalid();
                return;
            }

            bool invested = game.TryInvestCommunityShield(OwnerSide.Player, out string message);
            if (invested)
            {
                battleAudio?.PlayResourceSpend();
                battleAudio?.PlayShield();
                if (inviteMatch)
                {
                    RecordInviteAction("spend-community-defense", string.Empty, LaneType.Community.ToString());
                }

                UpdateScreen();
                StartCoroutine(PlayShardLaneAnimation(OwnerSide.Player, LaneType.Art, UIFactory.Blue, "WARD +1"));
            }
            else
            {
                battleAudio?.PlayInvalid();
                UpdateScreen();
            }

            ShowMatStatus(message);
        }

        private void InvestBlockchainShard()
        {
            if (combatAnimating || (inviteMatch && localInviteTurnEnded))
            {
                battleAudio?.PlayInvalid();
                return;
            }

            bool invested = game.TryInvestCommunityRally(OwnerSide.Player, out string message);
            if (invested)
            {
                battleAudio?.PlayResourceSpend();
                battleAudio?.PlayRally();
                if (inviteMatch)
                {
                    RecordInviteAction("spend-community-rally", string.Empty, LaneType.Community.ToString());
                }

                UpdateScreen();
                StartCoroutine(PlayShardLaneAnimation(OwnerSide.Player, LaneType.Blockchain, UIFactory.Red, "RALLY +1"));
            }
            else
            {
                battleAudio?.PlayInvalid();
                UpdateScreen();
            }

            ShowMatStatus(message);
        }

        private IEnumerator PlayShardLaneAnimation(OwnerSide side, LaneType sourceLane, Color color, string resultLabel)
        {
            if (matchTableRoot == null || lanesContent == null)
            {
                yield break;
            }

            bool opponentSide = side == OwnerSide.Opponent;
            string sourceButtonName = sourceLane == LaneType.Art
                ? (opponentSide ? "OpponentArtShards" : "PlayerArtShards")
                : (opponentSide ? "OpponentBlockchainShards" : "PlayerBlockchainShards");
            RectTransform sourceRoot = opponentSide ? opponentShardContent : playerShardContent;
            RectTransform source = sourceRoot == null
                ? null
                : sourceRoot.Find(sourceButtonName) as RectTransform;
            RectTransform sourceLaneRect = lanesContent.Find(sourceLane.ToString()) as RectTransform;
            RectTransform communityLane = lanesContent.Find("GrowthLane") as RectTransform;
            if (communityLane == null)
            {
                Debug.LogError($"[MatchUI] Cannot animate {sourceLane} shard spend because the Community lane visual is missing.");
                yield break;
            }

            Vector3 startWorld = source != null
                ? source.TransformPoint(source.rect.center)
                : sourceLaneRect != null
                    ? sourceLaneRect.TransformPoint(sourceLaneRect.rect.center)
                    : communityLane.TransformPoint(communityLane.rect.center);
            Vector3 endWorld = communityLane.TransformPoint(communityLane.rect.center);

            GameObject shard = new GameObject($"{sourceLane}ShardFlight", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            shard.transform.SetParent(matchTableRoot, false);
            RectTransform shardRect = shard.GetComponent<RectTransform>();
            shardRect.sizeDelta = new Vector2(34f, 34f);
            shardRect.position = startWorld;
            shardRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image shardImage = shard.GetComponent<Image>();
            shardImage.color = color;
            shardImage.raycastTarget = false;
            CanvasGroup shardGroup = shard.GetComponent<CanvasGroup>();
            shardGroup.blocksRaycasts = false;

            float elapsed = 0f;
            const float travelDuration = 0.54f;
            while (elapsed < travelDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 46f;
                shardRect.position = Vector3.Lerp(startWorld, endWorld, eased) + arc;
                shardRect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.28f, Mathf.Sin(t * Mathf.PI));
                shardRect.Rotate(0f, 0f, 420f * Time.unscaledDeltaTime);
                yield return null;
            }

            Destroy(shard);

            GameObject burst = new GameObject($"{sourceLane}ShardBurst", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            burst.transform.SetParent(matchTableRoot, false);
            RectTransform burstRect = burst.GetComponent<RectTransform>();
            burstRect.sizeDelta = new Vector2(92f, 92f);
            burstRect.position = endWorld;
            Image burstImage = burst.GetComponent<Image>();
            burstImage.color = new Color(color.r, color.g, color.b, 0.58f);
            burstImage.raycastTarget = false;
            CanvasGroup burstGroup = burst.GetComponent<CanvasGroup>();
            burstGroup.blocksRaycasts = false;

            Text label = UIFactory.CreateText(burst.transform, resultLabel, 15, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);
            label.raycastTarget = false;
            UIFactory.SetAnchors(label.rectTransform, new Vector2(-0.55f, 0.20f), new Vector2(1.55f, 0.80f), Vector2.zero, Vector2.zero);
            label.rectTransform.localRotation = Quaternion.identity;

            elapsed = 0f;
            const float burstDuration = 0.44f;
            while (elapsed < burstDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / burstDuration);
                burstRect.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.75f, 1f - Mathf.Pow(1f - t, 2f));
                burstGroup.alpha = 1f - t;
                yield return null;
            }

            Destroy(burst);
        }

        private IEnumerator PlayLeaderFlash(OwnerSide side, LaneType lane, string label)
        {
            if (matchTableRoot == null || lanesContent == null)
            {
                yield break;
            }

            RectTransform laneRect = lanesContent.Find("GrowthLane") as RectTransform;
            if (laneRect == null)
            {
                yield break;
            }

            Color laneColor = LaneColor(lane);
            GameObject flash = new GameObject("LeaderAbilityFlash", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            flash.transform.SetParent(matchTableRoot, false);
            RectTransform flashRect = flash.GetComponent<RectTransform>();
            flashRect.position = laneRect.TransformPoint(laneRect.rect.center);
            flashRect.sizeDelta = new Vector2(laneRect.rect.width * 0.88f, laneRect.rect.height * 0.58f);
            Image image = flash.GetComponent<Image>();
            image.color = new Color(laneColor.r, laneColor.g, laneColor.b, 0.42f);
            image.raycastTarget = false;
            CanvasGroup group = flash.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            string prefix = side == OwnerSide.Player ? "FOCUS" : "ENEMY FOCUS";
            Text text = UIFactory.CreateText(flash.transform, $"{prefix}\n{label}".ToUpperInvariant(), 18, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = 18;

            float elapsed = 0f;
            const float duration = 0.78f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                flashRect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.18f, pulse);
                group.alpha = 1f - t * 0.52f;
                yield return null;
            }

            Destroy(flash);
        }

        private void CreateLane(LaneType laneType)
        {
            LaneState lane = game.MainLane;
            GameObject lanePanel = UIFactory.CreatePanel(
                lanesContent,
                "PlayerBattlefieldDropSurface",
                Color.clear);
            UIFactory.SetAnchors(
                lanePanel.GetComponent<RectTransform>(),
                new Vector2(0.035f, 0.285f),
                new Vector2(0.965f, 0.685f),
                Vector2.zero,
                Vector2.zero);
            battlefieldDropRect = lanePanel.GetComponent<RectTransform>();
            PlaymatZoneMotion motion = lanePanel.AddComponent<PlaymatZoneMotion>();
            motion.Configure(false, Color.clear);
            MatchLaneDropZone dropZone = lanePanel.AddComponent<MatchLaneDropZone>();
            dropZone.Controller = this;
            dropZone.Lane = LaneType.Community;

            battlefieldDropHint = UIFactory.CreatePanel(lanesContent, "BattlefieldDropHint", new Color(UIFactory.NeonCyan.r, UIFactory.NeonCyan.g, UIFactory.NeonCyan.b, 0.10f));
            RectTransform hintRect = battlefieldDropHint.GetComponent<RectTransform>();
            UIFactory.SetAnchors(hintRect, new Vector2(0.055f, 0.305f), new Vector2(0.945f, 0.665f), Vector2.zero, Vector2.zero);
            Image hintImage = battlefieldDropHint.GetComponent<Image>();
            hintImage.raycastTarget = false;
            Outline hintOutline = battlefieldDropHint.AddComponent<Outline>();
            hintOutline.effectColor = UIFactory.NeonCyan;
            hintOutline.effectDistance = new Vector2(4f, -4f);
            Text hintText = UIFactory.CreateText(battlefieldDropHint.transform, "DROP ANYWHERE ON THE BATTLEFIELD", 22, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.Stretch(hintText.rectTransform);
            hintText.raycastTarget = false;
            hintText.resizeTextForBestFit = true;
            hintText.resizeTextMinSize = 13;
            hintText.resizeTextMaxSize = 22;
            battlefieldDropHint.SetActive(selectedHandIndex >= 0 && CanStartCardDrag(selectedHandIndex));
            battlefieldDropHint.transform.SetSiblingIndex(1);

            GameObject opponentRow = CreateBoardCardRow(lanesContent, lane, OwnerSide.Opponent);
            UIFactory.SetAnchors(opponentRow.GetComponent<RectTransform>(), new Vector2(0.120f, 0.505f), new Vector2(0.880f, 0.665f), Vector2.zero, Vector2.zero);

            GameObject playerRow = CreateBoardCardRow(lanesContent, lane, OwnerSide.Player);
            UIFactory.SetAnchors(playerRow.GetComponent<RectTransform>(), new Vector2(0.120f, 0.335f), new Vector2(0.880f, 0.495f), Vector2.zero, Vector2.zero);
        }

        private void HandleLaneSurfaceClick(LaneType lane)
        {
            if (selectedHandIndex < 0)
            {
                ShowMatStatus("Select a card, then play it on the shared row to choose Build or Discard.");
                return;
            }

            ShowPlayChoiceDialog(selectedHandIndex);
        }

        public void HandleLaneSurfaceClickFromInput(LaneType lane)
        {
            HandleLaneSurfaceClick(lane);
        }

        private void CreateLaneHeader(Transform parent, LaneType laneType, int opponentPower, int playerPower)
        {
            Color laneColor = LaneColor(laneType);
            GameObject header = UIFactory.CreateHorizontalStack(parent, $"{laneType}Header", new Color(1f, 1f, 1f, 0.82f), 5, 5);
            HorizontalLayoutGroup group = header.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childControlHeight = true;
            LayoutElement layout = header.AddComponent<LayoutElement>();
            layout.minHeight = 31;
            layout.preferredHeight = 34;
            layout.flexibleHeight = 0;

            CreateScorePill(header.transform, "OPP", opponentPower, UIFactory.Red);

            GameObject titlePanel = UIFactory.CreateVerticalStack(header.transform, "LaneTitle", Color.clear, 1, 0);
            LayoutElement titleLayout = titlePanel.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1;
            titleLayout.minHeight = 28;
            titleLayout.preferredHeight = 31;
            titleLayout.flexibleHeight = 0;
            UIFactory.CreateText(titlePanel.transform, laneType.ToString().ToUpperInvariant(), 16, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);
            UIFactory.CreateText(titlePanel.transform, LaneSubLabel(laneType), 8, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);

            CreateScorePill(header.transform, "YOU", playerPower, UIFactory.Green);
        }

        private void CreateScorePill(Transform parent, string label, int score, Color color)
        {
            GameObject pill = UIFactory.CreateHorizontalStack(parent, $"{label}Score", new Color(1f, 1f, 1f, 0.90f), 2, 5);
            LayoutElement layout = pill.AddComponent<LayoutElement>();
            layout.minWidth = 58;
            layout.preferredWidth = 64;
            layout.minHeight = 28;
            layout.preferredHeight = 31;
            layout.flexibleWidth = 0;
            layout.flexibleHeight = 0;
            Text tally = UIFactory.CreateText(pill.transform, $"{label} {score}", 14, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);
            tally.resizeTextForBestFit = true;
            tally.resizeTextMinSize = 9;
            tally.resizeTextMaxSize = 14;
            tally.raycastTarget = false;
        }

        private void CreateLaneControlBand(Transform parent, LaneType laneType, int opponentPower, int playerPower)
        {
            string status = playerPower == opponentPower ? "CONTESTED" : playerPower > opponentPower ? "WINNING" : "LOSING";
            if (laneType == LaneType.Community && (game.Player.CommunityShield > 0 || game.Player.CommunityRally > 0))
            {
                status += $"  |  WARD {game.Player.CommunityShield}  RALLY {game.Player.CommunityRally}";
            }
            Color statusColor = playerPower == opponentPower ? UIFactory.Accent : playerPower > opponentPower ? UIFactory.Green : UIFactory.Red;
            GameObject row = UIFactory.CreateHorizontalStack(parent, "LaneStatusRow", Color.clear, 0, 0);
            HorizontalLayoutGroup rowGroup = row.GetComponent<HorizontalLayoutGroup>();
            rowGroup.childAlignment = TextAnchor.MiddleCenter;
            rowGroup.childForceExpandWidth = false;
            rowGroup.childForceExpandHeight = false;
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 16;
            rowLayout.preferredHeight = 20;

            GameObject badge = UIFactory.CreatePanel(row.transform, "LaneStatus", new Color(1f, 1f, 1f, 0.82f));
            LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
            badgeLayout.minWidth = 84;
            badgeLayout.preferredWidth = laneType == LaneType.Community ? 176 : 96;
            badgeLayout.flexibleWidth = 0;
            badgeLayout.minHeight = 14;
            badgeLayout.preferredHeight = 18;
            Text text = UIFactory.CreateText(badge.transform, status, 11, TextAnchor.MiddleCenter, statusColor, FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = 11;
        }

        private static string LaneSubLabel(LaneType laneType)
        {
            switch (laneType)
            {
                case LaneType.Art:
                    return "CREATIVITY";
                case LaneType.Community:
                    return "ALLY BUFFS";
                case LaneType.Blockchain:
                    return "TECH POWER";
                default:
                    return "LANE";
            }
        }

        private static Rect LanePlaymatRect(LaneType laneType)
        {
            float aspect = Screen.height <= 0 ? 1.777f : (float)Screen.width / Screen.height;
            if (aspect < 0.78f)
            {
                return new Rect(0.205f, 0.325f, 0.590f, 0.350f);
            }

            return new Rect(0.205f, 0.325f, 0.590f, 0.350f);
        }

        private static Color LaneColor(LaneType laneType)
        {
            switch (laneType)
            {
                case LaneType.Art:
                    return UIFactory.Blue;
                case LaneType.Community:
                    return UIFactory.Green;
                case LaneType.Blockchain:
                    return UIFactory.Red;
                default:
                    return UIFactory.Accent;
            }
        }

        private GameObject CreateBoardCardRow(Transform parent, LaneState lane, OwnerSide side)
        {
            bool opponentSide = side == OwnerSide.Opponent;
            GameObject row = UIFactory.CreateHorizontalStack(
                parent,
                $"{side}BoardCards",
                Color.clear,
                opponentSide ? 4 : 7,
                1);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            int cardWidth = opponentSide ? OpponentBoardCardWidth : PlayerBoardCardWidth;
            int cardHeight = opponentSide ? OpponentBoardCardHeight : PlayerBoardCardHeight;
            rowLayout.minHeight = cardHeight;
            rowLayout.preferredHeight = cardHeight;
            rowLayout.flexibleHeight = 0;

            if (lane.GetCards(side).Count == 0)
            {
                string emptyLabel = opponentSide
                    ? "OPPONENT ENGINE"
                    : "YOUR ENGINE - DROP A CARD HERE TO BUILD";
                Text empty = UIFactory.CreateText(
                    row.transform,
                    emptyLabel,
                    opponentSide ? 12 : 14,
                    TextAnchor.MiddleCenter,
                    opponentSide ? UIFactory.Red : UIFactory.Green,
                    FontStyle.Bold);
                LayoutElement emptyLayout = empty.gameObject.AddComponent<LayoutElement>();
                emptyLayout.minWidth = 440;
                emptyLayout.preferredWidth = 620;
                emptyLayout.minHeight = cardHeight;
                emptyLayout.preferredHeight = cardHeight;
                empty.resizeTextForBestFit = true;
                empty.resizeTextMinSize = 9;
                empty.resizeTextMaxSize = opponentSide ? 12 : 14;
                empty.raycastTarget = false;
                return row;
            }

            List<BattleCardInstance> boardCards = lane.GetCards(side);
            int cardIndex = 0;
            foreach (BattleCardInstance instance in boardCards)
            {
                GameObject miniCard = UIFactory.CreateMiniCardPanel(
                    row.transform,
                    instance.Definition,
                    $"A {instance.BaseAttack}→{instance.CurrentAttack}  D {instance.BaseDefense}→{instance.CurrentDefense}{(instance.ActiveEffects.Count > 0 ? "  ⇅" : string.Empty)}",
                    false,
                    cardWidth,
                    cardHeight,
                    opponentSide ? 46 : 60);
                miniCard.name = $"BattleCard_{instance.InstanceId}";
                if (instance.IsExhausted)
                {
                    CanvasGroup exhausted = miniCard.AddComponent<CanvasGroup>();
                    exhausted.alpha = 0.58f;
                    miniCard.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0f, 0f, opponentSide ? -6f : 6f);
                }
                CardInspectionTrigger trigger = miniCard.AddComponent<CardInspectionTrigger>();
                trigger.Card = instance.Definition;
                trigger.ClickToInspect = true;
                UiCardMotion motion = miniCard.AddComponent<UiCardMotion>();
                motion.ConfigureHandPosition(cardIndex, lane.GetCards(side).Count, opponentSide);
                motion.ConfigureInteractionScale(1.05f, 1.04f);
                if (seenBoardCardIds.Add(instance.InstanceId))
                {
                    motion.ConfigureBoardDrop(opponentSide);
                }
                if (cardIndex < boardCards.Count - 1)
                {
                    BattleCardInstance neighbor = boardCards[cardIndex + 1];
                    GameObject link = UIFactory.CreatePanel(row.transform, $"Link_{instance.InstanceId}_{neighbor.InstanceId}", Color.clear);
                    LayoutElement linkLayout = link.AddComponent<LayoutElement>();
                    linkLayout.minWidth = 22;
                    linkLayout.preferredWidth = 22;
                    linkLayout.minHeight = cardHeight;
                    linkLayout.preferredHeight = cardHeight;
                    Text linkText = UIFactory.CreateText(link.transform,
                        BattleRules.AreLinked(instance, neighbor) ? "═\n+2\n═" : "",
                        12, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
                    UIFactory.Stretch(linkText.rectTransform);
                    linkText.raycastTarget = false;
                }
                cardIndex += 1;
            }

            return row;
        }

        private void UpdateOpponentHand()
        {
            if (opponentHandContent == null)
            {
                return;
            }

            UIFactory.ClearChildren(opponentHandContent);
            int handCount = inviteMatch
                ? Mathf.Clamp(GameConstants.StartingHandSize + game.Turn * GameConstants.CardsDrawnPerTurn - remotePlayedCards, 0, GameConstants.DecisionHandSize)
                : Mathf.Clamp(game.Opponent.Hand.Count, 0, GameConstants.DecisionHandSize);
            if (drawPresentationActive)
            {
                handCount = Mathf.Min(handCount, presentedOpponentHandCount);
            }

            if (handCount == 0)
            {
                if (!drawPresentationActive)
                {
                    UIFactory.CreateText(opponentHandContent, "No cards", 18, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);
                }
                return;
            }

            for (int i = 0; i < handCount; i++)
            {
                CardDefinition publicCard = i < game.Opponent.Hand.Count && game.Opponent.IsRevealed(game.Opponent.Hand[i]) ? game.Opponent.Hand[i] : null;
                GameObject visual = publicCard == null
                    ? UIFactory.CreateCardBackPanel(opponentHandContent, "APP", MatchHandCardWidth, MatchHandCardHeight)
                    : UIFactory.CreateMatchHandCardPanel(opponentHandContent, publicCard, null, footer: "REVEALED • PUBLIC");
                ApplyHandCardSizing(visual);
                RectTransform visualRect = visual.GetComponent<RectTransform>();
                LayoutElement publicLayout = visual.GetComponent<LayoutElement>();
                if (publicCard != null && publicLayout != null)
                {
                    publicLayout.minWidth = IsPhoneHandLayout ? MobileHandCardWidth : MatchHandCardWidth;
                    publicLayout.preferredWidth = publicLayout.minWidth;
                    publicLayout.minHeight = IsPhoneHandLayout ? MobileHandCardHeight : MatchHandCardHeight;
                    publicLayout.preferredHeight = publicLayout.minHeight;
                    CardInspectionTrigger inspection = visual.AddComponent<CardInspectionTrigger>();
                    inspection.Card = publicCard;
                    inspection.ClickToInspect = true;
                }
                UiCardMotion motion = visual.AddComponent<UiCardMotion>();
                motion.ConfigureHandPosition(i, handCount, true);
                if (drawPresentationActive && i == activeDrawSlot)
                {
                    motion.ConfigureDrawFromDeck(opponentDeckDrawSource, true);
                }
            }
        }

        private void UpdateDiscardMats()
        {
            RebuildCollapsedDiscardStack(playerDiscardContent, game.Player.DiscardPile);
            RebuildCollapsedDiscardStack(opponentDiscardContent, game.Opponent.DiscardPile);
        }

        private bool IsPhoneHandLayout => ResponsiveCanvasScaler.IsPhoneLayout;

        private void ApplyHandCardSizing(GameObject cardPanel)
        {
            if (cardPanel == null) return;
            LayoutElement layout = cardPanel.GetComponent<LayoutElement>();
            if (layout == null) return;
            int width = IsPhoneHandLayout ? MobileHandCardWidth : MatchHandCardWidth;
            int height = IsPhoneHandLayout ? MobileHandCardHeight : MatchHandCardHeight;
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private void ConfigureDiscardStackControl(RectTransform content, bool playerSide)
        {
            if (content == null) return;
            HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) layout.enabled = false;
            Button button = content.GetComponent<Button>() ?? content.gameObject.AddComponent<Button>();
            button.targetGraphic = content.GetComponent<Image>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ToggleDiscardStack(playerSide));
        }

        private static void RebuildCollapsedDiscardStack(RectTransform content, System.Collections.Generic.IReadOnlyList<CardDefinition> discardPile)
        {
            if (content == null)
            {
                return;
            }

            UIFactory.ClearChildren(content);
            int firstVisible = Mathf.Max(0, discardPile.Count - 3);
            for (int index = firstVisible; index < discardPile.Count; index++)
            {
                CardDefinition card = discardPile[index];
                GameObject discardedCard = UIFactory.CreateMiniCardPanel(content, card, $"#{index + 1}  A{card.GetAttack()} D{card.GetDefense()}", false, DiscardCardWidth, DiscardCardHeight, DiscardCardArtHeight);
                discardedCard.name = $"Discarded_{card.id}_{index}";
                RectTransform cardRect = discardedCard.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0.5f, 0.5f);
                cardRect.anchorMax = new Vector2(0.5f, 0.5f);
                cardRect.pivot = new Vector2(0.5f, 0.5f);
                cardRect.anchoredPosition = new Vector2((index - firstVisible) * 2f, (index - firstVisible) * 2f);
                cardRect.localRotation = Quaternion.identity;
                foreach (Graphic graphic in discardedCard.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
        }

        private void ToggleDiscardStack(bool playerSide)
        {
            if (discardStackOverlay != null)
            {
                Destroy(discardStackOverlay);
                discardStackOverlay = null;
                return;
            }

            IReadOnlyList<CardDefinition> pile = playerSide ? game.Player.DiscardPile : game.Opponent.DiscardPile;
            if (pile == null || pile.Count == 0)
            {
                ShowMatStatus("That discard pile is empty.");
                return;
            }

            discardStackOverlay = UIFactory.CreatePanel(matchTableRoot, "DiscardStackInspection", new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.96f));
            RectTransform overlayRect = discardStackOverlay.GetComponent<RectTransform>();
            UIFactory.SetAnchors(overlayRect, new Vector2(0.285f, 0.105f), new Vector2(0.715f, 0.895f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(discardStackOverlay, playerSide ? UIFactory.Green : UIFactory.Red);
            discardStackOverlay.transform.SetAsLastSibling();

            Text title = UIFactory.CreateText(discardStackOverlay.transform, playerSide ? "YOUR DISCARD PILE" : "OPPONENT DISCARD PILE", 18, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.SetAnchors(title.rectTransform, new Vector2(0.06f, 0.905f), new Vector2(0.94f, 0.975f), Vector2.zero, Vector2.zero);
            Text help = UIFactory.CreateText(discardStackOverlay.transform, "TAP A CARD TO EXAMINE • CARDS ARE STACKED IN PLAY ORDER", 12, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.SetAnchors(help.rectTransform, new Vector2(0.06f, 0.855f), new Vector2(0.94f, 0.905f), Vector2.zero, Vector2.zero);

            RectTransform content = UIFactory.CreateScrollContent(discardStackOverlay.transform, "DiscardStackScroll", false, out ScrollRect scroll);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            UIFactory.SetAnchors(scrollRect, new Vector2(0.08f, 0.105f), new Vector2(0.92f, 0.845f), Vector2.zero, Vector2.zero);
            VerticalLayoutGroup listLayout = content.GetComponent<VerticalLayoutGroup>();
            if (listLayout != null)
            {
                listLayout.spacing = -108;
                listLayout.padding = new RectOffset(18, 18, 18, 120);
                listLayout.childAlignment = TextAnchor.UpperCenter;
            }

            for (int index = pile.Count - 1; index >= 0; index--)
            {
                CardDefinition card = pile[index];
                GameObject cardPanel = UIFactory.CreateMiniCardPanel(content, card, $"#{index + 1}  A{card.GetAttack()} D{card.GetDefense()}", false, 122, 183, 66);
                CardInspectionTrigger trigger = cardPanel.AddComponent<CardInspectionTrigger>();
                trigger.Card = card;
                trigger.ClickToInspect = true;
            }

            Button close = UIFactory.CreateButton(discardStackOverlay.transform, "CLOSE DISCARD", () =>
            {
                if (discardStackOverlay != null) Destroy(discardStackOverlay);
                discardStackOverlay = null;
            }, UIFactory.PortalViolet);
            UIFactory.SetAnchors(close.GetComponent<RectTransform>(), new Vector2(0.29f, 0.018f), new Vector2(0.71f, 0.088f), Vector2.zero, Vector2.zero);
            scroll.verticalNormalizedPosition = 1f;
        }

        private void HandleHandCardClick(int handIndex)
        {
            if (handIndex == lastClickedHandIndex && Time.unscaledTime - lastClickedHandTime <= 0.42f)
            {
                lastClickedHandIndex = -1;
                lastClickedHandTime = -1f;
                PlayHandCardAutomatically(handIndex);
                return;
            }

            lastClickedHandIndex = handIndex;
            lastClickedHandTime = Time.unscaledTime;
            SelectHandCard(handIndex);
        }

        private void SelectHandCard(int handIndex)
        {
            battleAudio?.PlayCardSelected();
            selectedHandIndex = handIndex;
            RefreshTransientHandUi();
        }

        public bool CanStartCardDrag(int handIndex)
        {
            if (game == null || game.IsComplete || game.Player.HasCommittedCardThisTurn || game.Player.CommitSkippedThisTurn || playChoiceDialog != null || !CanPlayDuringInviteTurn())
            {
                return false;
            }

            if (handIndex < 0 || handIndex >= game.Player.Hand.Count)
            {
                return false;
            }

            return true;
        }

        public void ExplainBlockedCardDrag(int handIndex)
        {
            string reason;
            if (game == null)
            {
                reason = "The match is still preparing. Try the card again in a moment.";
            }
            else if (game.IsComplete)
            {
                reason = "The match is complete.";
            }
            else if (waitingForPhaseAdvance)
            {
                reason = "This phase is paused. Press NEXT PHASE to continue.";
            }
            else if (combatAnimating)
            {
                reason = "The current action is resolving. Card play resumes after the phase finishes.";
            }
            else if (game.Player.HasCommittedCardThisTurn || game.Player.CommitSkippedThisTurn)
            {
                reason = "Your card action is already committed for this turn. Press NEXT to continue.";
            }
            else if (inviteMatch && localInviteTurnEnded)
            {
                reason = "Your turn is submitted. Waiting for the opponent.";
            }
            else if (playChoiceDialog != null)
            {
                reason = "Finish or close the current card choice first.";
            }
            else if (handIndex < 0 || handIndex >= game.Player.Hand.Count)
            {
                reason = "That card is no longer in your hand.";
            }
            else
            {
                reason = "That card cannot be played during the current phase.";
            }

            battleAudio?.PlayInvalid();
            ShowMatStatus(reason);
        }

        public void MarkDraggingHandCard(int handIndex)
        {
            selectedHandIndex = handIndex;
            RefreshTransientHandUi();
            SetBattlefieldDropHighlight(true);
        }

        public void CancelDraggingHandCard()
        {
            selectedHandIndex = -1;
            RefreshTransientHandUi();
            SetBattlefieldDropHighlight(false);
        }

        public void SetBattlefieldDropHighlight(bool visible)
        {
            if (battlefieldDropHint != null)
            {
                battlefieldDropHint.SetActive(visible);
                if (visible) battlefieldDropHint.transform.SetAsLastSibling();
            }
        }

        public bool IsBattlefieldDropPoint(Vector2 screenPoint, Camera eventCamera)
        {
            return battlefieldDropRect != null &&
                RectTransformUtility.RectangleContainsScreenPoint(battlefieldDropRect, screenPoint, eventCamera);
        }

        public void PlayHandCardFromDrop(int handIndex, LaneType lane)
        {
            selectedHandIndex = handIndex;
            SetBattlefieldDropHighlight(false);
            ShowPlayChoiceDialog(handIndex);
        }

        public void DiscardHandCard(int handIndex)
        {
            if (combatAnimating || game == null || game.IsComplete || !CanPlayDuringInviteTurn())
            {
                battleAudio?.PlayInvalid();
                return;
            }

            if (handIndex < 0 || handIndex >= game.Player.Hand.Count)
            {
                battleAudio?.PlayInvalid();
                return;
            }

            CardDefinition card = game.Player.Hand[handIndex];
            bool discarded = game.TryDiscardCard(OwnerSide.Player, handIndex, out string message);
            selectedHandIndex = -1;
            if (discarded)
            {
                battleAudio?.PlayResourceGain();
                battleAudio?.PlayCardPlaced();
                if (inviteMatch)
                {
                    RecordInviteAction("discard-card", card.id, "Discard");
                }
            }
            else
            {
                battleAudio?.PlayInvalid();
            }

            UpdateScreen();
            ShowMatStatus(message);
            if (discarded && Application.isPlaying) StartCoroutine(PlayDiscardResolution(card, message));
            if (discarded && tutorialMatch && tutorialStep != TutorialStep.Combat)
            {
                tutorialCoreDemonstrated = true;
                LocalSaveSystem.SaveTutorialProgress((int)TutorialStep.HarmfulDiscard, true);
                SetTutorialStep(TutorialStep.HarmfulDiscard);
            }
        }

        private void PlayHandCardAutomatically(int handIndex)
        {
            if (!CanStartCardDrag(handIndex))
            {
                SelectHandCard(handIndex);
                return;
            }

            selectedHandIndex = handIndex;
            ShowPlayChoiceDialog(handIndex);
        }

        private LaneType ChooseAutoLane(CardDefinition card)
        {
            return LaneType.Community;
        }

        private void PlaySelectedCard(LaneType lane)
        {
            if (combatAnimating || selectedHandIndex < 0)
            {
                return;
            }

            if (!CanPlayDuringInviteTurn())
            {
                selectedHandIndex = -1;
                UpdateScreen();
                return;
            }

            if (selectedHandIndex >= game.Player.Hand.Count)
            {
                selectedHandIndex = -1;
                UpdateScreen();
                return;
            }

            CardDefinition selectedCard = game.Player.Hand[selectedHandIndex];
            CardInspectionOverlay.Hide();
            bool played = game.TryBuildCard(OwnerSide.Player, selectedHandIndex, out string message);
            selectedHandIndex = -1;

            if (played)
            {
                battleAudio?.PlayCardPlaced();
            }
            else
            {
                battleAudio?.PlayInvalid();
            }

            if (inviteMatch && played)
            {
                RecordInviteAction("play-card", selectedCard.id, LaneType.Community.ToString());
            }

            UpdateScreen();
            ShowMatStatus(message);
            if (played && tutorialMatch && tutorialStep != TutorialStep.Combat)
            {
                tutorialCoreDemonstrated = true;
                LocalSaveSystem.SaveTutorialProgress((int)TutorialStep.HarmfulDiscard, true);
                SetTutorialStep(TutorialStep.HarmfulDiscard);
            }
        }

        private void EndTurn()
        {
            if (combatAnimating)
            {
                return;
            }

            if (game == null || game.IsComplete || (!game.Player.HasCommittedCardThisTurn && !game.Player.CommitSkippedThisTurn))
            {
                battleAudio?.PlayInvalid();
                ShowMatStatus("Choose one card first: Build it on the shared row or Discard it for its revealed effect.");
                return;
            }

            selectedHandIndex = -1;
            battleAudio?.PlayEndTurn();
            if (tutorialMatch && tutorialStep == TutorialStep.Combat)
            {
                tutorialAwaitingTally = true;
            }
            if (inviteMatch)
            {
                if (localInviteTurnEnded)
                {
                    UpdateScreen();
                    return;
                }

                localInviteTurnEnded = true;
                RecordInviteAction("end-turn", string.Empty, string.Empty);
                if (TryAdvanceInviteTurn())
                {
                    return;
                }
            }
            else
            {
                StartCoroutine(RunCasualCombatSequence());
                return;
            }

            if (game.IsComplete)
            {
                PrepareMatchResultForRewards();
                SceneManager.LoadScene("ResultsScene");
                return;
            }

            UpdateScreen();
        }

        private IEnumerator RunCasualCombatSequence()
        {
            combatAnimating = true;
            game.RunAiTurn();
            battleAudio?.PlayCardPlaced();
            UpdateScreen();
            ShowMatStatus($"{opponentLabel} committed a card. Both unused cards will clear without effects, then Battle begins.");
            yield return new WaitForSecondsRealtime(1.0f);
            game.BeginEndTurnPhase();
            game.ResolveForcedDiscardPhase();
            UpdateScreen();
            ShowMatStatus("Commit resolved: unused cards cleared. Choose attacks, or preserve ready cards to score Appreciation.");
            yield return PlayPacedPhase(BattleTurnPhase.Battle);
            OpenCombatPlanner();
        }

        private void OpenCombatPlanner()
        {
            combatPlanner?.Close();
            combatAnimating = true;
            combatPlanner = CombatPlannerOverlay.Open(matchTableRoot, game, orders =>
            {
                combatPlanner = null;
                pendingCombatPlan = orders;
                StartCoroutine(ResolveFieldBattleAndTallySequence());
            });
        }

        private IEnumerator ResolveFieldBattleAndTallySequence()
        {
            List<BattleAttackOrder> playerPlan = pendingCombatPlan ?? game.BuildAutoAttackPlan(OwnerSide.Player);
            pendingCombatPlan = null;
            game.ResolveCombatPlans(playerPlan, game.BuildAutoAttackPlan(OwnerSide.Opponent), out string combatMessage);
            ShowMatStatus(combatMessage);
            if (combatAnimator != null && game.LastCombatEvents.Count > 0)
            {
                yield return combatAnimator.PlaySequence(
                    game.LastCombatEvents,
                    lanesContent,
                    battleEvent =>
                    {
                        battleAudio?.PlayAttack();
                        ShowMatStatus(battleEvent.Summary());
                    },
                    null,
                    GetDiscardAnimationTarget);
            }

            UpdateScreen();
            yield return PlayPacedPhase(BattleTurnPhase.GatherGrowth);
            game.ResolveGrowthTallyAndAdvanceTurn();
            drawPresentationActive = true;
            presentedPlayerHandCount = 0;
            presentedOpponentHandCount = 0;
            yield return AnimateResolvedCombatAndRefresh();
        }

        private IEnumerator AnimateResolvedCombatAndRefresh()
        {
            BattleTallyResult playerTally = game.LastPlayerTally;
            BattleTallyResult opponentTally = game.LastOpponentTally;
            if (playerTally != null)
            {
                battleAudio?.PlayResourceGain();
                yield return PlayTallyFlash(playerTally, game.Player.DisplayName, UIFactory.Green, false);
            }
            if (opponentTally != null)
            {
                yield return PlayTallyFlash(opponentTally, opponentLabel, UIFactory.Red, true);
            }

            if (game.IsComplete)
            {
                PrepareMatchResultForRewards();
                SceneManager.LoadScene("ResultsScene");
                yield break;
            }

            UpdateScreen();
            if (!tutorialMatch)
            {
                yield return PlayDrawSequence(true);
            }
            combatAnimating = false;
            if (tutorialMatch && tutorialAwaitingTally)
            {
                tutorialAwaitingTally = false;
                SetTutorialStep(TutorialStep.GatherGrowth);
            }
        }

        private IEnumerator PlayTallyFlash(BattleTallyResult tally, string playerName, Color color, bool opponent)
        {
            if (matchTableRoot == null || tally == null)
            {
                yield break;
            }

            UiAudioService.PlayReward();

            GameObject flash = new GameObject("AppreciationTally", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            flash.transform.SetParent(matchTableRoot, false);
            RectTransform rect = flash.GetComponent<RectTransform>();
            UIFactory.SetAnchors(
                rect,
                opponent ? new Vector2(0.30f, 0.53f) : new Vector2(0.30f, 0.37f),
                opponent ? new Vector2(0.70f, 0.63f) : new Vector2(0.70f, 0.47f),
                Vector2.zero,
                Vector2.zero);
            Image image = flash.GetComponent<Image>();
            image.color = new Color(0.025f, 0.018f, 0.105f, 0.96f);
            image.raycastTarget = false;
            CanvasGroup group = flash.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            Text text = UIFactory.CreateText(
                flash.transform,
                $"{(tally.EnteredSpotlight ? "SPOTLIGHT!  " : string.Empty)}{playerName.ToUpperInvariant()}  +{tally.TotalGrowth} GROWTH  →  {tally.EndingAppreciation}/{GameConstants.AppreciationVictoryTarget} APPRECIATION\nBOARD {tally.BoardGrowth}  •  COMBO {tally.CombinationGrowth}  •  BONUS {tally.AbilityGrowth + tally.TriggerGrowth}  •  MOD {tally.ModifierGrowth}",
                15,
                TextAnchor.MiddleCenter,
                color,
                FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = 15;
            text.raycastTarget = false;

            float elapsed = 0f;
            const float duration = 0.72f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * Mathf.Lerp(0.88f, 1.03f, Mathf.Sin(t * Mathf.PI));
                group.alpha = t < 0.72f ? 1f : 1f - (t - 0.72f) / 0.28f;
                yield return null;
            }

            Destroy(flash);
        }

        private RectTransform GetDiscardAnimationTarget(OwnerSide owner)
        {
            return owner == OwnerSide.Player ? playerDiscardContent : opponentDiscardContent;
        }

        private void PrepareMatchResultForRewards()
        {
            if (MatchResultData.LastResult != null && !string.IsNullOrWhiteSpace(matchRewardId))
            {
                MatchResultData.LastResult.matchId = matchRewardId;
                MatchResultData.LastResult.mode = matchMode;
            }
        }

        private void RecordInviteAction(string actionType, string cardId, string lane)
        {
            if (apiClient == null || string.IsNullOrWhiteSpace(inviteCode) || string.IsNullOrWhiteSpace(invitePlayerId))
            {
                return;
            }

            localActionCounter += 1;
            string actionId = $"{invitePlayerId}-{localActionCounter}";
            StartCoroutine(apiClient.RecordInviteAction(
                inviteCode,
                invitePlayerId,
                actionId,
                actionType,
                cardId,
                lane,
                game.Turn,
                _ => { },
                error => Debug.LogWarning($"Invite action sync failed: {error}")));
        }

        private void PollInviteActions()
        {
            if (combatAnimating || apiClient == null || string.IsNullOrWhiteSpace(inviteCode))
            {
                return;
            }

            StartCoroutine(apiClient.GetInviteActions(inviteCode, lastInviteActionSequence, response =>
            {
                if (response?.room?.matchState != null)
                {
                    latestInviteState = response.room.matchState;
                }

                if (ApplyTerminationState())
                {
                    return;
                }

                if (response?.actions == null || response.actions.Length == 0)
                {
                    if (latestInviteState != null && latestInviteState.version != lastInviteStateVersion)
                    {
                        lastInviteStateVersion = latestInviteState.version;
                        UpdateScreen();
                    }
                    return;
                }

                bool changed = false;
                foreach (InviteMatchAction action in response.actions)
                {
                    lastInviteActionSequence = Math.Max(lastInviteActionSequence, action.sequence);
                    if (action.playerId == invitePlayerId)
                    {
                        continue;
                    }

                    if (action.type == "play-card" && TryParseLane(action.lane, out LaneType laneType))
                    {
                        bool applied = game.ApplyRemoteCard(action.cardId, laneType, action.username, out _);
                        if (applied)
                        {
                            remotePlayedCards += 1;
                        }

                        changed |= applied;
                    }
                    else if (action.type == "discard-card" && action.turn == game.Turn)
                    {
                        bool applied = game.ApplyRemoteDiscard(action.cardId, action.username, out _);
                        if (applied)
                        {
                            remotePlayedCards += 1;
                        }

                        changed |= applied;
                    }
                    else if (action.type == "end-turn" && action.turn == game.Turn)
                    {
                        remoteInviteTurnEnded = true;
                        changed = true;
                    }
                    else if (action.type == "leader-ability" && action.turn == game.Turn)
                    {
                        bool applied = game.TryUseLeaderAbility(OwnerSide.Opponent, out _);
                        if (applied && game.Opponent.Leader != null)
                        {
                            battleAudio?.PlayRally();
                            StartCoroutine(PlayLeaderFlash(OwnerSide.Opponent, game.Opponent.Leader.FocusLane, $"ENEMY {game.Opponent.Leader.AbilityName}"));
                        }

                        changed |= applied;
                    }
                    else if (action.turn == game.Turn)
                    {
                        ShowMatStatus("A replay action uses an incompatible earlier rules version and was skipped safely.");
                    }
                }

                if (TryAdvanceInviteTurn())
                {
                    return;
                }

                if (latestInviteState != null && latestInviteState.version != lastInviteStateVersion)
                {
                    lastInviteStateVersion = latestInviteState.version;
                    changed = true;
                }

                if (changed)
                {
                    UpdateScreen();
                }
            }, error => Debug.LogWarning($"Invite action poll failed: {error}")));
        }

        private bool CanPlayDuringInviteTurn()
        {
            return !combatAnimating && (!inviteMatch || !localInviteTurnEnded);
        }

        private string InviteStatusSuffix()
        {
            if (!inviteMatch)
            {
                return string.Empty;
            }

            string waitState = localInviteTurnEnded && remoteInviteTurnEnded
                ? "advancing"
                : localInviteTurnEnded
                    ? "waiting for opponent"
                    : remoteInviteTurnEnded
                        ? "opponent ended"
                        : "live";

            string serverState = latestInviteState == null
                ? string.Empty
                : $"    Server Turn {latestInviteState.currentTurn}/{latestInviteState.maxTurn}";

            return $"    Online {waitState}{serverState}";
        }

        private bool TryAdvanceInviteTurn()
        {
            if (!inviteMatch || !localInviteTurnEnded || !remoteInviteTurnEnded)
            {
                return false;
            }

            localInviteTurnEnded = false;
            remoteInviteTurnEnded = false;
            selectedHandIndex = -1;
            combatAnimating = true;
            StartCoroutine(AnnounceInviteCombatAndOpenPlanner());
            return true;
        }

        private IEnumerator AnnounceInviteCombatAndOpenPlanner()
        {
            yield return new WaitForSecondsRealtime(1.0f);
            game.BeginEndTurnPhase();
            yield return PlayPacedPhase(BattleTurnPhase.EndTurn);
            yield return PlayPacedPhase(BattleTurnPhase.Discard);
            game.ResolveForcedDiscardPhase();
            UpdateScreen();
            if (game.LastPlayerForcedDiscard != null)
            {
                yield return PlayDiscardResolution(game.LastPlayerForcedDiscard, game.LastPlayerForcedDiscardMessage);
            }
            if (game.LastOpponentForcedDiscard != null)
            {
                yield return PlayDiscardResolution(game.LastOpponentForcedDiscard, game.LastOpponentForcedDiscardMessage);
            }
            yield return PlayPacedPhase(BattleTurnPhase.Combat);
            OpenCombatPlanner();
        }

        private static bool TryParseLane(string laneName, out LaneType laneType)
        {
            return Enum.TryParse(laneName, true, out laneType);
        }

        private void OnDestroy()
        {
            CardInspectionOverlay.Hide();
            ClosePlayChoiceDialogImmediate();
            CloseDiscardConfirmation();
            CancelInvoke(nameof(PollInviteActions));
        }
    }
}
