using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.Packs
{
    public class PackOpeningController : ScreenControllerBase
    {
        private const int DefaultAttunementShardCost = 50;
        private const int DefaultAttunementChancePercent = 65;

        public event Action OnPackOpeningStarted;
        public event Action<CardDefinition> OnMysteryCardRevealed;
        public event Action<CardDefinition> OnLegendaryCardRevealed;

        private readonly List<GameObject> rewardCards = new List<GameObject>();

        private PackOpeningService openingService;
        private PackInventoryService inventoryService;
        private BackendApiClient apiClient;
        private PackOpeningAnimationController animationController;
        private PackOpeningAudioController audioController;
        private List<PackDefinition> packs;
        private PackDefinition selectedPack;
        private PackServerInventory authoritativeInventory;
        private string playerId;
        private Lane selectedAttunement = Lane.Neutral;
        private RectTransform packListContent;
        private RectTransform rewardContent;
        private GameObject packVisual;
        private GameObject revealStage;
        private GameObject oddsPanel;
        private GameObject debugPanel;
        private CanvasGroup ritualFlash;
        private Image packArtImage;
        private Image packDepthShadowImage;
        private Text packNameText;
        private Text packDescriptionText;
        private Text packPromptText;
        private Text revealHintText;
        private GameObject rarityBanner;
        private CanvasGroup rarityBannerGroup;
        private Image rarityBannerImage;
        private Text rarityBannerText;
        private Text statusText;
        private Text inventoryText;
        private Text shardStoreBalanceText;
        private Text storeInfoText;
        private Text oddsText;
        private Text sealText;
        private Button openButton;
        private Button fastOpenButton;
        private Button collectionButton;
        private Button backButton;
        private Button shardStoreButton;
        private Button purchasePackButton;
        private Button bossContributeButton;
        private Button bossUnlockButton;
        private Text bossPoolText;
        private GameObject shardStorePanel;
        private BossPoolStatus bossPool;
        private const string AlphaBossPoolId = "alpha_boss";
        private Button artSealButton;
        private Button communitySealButton;
        private Button blockchainSealButton;
        private Button artAttuneButton;
        private Button communityAttuneButton;
        private Button blockchainAttuneButton;
        private Button neutralAttuneButton;
        private PackHoldToOpenInput packHoldInput;
        private GameObject inspectedRewardCard;
        private PackOddsResponse authoritativeOdds;
        private string authoritativeOddsPackId;
        private Lane? activeSeal;
        private bool sealBreakRequested;
        private bool oddsVisible;
        private bool isOpening;
        private string pendingOpenRequestId;
        private string pendingOpenPackId;
        private string pendingOpenAttunement;
        private bool serverInventoryReady;
        private bool revealAdvanceRequested;
        private GameObject activeRevealCard;
        private readonly PackOpeningFlow openingFlow = new PackOpeningFlow();
        private bool revealConfirmationRequested;

        private void Start()
        {
            openingService = new PackOpeningService();
            inventoryService = new PackInventoryService(new PackSaveService());
            apiClient = gameObject.AddComponent<BackendApiClient>();
            playerId = LocalSaveSystem.LoadOrCreatePlayerId();
            animationController = gameObject.AddComponent<PackOpeningAnimationController>();
            audioController = gameObject.AddComponent<PackOpeningAudioController>();

            packs = openingService.LoadPackDefinitions();
            selectedPack = packs.FirstOrDefault();
            RestorePendingOpenContext();
            EnsureValidAttunement(selectedPack);

            BuildUi();
            RefreshAll();
            StartPackIdlePresentation();
            if (selectedPack == null)
            {
                ReportError("Pack opening is unavailable because pack data is missing.", "No valid pack definitions were loaded from Resources/PackData/sample_packs.json.");
                return;
            }

            if (string.IsNullOrWhiteSpace(playerId))
            {
                ReportError("Pack opening is unavailable because player identity could not be created.", "LocalSaveSystem returned an empty playerId.");
                return;
            }

            StartCoroutine(InitializeRitualRoutine());
        }

        private void BuildUi()
        {
            RectTransform playmat = UIFactory.CreateOfficialPlaymatRoot(Root);
            GameObject screen = UIFactory.CreatePanel(playmat, "AppreciationRitual", new Color(0.010f, 0.014f, 0.034f, 0.86f));
            UIFactory.SetAnchors(screen.GetComponent<RectTransform>(), new Vector2(0.015f, 0.025f), new Vector2(0.985f, 0.975f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(screen, UIFactory.PortalViolet);

            GameObject header = UIFactory.CreatePanel(screen.transform, "RitualHeader", new Color(0.012f, 0.018f, 0.045f, 0.92f));
            UIFactory.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0.018f, 0.875f), new Vector2(0.982f, 0.982f), Vector2.zero, Vector2.zero);
            Text title = UIFactory.CreateText(header.transform, "APPRECIATION RITUAL", 38, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            UIFactory.SetAnchors(title.rectTransform, new Vector2(0.025f, 0.34f), new Vector2(0.58f, 0.94f), Vector2.zero, Vector2.zero);
            Text subtitle = UIFactory.CreateText(header.transform, "Open owned packs, reveal five cards, and grow your persistent Appreciation Shard collection.", 17, TextAnchor.MiddleLeft, UIFactory.Cream);
            UIFactory.SetAnchors(subtitle.rectTransform, new Vector2(0.027f, 0.05f), new Vector2(0.74f, 0.38f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(header, UIFactory.NeonCyan);

            GameObject packSection = UIFactory.CreatePanel(screen.transform, "UnopenedPackButtonSlot", Color.clear);
            UIFactory.SetAnchors(packSection.GetComponent<RectTransform>(), new Vector2(0.018f, 0.145f), new Vector2(0.248f, 0.855f), Vector2.zero, Vector2.zero);
            packListContent = UIFactory.CreateScrollContent(packSection.transform, "PackList", false, out ScrollRect packScroll);
            UIFactory.Stretch(packScroll.GetComponent<RectTransform>());
            Image packScrollImage = packScroll.GetComponent<Image>();
            if (packScrollImage != null) packScrollImage.color = Color.clear;
            Image packViewportImage = packScroll.viewport == null ? null : packScroll.viewport.GetComponent<Image>();
            if (packViewportImage != null) packViewportImage.color = Color.clear;
            LayoutElement packScrollLayout = packScroll.gameObject.GetComponent<LayoutElement>();
            packScrollLayout.flexibleHeight = 1;
            packScroll.movementType = ScrollRect.MovementType.Clamped;
            ScrollWheelRelay packWheel = packScroll.gameObject.AddComponent<ScrollWheelRelay>();
            packWheel.Target = packScroll;

            GameObject centerStage = UIFactory.CreatePanel(screen.transform, "RitualStage", Color.clear);
            UIFactory.SetAnchors(centerStage.GetComponent<RectTransform>(), new Vector2(0.260f, 0.145f), new Vector2(0.700f, 0.855f), Vector2.zero, Vector2.zero);
            CreatePackVisual(centerStage.transform);
            CreateRevealStage(centerStage.transform);
            CreateRitualFlash(centerStage.transform);

            GameObject controlSection = CreateSection(screen.transform, "RITUAL STATUS");
            UIFactory.SetAnchors(controlSection.GetComponent<RectTransform>(), new Vector2(0.712f, 0.145f), new Vector2(0.982f, 0.855f), Vector2.zero, Vector2.zero);
            inventoryText = UIFactory.CreateText(controlSection.transform, string.Empty, 17, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            inventoryText.verticalOverflow = VerticalWrapMode.Truncate;
            inventoryText.lineSpacing = 1.15f;
            SetFixedHeight(inventoryText.gameObject, 58);
            shardStoreButton = UIFactory.CreateButton(controlSection.transform, "OPEN APPRECIATION SHARD STORE", ToggleShardStore, UIFactory.Accent);
            MakeCompactButton(shardStoreButton, 34, 13);

            shardStorePanel = UIFactory.CreateVerticalStack(screen.transform, "ShardStorePanel", new Color(0.015f, 0.020f, 0.052f, 0.98f), 7, 12);
            UIFactory.SetAnchors(shardStorePanel.GetComponent<RectTransform>(), new Vector2(0.705f, 0.145f), new Vector2(0.985f, 0.855f), Vector2.zero, Vector2.zero);
            UIFactory.CreateText(shardStorePanel.transform, "APPRECIATION SHARD STORE", 22, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            UIFactory.MakeDimensionalPanel(shardStorePanel, UIFactory.Accent);
            shardStoreBalanceText = UIFactory.CreateText(shardStorePanel.transform, string.Empty, 14, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            SetFixedHeight(shardStoreBalanceText.gameObject, 24);

            GameObject purchaseSection = UIFactory.CreateVerticalStack(shardStorePanel.transform, "PackPurchase", new Color(0.025f, 0.035f, 0.075f, 0.82f), 3, 6);
            LayoutElement purchaseLayout = purchaseSection.AddComponent<LayoutElement>();
            purchaseLayout.minHeight = 236;
            purchaseLayout.preferredHeight = 246;
            Text purchaseTitle = UIFactory.CreateText(purchaseSection.transform, "PURCHASE PACK", 15, TextAnchor.MiddleLeft, UIFactory.Cream, FontStyle.Bold);
            SetFixedHeight(purchaseTitle.gameObject, 20);
            storeInfoText = UIFactory.CreateText(purchaseSection.transform, string.Empty, 13, TextAnchor.UpperLeft, UIFactory.MutedTextColor);
            storeInfoText.verticalOverflow = VerticalWrapMode.Truncate;
            SetFixedHeight(storeInfoText.gameObject, 24);
            purchasePackButton = UIFactory.CreateButton(purchaseSection.transform, "SELECT A STORE PACK", PurchaseSelectedPack, UIFactory.Accent);
            MakeCompactButton(purchasePackButton, 32, 13);
            Button cosmeticsButton = UIFactory.CreateButton(purchaseSection.transform, "COSMETICS  |  CATALOG COMING SOON", () => { }, UIFactory.PortalViolet);
            MakeCompactButton(cosmeticsButton, 30, 11);
            cosmeticsButton.interactable = false;
            bossPoolText = UIFactory.CreateText(purchaseSection.transform, "BOSS VAULT  |  Loading shared pool...", 12, TextAnchor.UpperLeft, UIFactory.Cream, FontStyle.Bold);
            bossPoolText.verticalOverflow = VerticalWrapMode.Truncate;
            SetFixedHeight(bossPoolText.gameObject, 22);
            GameObject bossActions = UIFactory.CreateHorizontalStack(purchaseSection.transform, "BossPoolActions", Color.clear, 5, 0);
            LayoutElement bossActionsLayout = bossActions.AddComponent<LayoutElement>();
            bossActionsLayout.minHeight = 34;
            bossActionsLayout.preferredHeight = 36;
            bossContributeButton = UIFactory.CreateButton(bossActions.transform, "CONTRIBUTE 100", HandleBossPrimaryAction, UIFactory.PortalViolet);
            bossUnlockButton = UIFactory.CreateButton(bossActions.transform, "FUND REMAINDER", () => ContributeToBossPool(true), UIFactory.Red);
            MakeCompactButton(bossContributeButton, 34, 11);
            MakeCompactButton(bossUnlockButton, 34, 11);
            Text nftNote = UIFactory.CreateText(purchaseSection.transform, "Monthly NFT-holder packs: distribution TBD. Wallet ownership is not required for alpha rewards.", 10, TextAnchor.UpperLeft, UIFactory.MutedTextColor);
            SetFixedHeight(nftNote.gameObject, 28);
            Button closeStoreButton = UIFactory.CreateButton(shardStorePanel.transform, "CLOSE STORE", () => SetShardStoreVisible(false), UIFactory.PanelAlt);
            MakeCompactButton(closeStoreButton, 32, 12);
            shardStorePanel.SetActive(false);

            sealText = UIFactory.CreateText(controlSection.transform, string.Empty, 15, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            sealText.verticalOverflow = VerticalWrapMode.Truncate;
            SetFixedHeight(sealText.gameObject, 18);
            statusText = UIFactory.CreateText(controlSection.transform, "Ready.", 16, TextAnchor.UpperLeft, UIFactory.MutedTextColor, FontStyle.Bold);
            statusText.verticalOverflow = VerticalWrapMode.Truncate;
            LayoutElement statusLayout = statusText.gameObject.AddComponent<LayoutElement>();
            statusLayout.flexibleHeight = 1;

            oddsPanel = UIFactory.CreateVerticalStack(controlSection.transform, "OddsPanel", new Color(0.018f, 0.025f, 0.060f, 0.96f), 4, 8);
            oddsText = UIFactory.CreateText(oddsPanel.transform, string.Empty, 13, TextAnchor.UpperLeft, UIFactory.TextColor);
            oddsText.resizeTextForBestFit = true;
            oddsText.resizeTextMinSize = 10;
            oddsText.resizeTextMaxSize = 13;
            oddsPanel.SetActive(false);

            GameObject primaryActions = UIFactory.CreateHorizontalStack(screen.transform, "PrimaryActions", new Color(0.008f, 0.012f, 0.030f, 0.90f), 9, 10);
            UIFactory.SetAnchors(primaryActions.GetComponent<RectTransform>(), new Vector2(0.018f, 0.018f), new Vector2(0.982f, 0.125f), Vector2.zero, Vector2.zero);
            openButton = UIFactory.CreateButton(primaryActions.transform, "OPEN PACK", HandlePrimaryOpenAction, UIFactory.Green);
            fastOpenButton = UIFactory.CreateButton(primaryActions.transform, "FAST OPEN", () => BeginOpen(true), UIFactory.Accent);
            UIFactory.CreateButton(primaryActions.transform, "ODDS", ToggleOdds, UIFactory.Blue);
            collectionButton = UIFactory.CreateButton(primaryActions.transform, "COLLECTION", () => SceneManager.LoadScene("CollectionScene"), UIFactory.PanelAlt);
            Button devButton = UIFactory.CreateButton(primaryActions.transform, "DEV", ToggleDebugPanel, UIFactory.PortalViolet);
            devButton.gameObject.SetActive(HasAdminAccess());
            backButton = BackButton(primaryActions.transform);

            debugPanel = UIFactory.CreateHorizontalStack(screen.transform, "DebugTools", new Color(0.020f, 0.012f, 0.038f, 0.98f), 7, 8);
            UIFactory.SetAnchors(debugPanel.GetComponent<RectTransform>(), new Vector2(0.22f, 0.13f), new Vector2(0.78f, 0.205f), Vector2.zero, Vector2.zero);
            UIFactory.CreateButton(debugPanel.transform, "GRANT + OPEN", DebugGrantAndOpen, UIFactory.PortalViolet);
            UIFactory.CreateButton(debugPanel.transform, "GRANT 1", () => GrantTestPacks(1), UIFactory.PanelAlt);
            UIFactory.CreateButton(debugPanel.transform, "SIM 100", SimulateOneHundred, UIFactory.PanelAlt);
            UIFactory.CreateButton(debugPanel.transform, "PRINT", PrintInventory, UIFactory.PanelAlt);
            UIFactory.CreateButton(debugPanel.transform, "RESET", ResetInventory, UIFactory.Red);
            debugPanel.SetActive(false);
        }

        private void CreatePackVisual(Transform parent)
        {
            packVisual = UIFactory.CreatePanel(parent, "RitualPack", Color.clear);
            RectTransform rect = packVisual.GetComponent<RectTransform>();
            UIFactory.SetAnchors(rect, new Vector2(0.025f, 0.025f), new Vector2(0.975f, 0.975f), Vector2.zero, Vector2.zero);

            GameObject depthShadow = UIFactory.CreatePanel(packVisual.transform, "PackDepthShadow", Color.clear);
            UIFactory.SetAnchors(depthShadow.GetComponent<RectTransform>(), new Vector2(0.158f, 0.280f), new Vector2(0.858f, 0.980f), Vector2.zero, Vector2.zero);
            packDepthShadowImage = depthShadow.GetComponent<Image>();
            packDepthShadowImage.color = new Color(0.01f, 0.01f, 0.04f, 0.62f);
            packDepthShadowImage.preserveAspect = true;
            packDepthShadowImage.raycastTarget = false;
            depthShadow.SetActive(false);

            GameObject art = UIFactory.CreatePanel(packVisual.transform, "PackArt", Color.clear);
            UIFactory.SetAnchors(art.GetComponent<RectTransform>(), new Vector2(0.08f, 0.035f), new Vector2(0.92f, 0.965f), Vector2.zero, Vector2.zero);
            packArtImage = art.GetComponent<Image>();
            packArtImage.preserveAspect = true;
            packArtImage.raycastTarget = false;

            GameObject infoBox = UIFactory.CreateVerticalStack(packVisual.transform, "PackInformation", new Color(0.012f, 0.018f, 0.050f, 0.98f), 4, 8);
            UIFactory.SetAnchors(infoBox.GetComponent<RectTransform>(), new Vector2(0.035f, 0.025f), new Vector2(0.965f, 0.285f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(infoBox, UIFactory.NeonCyan);

            packNameText = UIFactory.CreateText(infoBox.transform, "APPRECIATION PACK", 23, TextAnchor.MiddleCenter, UIFactory.TextColor, FontStyle.Bold);
            SetFixedHeight(packNameText.gameObject, 26);
            packDescriptionText = UIFactory.CreateText(infoBox.transform, string.Empty, 15, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);
            packDescriptionText.resizeTextForBestFit = true;
            packDescriptionText.resizeTextMinSize = 9;
            packDescriptionText.resizeTextMaxSize = 15;
            packDescriptionText.verticalOverflow = VerticalWrapMode.Truncate;
            SetFixedHeight(packDescriptionText.gameObject, 28);
            Text contents = UIFactory.CreateText(infoBox.transform, "5 CARDS  |  3 SEALS  |  1 MYSTERY", 15, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            SetFixedHeight(contents.gameObject, 18);

            Text seals = UIFactory.CreateText(infoBox.transform, "THREE NEUTRAL SEALS", 14, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            SetFixedHeight(seals.gameObject, 16);
            artSealButton = null;
            communitySealButton = null;
            blockchainSealButton = null;

            packPromptText = UIFactory.CreateText(infoBox.transform, "TAP OPEN OR HOLD PACK", 16, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            LayoutElement promptLayout = packPromptText.gameObject.AddComponent<LayoutElement>();
            promptLayout.minHeight = 18;
            promptLayout.preferredHeight = 20;
            promptLayout.flexibleHeight = 0;
            infoBox.SetActive(false);

            GameObject holdSurface = new GameObject("PackHoldSurface", typeof(RectTransform), typeof(Image), typeof(PackHoldToOpenInput));
            holdSurface.transform.SetParent(art.transform, false);
            UIFactory.Stretch(holdSurface.GetComponent<RectTransform>());
            Image holdImage = holdSurface.GetComponent<Image>();
            holdImage.color = new Color(1f, 1f, 1f, 0.001f);
            holdSurface.transform.SetAsLastSibling();
            packHoldInput = holdSurface.GetComponent<PackHoldToOpenInput>();
            packHoldInput.Configure(1.25f, HandlePackHoldProgress, HandlePackHoldCompleted, HandlePackTapped);
        }

        private static bool HasAdminAccess()
        {
            // No production admin identity has been assigned yet. Keep developer
            // controls absent until authenticated account roles are implemented.
            return false;
        }

        private Button CreateSealButton(Transform parent, Lane lane, string label)
        {
            Button button = UIFactory.CreateButton(parent, label, () => RequestSealBreak(lane), LaneColor(lane));
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minHeight = 62;
            layout.preferredHeight = 68;
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = label.Length > 5 ? 15 : 18;
            }
            return button;
        }

        private static void SetFixedHeight(GameObject target, float height)
        {
            if (target == null)
            {
                return;
            }

            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static void MakeCompactButton(Button button, float height, int fontSize)
        {
            if (button == null)
            {
                return;
            }

            float readableHeight = ResponsiveCanvasScaler.IsPhoneLayout ? Mathf.Max(60f, height) : height;
            int readableFontSize = ResponsiveCanvasScaler.IsPhoneLayout ? Mathf.Max(16, fontSize) : fontSize;
            SetFixedHeight(button.gameObject, readableHeight);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = readableFontSize;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = ResponsiveCanvasScaler.IsPhoneLayout ? 13 : 9;
                label.resizeTextMaxSize = readableFontSize;
            }
        }

        private void CreateRevealStage(Transform parent)
        {
            revealStage = UIFactory.CreatePanel(parent, "CardRevealStage", new Color(0.004f, 0.010f, 0.028f, 0.76f));
            UIFactory.Stretch(revealStage.GetComponent<RectTransform>());
            rewardContent = UIFactory.CreatePanel(revealStage.transform, "RevealDeck", Color.clear).GetComponent<RectTransform>();
            UIFactory.SetAnchors(rewardContent, new Vector2(0.02f, 0.10f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
            rarityBanner = UIFactory.CreatePanel(revealStage.transform, "RarityRevealBanner", new Color(0.025f, 0.020f, 0.090f, 0.96f));
            UIFactory.SetAnchors(rarityBanner.GetComponent<RectTransform>(), new Vector2(0.18f, 0.865f), new Vector2(0.82f, 0.975f), Vector2.zero, Vector2.zero);
            rarityBannerImage = rarityBanner.GetComponent<Image>();
            rarityBannerGroup = rarityBanner.AddComponent<CanvasGroup>();
            rarityBannerGroup.alpha = 0f;
            rarityBannerGroup.blocksRaycasts = false;
            rarityBannerGroup.interactable = false;
            rarityBannerText = UIFactory.CreateText(rarityBanner.transform, "RARITY REVEAL", 27, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.Stretch(rarityBannerText.rectTransform);
            rarityBannerText.resizeTextForBestFit = true;
            rarityBannerText.resizeTextMinSize = 16;
            rarityBannerText.resizeTextMaxSize = 29;
            rarityBannerText.raycastTarget = false;
            rarityBanner.SetActive(false);
            revealHintText = UIFactory.CreateText(revealStage.transform, "", 19, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.SetAnchors(revealHintText.rectTransform, new Vector2(0.08f, 0.015f), new Vector2(0.92f, 0.10f), Vector2.zero, Vector2.zero);
            revealStage.SetActive(false);
        }

        private void CreateRitualFlash(Transform parent)
        {
            GameObject flash = new GameObject("RitualFlash", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            flash.transform.SetParent(parent, false);
            UIFactory.Stretch(flash.GetComponent<RectTransform>());
            Image image = flash.GetComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;
            ritualFlash = flash.GetComponent<CanvasGroup>();
            ritualFlash.alpha = 0f;
            ritualFlash.blocksRaycasts = false;
            ritualFlash.interactable = false;
            flash.transform.SetAsLastSibling();
        }

        private void RestorePendingOpenContext()
        {
            if (!LocalSaveSystem.TryLoadPendingPackOpen(out string requestId, out string packId, out string attunement))
            {
                return;
            }

            pendingOpenRequestId = requestId;
            pendingOpenPackId = packId;
            pendingOpenAttunement = attunement;
            PackDefinition pendingPack = packs?.FirstOrDefault(pack => pack != null && pack.id == packId);
            if (pendingPack == null)
            {
                Debug.LogError($"[PackOpening] Pending request '{requestId}' references missing pack definition '{packId}'. The retry token was preserved.");
                return;
            }

            if (!Enum.TryParse(attunement, true, out Lane parsedAttunement) || !IsAttunementAllowed(pendingPack, parsedAttunement))
            {
                Debug.LogWarning($"[PackOpening] Clearing legacy pending request '{requestId}' because lane attunement '{attunement}' is no longer supported.");
                pendingOpenRequestId = null;
                pendingOpenPackId = null;
                pendingOpenAttunement = null;
                LocalSaveSystem.ClearPendingPackOpen();
                selectedAttunement = Lane.Neutral;
                return;
            }

            selectedPack = pendingPack;
            selectedAttunement = parsedAttunement;
        }

        private IEnumerator InitializeRitualRoutine()
        {
            yield return RefreshServerInventoryRoutine();
            yield return RefreshBossPoolRoutine();
            if (!serverInventoryReady || string.IsNullOrWhiteSpace(pendingOpenRequestId))
            {
                yield break;
            }

            if (selectedPack == null || selectedPack.id != pendingOpenPackId ||
                !string.Equals(selectedAttunement.ToString(), pendingOpenAttunement, StringComparison.OrdinalIgnoreCase))
            {
                ReportError("A pending pack retry needs updated local pack data.", $"Could not restore pending request '{pendingOpenRequestId}' for pack '{pendingOpenPackId}' and attunement '{pendingOpenAttunement}'.");
                yield break;
            }

            SetStatus("An unfinished Appreciation Ritual is ready. Tap the hovering pack to resume with the same secured reward request.");
            if (packPromptText != null)
            {
                packPromptText.text = "TAP RESUME OR HOLD PACK";
            }
            ShowPackStage();
        }

        private GameObject CreateSection(Transform parent, string title)
        {
            GameObject section = UIFactory.CreateVerticalStack(parent, title, new Color(0.015f, 0.020f, 0.046f, 0.76f), 7, 10);
            LayoutElement layout = section.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            Text sectionTitle = UIFactory.CreateText(section.transform, title, 16, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            sectionTitle.verticalOverflow = VerticalWrapMode.Truncate;
            SetFixedHeight(sectionTitle.gameObject, 22);
            UIFactory.MakeDimensionalPanel(section, UIFactory.NeonCyan);
            return section;
        }

        private void CreateAttuneButton(Transform parent, Lane lane)
        {
            Button button = UIFactory.CreateButton(parent, lane.ToString().ToUpperInvariant(), () =>
            {
                if (isOpening)
                {
                    return;
                }
                if (!string.IsNullOrWhiteSpace(pendingOpenRequestId))
                {
                    SetStatus("Retry the pending pack opening before changing attunement.");
                    return;
                }

                if (!IsAttunementAllowed(selectedPack, lane))
                {
                    SetStatus($"{lane} Attunement is not available for this pack.");
                    return;
                }

                int attunementCost = CurrentAttunementShardCost();
                int shardBalance = authoritativeInventory?.appreciationShards ?? 0;
                if (lane != Lane.Neutral && shardBalance < attunementCost)
                {
                    SetStatus($"{lane} Attunement costs {attunementCost} Appreciation Shards. Current balance: {shardBalance}.");
                    return;
                }

                selectedAttunement = lane;
                pendingOpenRequestId = null;
                authoritativeOdds = null;
                authoritativeOddsPackId = null;
                SetStatus(lane == Lane.Neutral
                    ? "Natural opening selected. No Appreciation Shards will be spent and all card lanes remain random."
                    : $"{lane} selected: spend {attunementCost} Appreciation Shards for a {CurrentAttunementChancePercent()}% chance that only the final mystery card matches. Rarity odds are unchanged.");
                RefreshAll();
                if (oddsVisible)
                {
                    StartCoroutine(RefreshServerOddsRoutine());
                }
            }, LaneColor(lane));

            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minHeight = 42;
            layout.preferredHeight = 46;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = 16;
            }

            switch (lane)
            {
                case Lane.Neutral:
                    neutralAttuneButton = button;
                    break;
                case Lane.Art:
                    artAttuneButton = button;
                    break;
                case Lane.Community:
                    communityAttuneButton = button;
                    break;
                case Lane.Blockchain:
                    blockchainAttuneButton = button;
                    break;
            }
        }

        private void RefreshAll()
        {
            selectedAttunement = Lane.Neutral;
            RebuildPackList();
            RefreshInventory();
            RefreshOdds();
            RefreshPackVisual();
            RefreshOpenControls();
            RefreshSealText("THREE NEUTRAL SEALS READY");
        }

        private void RefreshPackVisual()
        {
            if (packNameText == null || packDescriptionText == null || packArtImage == null)
            {
                Debug.LogError("[PackOpening] Pack visual references are incomplete.");
                return;
            }

            if (selectedPack == null)
            {
                packNameText.text = "NO PACK SELECTED";
                packDescriptionText.text = "Select a valid pack from the Vault.";
                packArtImage.sprite = null;
                if (packDepthShadowImage != null) packDepthShadowImage.sprite = null;
                packArtImage.color = new Color(0.03f, 0.04f, 0.08f, 1f);
                return;
            }

            packNameText.text = selectedPack.name.ToUpperInvariant();
            packDescriptionText.text = selectedPack.description;
            Sprite packSprite = PackOpeningCinematicController.LoadRenderedSprite("Art/Blender/PackOpening/pack_foil_only_cropped")
                ?? PackCardArtResolver.LoadPackSprite(selectedPack);
            if (packSprite != null)
            {
                packArtImage.sprite = packSprite;
                if (packDepthShadowImage != null)
                {
                    packDepthShadowImage.sprite = packSprite;
                    packDepthShadowImage.preserveAspect = true;
                }
                packArtImage.color = Color.white;
                packArtImage.preserveAspect = true;
            }
            else
            {
                packArtImage.sprite = null;
                if (packDepthShadowImage != null) packDepthShadowImage.sprite = null;
                packArtImage.color = Color.Lerp(LaneColor(selectedPack.featuredLane), Color.black, 0.55f);
            }
        }

        private void RefreshAttunementButtons()
        {
            RefreshAttunementButton(neutralAttuneButton, Lane.Neutral);
            RefreshAttunementButton(artAttuneButton, Lane.Art);
            RefreshAttunementButton(communityAttuneButton, Lane.Community);
            RefreshAttunementButton(blockchainAttuneButton, Lane.Blockchain);
        }

        private void RefreshAttunementButton(Button button, Lane lane)
        {
            if (button == null)
            {
                return;
            }

            bool selected = selectedAttunement == lane;
            Color baseColor = selected ? Color.Lerp(LaneColor(lane), Color.white, 0.18f) : Color.Lerp(LaneColor(lane), Color.black, 0.35f);
            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.18f);
            button.colors = colors;
            bool canAfford = lane == Lane.Neutral || (authoritativeInventory?.appreciationShards ?? 0) >= CurrentAttunementShardCost();
            button.interactable = !isOpening && IsAttunementAllowed(selectedPack, lane) && canAfford;
            string laneLabel = lane == Lane.Neutral ? "NATURAL - FREE" : $"{lane.ToString().ToUpperInvariant()} - {CurrentAttunementShardCost()} APPRECIATION SHARDS";
            SetButtonLabel(button, selected ? $"{laneLabel}  [SELECTED]" : laneLabel);
        }

        private int CurrentAttunementShardCost()
        {
            return authoritativeOdds != null && authoritativeOdds.attunementShardCost > 0
                ? authoritativeOdds.attunementShardCost
                : DefaultAttunementShardCost;
        }

        private int CurrentAttunementChancePercent()
        {
            return authoritativeOdds != null && authoritativeOdds.attunementChancePercent > 0
                ? authoritativeOdds.attunementChancePercent
                : DefaultAttunementChancePercent;
        }

        private void RebuildPackList()
        {
            if (packListContent == null)
            {
                Debug.LogError("[PackOpening] Cannot rebuild pack list because the UI content transform is missing.");
                return;
            }

            foreach (Transform child in packListContent)
            {
                Destroy(child.gameObject);
            }

            List<PackDefinition> unopened = (packs ?? new List<PackDefinition>())
                .Where(pack => pack != null && !string.IsNullOrWhiteSpace(pack.id) && GetAuthoritativePackCount(pack.id) > 0)
                .ToList();

            if (authoritativeInventory?.packs == null)
            {
                selectedPack = selectedPack ?? packs?.FirstOrDefault(pack => pack != null && !string.IsNullOrWhiteSpace(pack.id));
                Text syncing = UIFactory.CreateText(packListContent, "SYNCING UNOPENED PACKS...", 13, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
                SetFixedHeight(syncing.gameObject, 64);
                return;
            }

            if (unopened.Count > 0 && (selectedPack == null || GetAuthoritativePackCount(selectedPack.id) <= 0))
            {
                selectedPack = unopened.FirstOrDefault();
            }

            if (unopened.Count == 0)
            {
                Text empty = UIFactory.CreateText(packListContent, "NO UNOPENED PACKS", 14, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
                SetFixedHeight(empty.gameObject, 64);
                return;
            }

            foreach (PackDefinition pack in unopened)
            {
                PackDefinition capturedPack = pack;
                bool selected = selectedPack == pack;
                string displayName = pack.id == "starter_appreciation_pack"
                    ? "STARTER PACK"
                    : pack.id == "random_appreciation_pack"
                        ? "RANDOM PACK"
                        : $"{pack.minimumMysteryRarity.ToUpperInvariant()} PACK";
                string label = $"{displayName}\nUNOPENED  {GetAuthoritativePackCount(pack.id)}";
                Button button = UIFactory.CreateButton(packListContent, label, () =>
                {
                    if (isOpening)
                    {
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(pendingOpenRequestId))
                    {
                        SetStatus("Retry the pending pack opening before selecting another pack.");
                        return;
                    }

                    selectedPack = capturedPack;
                    selectedAttunement = Lane.Neutral;
                    EnsureValidAttunement(capturedPack);
                    pendingOpenRequestId = null;
                    authoritativeOdds = null;
                    authoritativeOddsPackId = null;
                    SetStatus($"Selected {capturedPack.name}.");
                    ShowPackStage();
                    RefreshAll();
                    if (oddsVisible)
                    {
                        StartCoroutine(RefreshServerOddsRoutine());
                    }
                }, selected ? UIFactory.Accent : UIFactory.PanelAlt);
                LayoutElement layout = button.gameObject.GetComponent<LayoutElement>();
                layout.minHeight = 76;
                layout.preferredHeight = 80;
                Text labelText = button.GetComponentInChildren<Text>();
                if (labelText != null)
                {
                    labelText.fontSize = 11;
                    labelText.resizeTextForBestFit = true;
                    labelText.resizeTextMinSize = 9;
                    labelText.resizeTextMaxSize = 11;
                    labelText.lineSpacing = 1f;
                    labelText.verticalOverflow = VerticalWrapMode.Truncate;
                }
            }
        }

        private void RefreshInventory()
        {
            if (inventoryText == null)
            {
                Debug.LogError("[PackOpening] Inventory UI text reference is missing.");
                return;
            }

            int packCount = selectedPack == null ? 0 : GetAuthoritativePackCount(selectedPack.id);
            int unopenedPacks = authoritativeInventory?.packs?.Sum(entry => entry == null ? 0 : Mathf.Max(0, entry.count)) ?? 0;
            int ownedCards = authoritativeInventory?.ownedCardCount ?? 0;
            int shards = authoritativeInventory?.appreciationShards ?? 0;
            inventoryText.text = $"APPRECIATION SHARDS {shards:N0}\nUNOPENED PACKS {unopenedPacks:N0}  |  SELECTED {packCount:N0}  |  CARDS {ownedCards:N0}";
            if (shardStoreBalanceText != null)
            {
                int matchWins = authoritativeInventory?.matchWinsRewarded ?? 0;
                shardStoreBalanceText.text = $"BALANCE {shards:N0} APPRECIATION SHARDS  |  MATCH WINS PAID {matchWins:N0}";
            }
            if (storeInfoText != null)
            {
                storeInfoText.text = selectedPack == null
                    ? "Select a pack tier from the store."
                    : selectedPack.purchasable
                        ? $"{selectedPack.minimumMysteryRarity.ToUpperInvariant()}+ MYSTERY  |  {selectedPack.shardCost:N0} APPRECIATION SHARDS"
                        : "FREE STARTER  |  RARE+ MYSTERY";
            }
            RefreshBossPoolDisplay();
        }

        private void RefreshBossPoolDisplay()
        {
            if (bossPoolText == null)
            {
                return;
            }
            bossPoolText.text = bossPool == null
                ? "BOSS VAULT  |  Loading shared pool..."
                : bossPool.unlocked
                    ? $"BOSS UNLOCKED  |  {bossPool.totalShards:N0}/{bossPool.targetShards:N0}"
                    : $"BOSS VAULT  {bossPool.totalShards:N0}/{bossPool.targetShards:N0}  |  {bossPool.remainingShards:N0} TO GO";
        }

        private void RefreshOdds()
        {
            if (oddsText == null)
            {
                Debug.LogError("[PackOpening] Odds UI text reference is missing.");
                return;
            }

            if (!oddsVisible)
            {
                oddsText.text = "Tap Odds Preview to retrieve transparent server odds before opening.";
                return;
            }

            oddsText.text = authoritativeOdds != null && authoritativeOddsPackId == selectedPack?.id
                ? PackOddsCalculator.BuildOddsPreview(authoritativeOdds, selectedAttunement)
                : "Connecting to the Vault for authoritative odds...";
        }

        private void RefreshSealText(string text)
        {
            if (sealText == null)
            {
                Debug.LogError("[PackOpening] Seal status UI text reference is missing.");
                return;
            }

            sealText.text = string.IsNullOrWhiteSpace(text) ? "NEUTRAL ODDS  |  NO LANE BIAS" : $"NEUTRAL  |  {text}";
        }

        private void ToggleOdds()
        {
            oddsVisible = !oddsVisible;
            if (oddsPanel != null)
            {
                oddsPanel.SetActive(oddsVisible);
            }
            RefreshOdds();
            if (oddsVisible)
            {
                StartCoroutine(RefreshServerOddsRoutine());
            }
        }

        private void ToggleDebugPanel()
        {
            if (debugPanel != null)
            {
                debugPanel.SetActive(!debugPanel.activeSelf);
            }
        }

        private void ToggleShardStore()
        {
            SetShardStoreVisible(shardStorePanel == null || !shardStorePanel.activeSelf);
        }

        private void SetShardStoreVisible(bool visible)
        {
            if (shardStorePanel == null)
            {
                return;
            }

            shardStorePanel.SetActive(visible);
            if (visible)
            {
                shardStorePanel.transform.SetAsLastSibling();
                RefreshInventory();
            }

            RefreshOpenControls();
        }

        private void HandlePrimaryOpenAction()
        {
            if (isOpening)
            {
                return;
            }

            if (rewardCards.Count > 0 || (revealStage != null && revealStage.activeSelf))
            {
                PrepareAnotherRitual();
                return;
            }

            BeginOpen(false);
        }

        private void HandlePackHoldProgress(float progress)
        {
            if (isOpening)
            {
                return;
            }

            animationController?.SetPackHoldIntensity(progress);
            if (packPromptText == null)
            {
                return;
            }

            if (progress <= 0.001f)
            {
                packPromptText.text = string.IsNullOrWhiteSpace(pendingOpenRequestId)
                    ? "TAP OPEN OR HOLD PACK"
                    : "TAP RESUME OR HOLD PACK";
                return;
            }

            packPromptText.text = $"RITUAL CHARGE  {Mathf.CeilToInt(progress * 100f)}%";
        }

        private void HandlePackHoldCompleted()
        {
            if (isOpening)
            {
                return;
            }

            if (selectedPack == null)
            {
                ReportError("No pack selected.", "The pack visual was tapped without a valid selected pack.");
                return;
            }

            if (!CanOpenSelectedPack())
            {
                SetStatus(serverInventoryReady
                    ? $"You do not own any {selectedPack.name} packs. Earn or grant a pack before beginning the ritual."
                    : "Pack inventory is still loading. Try the hold again in a moment.");
                RefreshOpenControls();
                return;
            }

            SetStatus("Ritual charge complete. Securing the pack reward...");
            BeginOpen(false);
        }

        private void HandlePackTapped()
        {
            if (openingFlow.TryConfirmReveal())
            {
                revealConfirmationRequested = true;
                if (packPromptText != null) packPromptText.text = "APPRECIATING...";
                return;
            }

            if (!isOpening && openingFlow.State == PackOpeningState.Sealed)
            {
                BeginOpen(false);
            }
        }

        private void PurchaseSelectedPack()
        {
            if (isOpening || selectedPack == null)
            {
                return;
            }
            if (!selectedPack.purchasable || selectedPack.shardCost <= 0)
            {
                SetStatus("Starter packs are granted automatically and cannot be purchased.");
                return;
            }

            StartCoroutine(PurchaseSelectedPackRoutine(selectedPack));
        }

        private IEnumerator PurchaseSelectedPackRoutine(PackDefinition packToPurchase)
        {
            SetOpeningState(true);
            SetStatus($"Purchasing {packToPurchase.name} for {packToPurchase.shardCost:N0} Appreciation Shards...");
            PackPurchaseResponse response = null;
            string requestError = null;
            string requestId = $"purchase_{Guid.NewGuid():N}";
            yield return apiClient.PurchasePack(
                requestId,
                playerId,
                packToPurchase.id,
                result => response = result,
                error => requestError = error);

            if (response?.success == true && response.inventory != null && ApplyAuthoritativeInventory(response.inventory))
            {
                SetStatus($"Purchased {response.packName}. {response.remainingShards:N0} Appreciation Shards remain. Hold the pack to open it.");
                ShowPackStage();
            }
            else
            {
                string message = requestError != null && requestError.IndexOf("INSUFFICIENT_SHARDS", StringComparison.OrdinalIgnoreCase) >= 0
                    ? $"You need {packToPurchase.shardCost:N0} Appreciation Shards to purchase this pack."
                    : "Pack purchase was rejected by the server.";
                ReportError(message, $"Purchase request failed for '{packToPurchase.id}': {requestError}");
            }

            SetOpeningState(false);
        }

        private void ContributeToBossPool(bool fundRemainder)
        {
            if (isOpening || authoritativeInventory == null || bossPool == null || bossPool.unlocked)
            {
                return;
            }

            int amount = fundRemainder ? bossPool.remainingShards : Mathf.Min(100, bossPool.remainingShards);
            if (amount <= 0 || authoritativeInventory.appreciationShards < amount)
            {
                SetStatus($"You need {amount:N0} Appreciation Shards for that Boss Vault contribution.");
                return;
            }
            StartCoroutine(ContributeToBossPoolRoutine(amount));
        }

        private void HandleBossPrimaryAction()
        {
            if (bossPool?.unlocked == true)
            {
                SceneManager.LoadScene("BossBattleScene");
                return;
            }

            ContributeToBossPool(false);
        }

        private IEnumerator ContributeToBossPoolRoutine(int amount)
        {
            SetOpeningState(true);
            BossContributionResponse response = null;
            string requestError = null;
            yield return apiClient.ContributeBossShards(
                $"boss_{Guid.NewGuid():N}",
                playerId,
                AlphaBossPoolId,
                amount,
                result => response = result,
                error => requestError = error);

            if (response?.success == true && response.pool != null && response.inventory != null)
            {
                bossPool = response.pool;
                ApplyAuthoritativeInventory(response.inventory);
                SetStatus(response.unlocked
                    ? "Boss Vault unlocked. The future Boss Battle can now begin."
                    : $"Added {response.amountContributed:N0} Appreciation Shards to the Boss Vault.");
            }
            else
            {
                ReportError("Boss Vault contribution was rejected.", requestError);
            }
            SetOpeningState(false);
        }

        private void PrepareAnotherRitual()
        {
            if (!CanOpenSelectedPack())
            {
                int remaining = selectedPack == null ? 0 : GetAuthoritativePackCount(selectedPack.id);
                SetStatus(remaining <= 0
                    ? "No owned packs remain. Earn another pack to begin a new Appreciation Ritual."
                    : "Pack inventory is still loading.");
                RefreshOpenControls();
                return;
            }

            ClearRewards();
            openingFlow.Reset();
            revealConfirmationRequested = false;
            animationController?.ResetPackOpenVisual(packArtImage == null ? null : packArtImage.rectTransform);
            RefreshSealText("THREE NEUTRAL SEALS READY");
            SetStatus($"Another pack is ready. {GetAuthoritativePackCount(selectedPack.id)} owned. Tap Open Pack or hold the hovering pack to begin.");
            SetButtonLabel(openButton, "OPEN PACK");
            if (packPromptText != null)
            {
                packPromptText.text = "TAP OPEN OR HOLD PACK";
            }
            ShowPackStage();
            RefreshOpenControls();
        }

        private void StartPackIdlePresentation()
        {
            if (animationController == null || packVisual == null || isOpening || !packVisual.activeInHierarchy)
            {
                return;
            }

            animationController.StartPackIdleAnimation(packVisual.GetComponent<RectTransform>());
        }

        private void ShowPackStage()
        {
            if (packVisual != null)
            {
                packVisual.SetActive(true);
            }

            if (revealStage != null)
            {
                revealStage.SetActive(false);
            }

            if (ritualFlash != null)
            {
                ritualFlash.alpha = 0f;
            }

            StartPackIdlePresentation();
        }

        private void ShowRevealStage()
        {
            if (packVisual != null)
            {
                packVisual.SetActive(false);
            }

            if (revealStage != null)
            {
                revealStage.SetActive(true);
            }

            if (rarityBanner != null)
            {
                rarityBanner.SetActive(false);
            }
        }

        private void DebugGrantAndOpen()
        {
            if (isOpening)
            {
                return;
            }

            if (selectedPack == null)
            {
                ReportError("No pack selected.", "Debug grant/open was requested without a valid selected pack.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(pendingOpenRequestId))
            {
                SetStatus("Retry the pending pack with Fast Open before granting another pack.");
                return;
            }

            StartCoroutine(DebugGrantAndOpenRoutine(selectedPack, selectedAttunement));
        }

        private void GrantTestPacks(int count)
        {
            if (isOpening)
            {
                return;
            }

            if (selectedPack == null)
            {
                ReportError("No pack selected.", "Test pack grant was requested without a valid selected pack.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(pendingOpenRequestId))
            {
                SetStatus("Retry the pending pack opening before granting test packs.");
                return;
            }

            StartCoroutine(GrantTestPacksRoutine(selectedPack, count));
        }

        private void BeginOpen(bool fast)
        {
            if (isOpening)
            {
                return;
            }

            if (selectedPack == null)
            {
                ReportError("No pack selected.", "Pack open was requested without a valid selected pack.");
                return;
            }

            selectedAttunement = Lane.Neutral;

            if (!CanOpenSelectedPack())
            {
                SetStatus(serverInventoryReady
                    ? $"No {selectedPack.name} packs remain in your inventory."
                    : "Pack inventory is still loading.");
                RefreshOpenControls();
                return;
            }

            bool resumingPendingOpen = !string.IsNullOrWhiteSpace(pendingOpenRequestId);
            if (!fast && resumingPendingOpen && openingFlow.State != PackOpeningState.Sealed)
            {
                openingFlow.Reset();
            }

            if (!fast && !openingFlow.TryBeginTear())
            {
                ReportError(
                    "The ritual animation could not resume. Its visual state has been reset; press Resume Pack Opening again.",
                    $"Resume was blocked in local state '{openingFlow.State}' for request '{pendingOpenRequestId}'.");
                SetOpeningState(false);
                return;
            }

            StartCoroutine(OpenPackRoutine(fast, selectedPack, selectedAttunement));
        }

        private IEnumerator OpenPackRoutine(bool fast, PackDefinition packToOpen, Lane attunement)
        {
            if (packToOpen == null || string.IsNullOrWhiteSpace(packToOpen.id))
            {
                ReportError("Pack opening failed because the selected pack is invalid.", "OpenPackRoutine received a null pack or missing pack id.");
                yield break;
            }

            SetOpeningState(true);
            ClearRewards();
            ShowPackStage();
            SetStatus("Connecting to the Vault...");

            SignedPackRewardResponse serverResponse = null;
            string requestError = null;
            pendingOpenRequestId = string.IsNullOrWhiteSpace(pendingOpenRequestId)
                ? $"pack_{Guid.NewGuid():N}"
                : pendingOpenRequestId;
            pendingOpenPackId = packToOpen.id;
            pendingOpenAttunement = attunement.ToString();
            LocalSaveSystem.SavePendingPackOpen(pendingOpenRequestId, packToOpen.id, attunement.ToString());
            bool openRequestComplete = false;
            Coroutine openRequest = StartCoroutine(apiClient.OpenPack(
                pendingOpenRequestId,
                playerId,
                packToOpen.id,
                attunement.ToString(),
                response =>
                {
                    serverResponse = response;
                    openRequestComplete = true;
                },
                error =>
                {
                    requestError = error;
                    openRequestComplete = true;
                }));

            float requestStartedAt = Time.realtimeSinceStartup;
            float requestDeadline = requestStartedAt + 18f;
            bool slowNoticeShown = false;
            while (!openRequestComplete && Time.realtimeSinceStartup < requestDeadline)
            {
                if (!slowNoticeShown && Time.realtimeSinceStartup - requestStartedAt > 5f)
                {
                    slowNoticeShown = true;
                    SetStatus("The Vault is restoring your saved pack request. Please keep this tab active...");
                }
                yield return null;
            }

            if (!openRequestComplete)
            {
                if (openRequest != null)
                {
                    StopCoroutine(openRequest);
                }

                ReportError(
                    "The Vault took too long to answer. The same pack request is saved; hold the pack again to retry safely.",
                    $"Pack open request timed out locally for '{packToOpen.id}' and request '{pendingOpenRequestId}'.");
                SetOpeningState(false);
                ShowPackStage();
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(requestError))
            {
                string userError = "Pack open was rejected by the server.";
                if (IsDefinitiveOpenRejection(requestError))
                {
                    bool insufficientShards = requestError.IndexOf("INSUFFICIENT_SHARDS", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        requestError.IndexOf("attunement costs", StringComparison.OrdinalIgnoreCase) >= 0;
                    ClearPendingOpenContext();
                    if (insufficientShards)
                    {
                        selectedAttunement = Lane.Neutral;
                        userError = "Not enough Appreciation Shards for that attunement. Natural opening is selected and ready to retry.";
                    }

                    RefreshAll();
                }

                ReportError(userError, $"Backend open request failed for pack '{packToOpen.id}': {requestError}");
                SetOpeningState(false);
                yield break;
            }

            if (serverResponse == null)
            {
                ReportError("Server returned no pack reward.", $"Backend returned a null response for pack '{packToOpen.id}'.");
                SetOpeningState(false);
                yield break;
            }

            if (!serverResponse.TryValidate(playerId, pendingOpenRequestId, packToOpen.id, attunement.ToString(), out string validationError))
            {
                ReportError("Server returned an invalid or incomplete signed pack reward.", validationError);
                SetOpeningState(false);
                yield break;
            }

            // The client cannot verify HMAC without exposing the secret. It requires the signed
            // envelope shape and leaves cryptographic verification/replay checks server-side.
            PackRewardResult result = serverResponse.reward;
            if (result == null)
            {
                ReportError("Server returned no pack reward.", "Validated pack response did not contain a reward payload.");
                SetOpeningState(false);
                yield break;
            }

            if (Enum.TryParse(serverResponse.attunement, true, out Lane confirmedAttunement))
            {
                result.attunement = confirmedAttunement;
            }
            else if (!packToOpen.attunementEnabled)
            {
                result.attunement = Lane.Neutral;
            }
            result.attunementLabel = result.attunement.ToString();
            SetStatus(serverResponse.idempotentReplay
                ? "Vault recovered the completed ritual. Preparing the finalized rewards..."
                : "Server confirmed rewards. Tear the sealed pack...");
            OnPackOpeningStarted?.Invoke();
            audioController.PlayPackStartSfx();
            yield return animationController.PlayPackEnterAnimation(packVisual == null ? null : packVisual.GetComponent<RectTransform>());

            if (!fast)
            {
                SetStatus("The pack is gathering Appreciation...");
                if (packPromptText != null) packPromptText.text = "OPENING PACK";
                yield return animationController.PlayAcceleratingPackSpin(
                    packVisual == null ? null : packVisual.GetComponent<RectTransform>(),
                    ritualFlash);
                openingFlow.MarkOpenGlow();
                openingFlow.WaitForReveal();
                openingFlow.TryConfirmReveal();
                openingFlow.MarkConfetti();
                openingFlow.MarkRevealingCards();
            }
            else
            {
                RefreshSealText("THREE NEUTRAL SEALS BROKEN");
            }

            SetStatus("The pack has opened. Choose each face-down card to reveal it.");
            ShowRevealStage();
            List<GameObject> revealPanels = new List<GameObject>();
            for (int i = 0; i < result.cards.Count; i++)
            {
                GameObject rewardPanel = CreateRewardCard(result.cards[i]);
                if (rewardPanel == null)
                {
                    continue;
                }

                revealPanels.Add(rewardPanel);
                if (!fast)
                {
                    AddUnrevealedCardBack(rewardPanel);
                    PositionUnrevealedCard(rewardPanel.GetComponent<RectTransform>(), i, result.cards.Count);
                }
            }

            for (int i = 0; i < revealPanels.Count; i++)
            {
                PackRewardCardResult reward = result.cards[i];
                GameObject rewardPanel = revealPanels[i];

                RectTransform rewardRect = rewardPanel.GetComponent<RectTransform>();
                if (!fast)
                {
                    rewardPanel.transform.SetAsLastSibling();
                    string slotLabel = string.IsNullOrWhiteSpace(reward.slotLabel) ? $"CARD {reward.slotIndex}" : reward.slotLabel;
                    revealHintText.text = reward.isMysterySlot
                        ? "TAP THE FINAL FACE-DOWN CARD TO REVEAL IT"
                        : $"{slotLabel.ToUpperInvariant()} - TAP THE FACE-DOWN CARD TO REVEAL";
                    SetStatus("Tap the highlighted face-down card to reveal it.");
                    yield return WaitForPlayerCardAdvance(rewardPanel);
                    RemoveUnrevealedCardBack(rewardPanel);
                    audioController.PlayCardRevealSfx();
                    if (reward.isMysterySlot)
                    {
                        yield return animationController.PlayMysteryRevealAnimation(reward.card, rewardRect);
                    }
                    else
                    {
                        yield return animationController.PlayCardRevealAnimation(reward.card, reward.slotIndex, rewardRect);
                    }
                }
                else
                {
                    audioController.PlayCardRevealSfx();
                    animationController.ShowCardImmediately(rewardRect);
                }

                yield return PlayRarityAnnouncement(reward.card, fast);

                if (reward.isMysterySlot)
                {
                    OnMysteryCardRevealed?.Invoke(reward.card);
                }

                if (reward.card != null && reward.card.rarity >= Rarity.Rare)
                {
                    audioController.PlayRareRevealSfx();
                }

                if (reward.card != null && reward.card.rarity == Rarity.Legendary)
                {
                    OnLegendaryCardRevealed?.Invoke(reward.card);
                }

                if (reward.isDuplicate)
                {
                    audioController.PlayDuplicateSfx();
                    if (!fast)
                    {
                        yield return animationController.PlayDuplicateConvertAnimation(reward.card, reward.shardsAwarded);
                    }
                }

                if (!fast)
                {
                    revealHintText.text = reward.isMysterySlot
                        ? "FINAL MYSTERY REVEALED"
                        : "CARD REVEALED";
                    yield return new WaitForSecondsRealtime(reward.isMysterySlot ? 1.35f : 0.90f);
                }

                yield return animationController.PlayCardArchiveAnimation(rewardRect, i, result.cards.Count, fast);
            }

            if (revealHintText != null)
            {
                revealHintText.text = $"APPRECIATION SHARD CACHE  +{result.packShardsAwarded}";
            }
            SetStatus($"Five cards revealed. The pack also contained {result.packShardsAwarded} Appreciation Shards.");
            if (!fast)
            {
                yield return new WaitForSecondsRealtime(1.1f);
            }

            if (!ApplyAuthoritativeInventory(serverResponse.inventory))
            {
                ReportError("Pack reward could not update the local inventory display.", "Signed reward inventory failed local identity validation after reveal.");
                SetOpeningState(false);
                yield break;
            }

            audioController.PlaySummarySfx();
            if (!fast)
            {
                yield return animationController.PlaySummaryAnimation();
            }

            SetStatus(BuildResultSummary(result));
            if (revealHintText != null)
            {
                string mysteryOutcome = result.attunement == Lane.Neutral
                    ? "NEUTRAL MYSTERY"
                    : result.attunementSucceeded ? $"{result.attunement.ToString().ToUpperInvariant()} ATTUNEMENT HIT" : "ATTUNEMENT MISSED";
                revealHintText.text = $"+{result.packShardsAwarded} APPRECIATION SHARDS  |  {mysteryOutcome}  |  REVIEW PULLS OR OPEN ANOTHER";
            }
            ClearPendingOpenContext();
            if (!fast) openingFlow.MarkComplete();
            List<RectTransform> finalCardRects = rewardCards
                .Where(card => card != null)
                .Select(card => card.GetComponent<RectTransform>())
                .Where(rect => rect != null)
                .ToList();
            if (!fast)
            {
                yield return animationController.PlayFinalCardFanAnimation(
                    finalCardRects,
                    result.cards.Where(entry => entry?.card != null).Select(entry => entry.card.rarity).ToList());
            }
            animationController.StartResultCardFloat(
                finalCardRects);
            int remainingPacks = GetAuthoritativePackCount(result.packId);
            SetButtonLabel(openButton, remainingPacks > 0 ? $"PREPARE NEXT ({remainingPacks})" : "NO PACKS REMAINING");
            SetOpeningState(false);
        }

        private IEnumerator WaitForPlayerSealBreak(Lane lane)
        {
            activeSeal = lane;
            sealBreakRequested = false;
            Button sealButton = SealButtonFor(lane);
            if (sealButton == null)
            {
                RefreshSealText($"BREAKING NEUTRAL SEAL {SealNumber(lane)} OF 3");
                yield return new WaitForSecondsRealtime(0.22f);
                sealBreakRequested = true;
            }
            else
            {
                SetSealButtons(lane);
                RefreshSealText($"BREAK NEUTRAL SEAL {SealNumber(lane)} OF 3");
            }

            while (!sealBreakRequested)
            {
                yield return null;
            }

            SetSealButtons(null);
            audioController.PlaySealBreakSfx();
            RefreshSealText($"NEUTRAL SEAL {SealNumber(lane)} OF 3 BROKEN");
            yield return animationController.PlaySealBreakAnimation(lane, sealButton == null ? null : sealButton.GetComponent<RectTransform>());
            activeSeal = null;
        }

        private IEnumerator WaitForPlayerCardAdvance(GameObject cardPanel)
        {
            activeRevealCard = cardPanel;
            revealAdvanceRequested = false;
            Button button = cardPanel == null ? null : cardPanel.GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError("[PackOpening] Reveal card is missing its tap target. Continuing so the finalized reward is not stranded.");
                revealAdvanceRequested = true;
            }

            while (!revealAdvanceRequested)
            {
                yield return null;
            }

            activeRevealCard = null;
            revealAdvanceRequested = false;
        }

        private void RequestRevealAdvance(GameObject cardPanel)
        {
            if (cardPanel == null)
            {
                return;
            }

            if (!isOpening)
            {
                if (rewardCards.Contains(cardPanel))
                {
                    inspectedRewardCard = inspectedRewardCard == cardPanel ? null : cardPanel;
                    animationController?.SetInspectedResultCard(
                        inspectedRewardCard == null ? null : inspectedRewardCard.GetComponent<RectTransform>());
                }
                return;
            }

            if (activeRevealCard != cardPanel)
            {
                return;
            }

            revealAdvanceRequested = true;
        }

        private void ClearRewards()
        {
            animationController?.StopResultCardFloat();
            foreach (GameObject rewardCard in rewardCards)
            {
                Destroy(rewardCard);
            }

            rewardCards.Clear();
            inspectedRewardCard = null;
            activeRevealCard = null;
            revealAdvanceRequested = false;
            if (revealHintText != null)
            {
                revealHintText.text = string.Empty;
            }
        }

        private GameObject CreateRewardCard(PackRewardCardResult reward)
        {
            if (reward?.card == null)
            {
                Debug.LogError("[PackOpening] Reveal UI skipped a reward because its card data was null.");
                return null;
            }

            if (rewardContent == null)
            {
                Debug.LogError($"[PackOpening] Reveal UI cannot display card '{reward.card.id}' because RewardReveal content is missing.");
                return null;
            }

            CardDefinition card = reward.card;
            Color frameColor = RarityColor(card.rarity);
            GameObject panel = UIFactory.CreatePanel(rewardContent, card.name, Color.clear);
            UIFactory.AddNeonFrame(panel, frameColor, 0.42f);
            rewardCards.Add(panel);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(310f, 465f);
            panelRect.anchoredPosition = new Vector2(0f, 12f);
            panelRect.localScale = Vector3.zero;
            panelRect.localRotation = Quaternion.identity;
            panel.transform.SetAsLastSibling();

            Image frameImage = panel.GetComponent<Image>();
            frameImage.color = Color.clear;

            Button revealButton = panel.AddComponent<Button>();
            revealButton.targetGraphic = frameImage;
            revealButton.onClick.AddListener(() => RequestRevealAdvance(panel));
            ColorBlock cardColors = revealButton.colors;
            cardColors.normalColor = Color.white;
            cardColors.highlightedColor = new Color(1f, 1f, 1f, 0.96f);
            cardColors.pressedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
            cardColors.disabledColor = Color.white;
            revealButton.colors = cardColors;

            string cardType = string.IsNullOrWhiteSpace(card.type) ? "CARD" : card.type.ToUpperInvariant();
            string metadata = $"{card.rarity.ToString().ToUpperInvariant()}  |  {cardType}";
            Sprite bakedFace = PackCardArtResolver.LoadCardFaceSprite(card);
            if (bakedFace != null)
            {
                UIFactory.CreateBakedCardVisual(panel.transform, bakedFace, card.rarity.ToString());
            }
            else
            {
                Debug.LogError($"[PackOpening] Missing baked production card face for '{card.id}'.");
                UIFactory.CreateOfficialCardVisual(
                    panel.transform,
                    card.name,
                    card.GetAttack(),
                    card.GetDefense(),
                    card.effectText,
                    metadata,
                    PackCardArtResolver.LoadSprite(card));
            }

            string slotLabel = reward.isMysterySlot
                ? "FINAL MYSTERY"
                : string.IsNullOrWhiteSpace(reward.slotLabel) ? $"CARD {reward.slotIndex}" : reward.slotLabel.ToUpperInvariant();
            Text slotText = UIFactory.CreateText(panel.transform, slotLabel, 11, TextAnchor.MiddleCenter, frameColor, FontStyle.Bold);
            slotText.gameObject.name = "RevealSlotLabel";
            UIFactory.SetAnchors(slotText.rectTransform, new Vector2(0.24f, 0.955f), new Vector2(0.76f, 0.995f), Vector2.zero, Vector2.zero);
            slotText.raycastTarget = false;

            string collectionStatus;
            Color statusColor;
            if (reward.isDuplicate)
            {
                collectionStatus = $"DUPLICATE  +{reward.shardsAwarded} APPRECIATION SHARDS";
                statusColor = UIFactory.NeonCyan;
            }
            else
            {
                collectionStatus = "NEW TO COLLECTION";
                statusColor = UIFactory.Green;
            }

            Text status = UIFactory.CreateText(panel.transform, collectionStatus, 11, TextAnchor.MiddleCenter, statusColor, FontStyle.Bold);
            status.gameObject.name = "CollectionStatus";
            UIFactory.SetAnchors(status.rectTransform, new Vector2(0.18f, 0.006f), new Vector2(0.82f, 0.045f), Vector2.zero, Vector2.zero);
            status.raycastTarget = false;

            return panel;
        }

        private static void PositionUnrevealedCard(RectTransform card, int index, int total)
        {
            if (card == null) return;
            float center = (total - 1) * 0.5f;
            float offset = index - center;
            card.anchoredPosition = new Vector2(offset * 112f, 18f - Mathf.Abs(offset) * 7f);
            card.localScale = Vector3.one * 0.58f;
            card.localRotation = Quaternion.Euler(0f, 0f, offset * -4.5f);
        }

        private static void AddUnrevealedCardBack(GameObject cardPanel)
        {
            if (cardPanel == null) return;
            GameObject back = UIFactory.CreateCardBackPanel(cardPanel.transform, "APP", 310, 465);
            back.name = "UnrevealedCardBack";
            RectTransform backRect = back.GetComponent<RectTransform>();
            UIFactory.Stretch(backRect);
            back.transform.SetAsLastSibling();
            foreach (Graphic graphic in back.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private static void RemoveUnrevealedCardBack(GameObject cardPanel)
        {
            if (cardPanel == null) return;
            Transform back = cardPanel.transform.Find("UnrevealedCardBack");
            if (back != null) back.gameObject.SetActive(false);
        }

        private IEnumerator PlayRarityAnnouncement(CardDefinition card, bool fast)
        {
            if (card == null)
            {
                Debug.LogError("[PackOpening] Cannot announce reveal rarity because the card definition is missing.");
                yield break;
            }

            if (rarityBanner == null || rarityBannerGroup == null || rarityBannerImage == null || rarityBannerText == null)
            {
                Debug.LogError($"[PackOpening] Cannot announce {card.rarity} rarity for '{card.id}' because the rarity banner UI is incomplete.");
                yield break;
            }

            Color rarityColor = RarityColor(card.rarity);
            rarityBannerImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.96f);
            rarityBannerText.color = card.rarity == Rarity.Uncommon || card.rarity == Rarity.Legendary
                ? UIFactory.Ink
                : Color.white;
            rarityBannerText.text = RarityBannerLabel(card.rarity);
            rarityBanner.transform.SetAsLastSibling();
            yield return animationController.PlayRarityBannerAnimation(
                rarityBanner.GetComponent<RectTransform>(),
                rarityBannerGroup,
                card.rarity,
                fast);
        }

        private static string RarityBannerLabel(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Legendary:
                    return "LEGENDARY APPRECIATION";
                case Rarity.Epic:
                    return "EPIC RESONANCE";
                case Rarity.Rare:
                    return "RARE SIGNAL";
                case Rarity.Uncommon:
                    return "UNCOMMON FIND";
                default:
                    return "COMMON DISCOVERY";
            }
        }

        private static void CreatePackLaneStrengthHeader(Transform parent, CardDefinition card)
        {
            GameObject header = UIFactory.CreateHorizontalStack(parent, "LaneStrengths", new Color(0.020f, 0.020f, 0.130f, 0.98f), 4, 4);
            UIFactory.AddNeonFrame(header, UIFactory.PortalViolet, 0.96f);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.minHeight = 48;
            headerLayout.preferredHeight = 50;
            headerLayout.flexibleHeight = 0;

            CreatePackLaneBadge(header.transform, card, Lane.Art, "ART", "♥", UIFactory.HeartRed);
            CreatePackLaneBadge(header.transform, card, Lane.Blockchain, "CHAIN", "◆", new Color(0.09f, 0.41f, 1f));
            CreatePackLaneBadge(header.transform, card, Lane.Community, "COMM", "★", UIFactory.Accent);
        }

        private static void CreatePackLaneBadge(Transform parent, CardDefinition card, Lane lane, string label, string icon, Color color)
        {
            bool strongest = card.StrongestLane() == lane;
            GameObject badge = UIFactory.CreateVerticalStack(parent, $"{lane}Strength", new Color(color.r * 0.18f, color.g * 0.18f, color.b * 0.18f, 0.98f), 0, 2);
            UIFactory.AddNeonFrame(badge, strongest ? color : Color.Lerp(color, UIFactory.Ink, 0.48f), strongest ? 0.98f : 0.62f);
            LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
            badgeLayout.flexibleWidth = 1;
            Text value = UIFactory.CreateText(badge.transform, $"{icon} {card.GetLaneStrength(lane)}", 16, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleHeight = 1;
            Text laneLabel = UIFactory.CreateText(badge.transform, label, 8, TextAnchor.MiddleCenter, strongest ? color : UIFactory.MutedTextColor, FontStyle.Bold);
            SetFixedHeight(laneLabel, 12f);
        }

        private static void CreatePackNameStrip(Transform parent, CardDefinition card)
        {
            GameObject strip = UIFactory.CreateHorizontalStack(parent, "CardNameStrip", new Color(0.20f, 0.055f, 0.42f, 0.98f), 4, 5);
            UIFactory.AddNeonFrame(strip, UIFactory.Accent, 0.82f);
            LayoutElement stripLayout = strip.AddComponent<LayoutElement>();
            stripLayout.minHeight = 36;
            stripLayout.preferredHeight = 38;
            stripLayout.flexibleHeight = 0;
            Text name = UIFactory.CreateText(strip.transform, card.name.ToUpperInvariant(), 18, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 12;
            name.resizeTextMaxSize = 18;
            LayoutElement nameLayout = name.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;
            GameObject cost = UIFactory.CreatePanel(strip.transform, "CostBadge", UIFactory.Accent);
            UIFactory.AddNeonFrame(cost, UIFactory.Ink, 0.96f);
            LayoutElement costLayout = cost.AddComponent<LayoutElement>();
            costLayout.minWidth = 38;
            costLayout.preferredWidth = 38;
            costLayout.flexibleWidth = 0;
            Text costText = UIFactory.CreateText(cost.transform, card.cost.ToString(), 21, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);
            UIFactory.Stretch(costText.rectTransform);
        }

        private static void SetFixedHeight(Text text, float height)
        {
            if (text == null)
            {
                return;
            }

            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private void SimulateOneHundred()
        {
            if (isOpening)
            {
                return;
            }

            if (selectedPack == null)
            {
                ReportError("No pack selected.", "Simulation was requested without a valid selected pack.");
                return;
            }

            StartCoroutine(SimulateOneHundredRoutine(selectedPack, selectedAttunement));
        }

        private void PrintInventory()
        {
            string report = $"Player: {playerId}, owned cards: {authoritativeInventory?.ownedCardCount ?? 0}, shards: {authoritativeInventory?.appreciationShards ?? 0}, selected pack count: {(selectedPack == null ? 0 : GetAuthoritativePackCount(selectedPack.id))}";
            Debug.Log(report);
            SetStatus(report);
        }

        private void ResetInventory()
        {
            if (isOpening)
            {
                return;
            }

            StartCoroutine(ResetInventoryRoutine());
        }

        private IEnumerator RefreshServerInventoryRoutine()
        {
            serverInventoryReady = false;
            string requestError = null;
            PackInventoryResponse response = null;
            yield return apiClient.GetPackInventory(playerId, result => response = result, error => requestError = error);
            if (response?.inventory != null)
            {
                if (ApplyAuthoritativeInventory(response.inventory))
                {
                    serverInventoryReady = true;
                    RefreshAll();
                    SetStatus("Authoritative pack inventory loaded.");
                }
                else
                {
                    ReportError("Pack backend returned an invalid inventory.", "Inventory refresh failed local player identity validation.");
                }
            }
            else if (!string.IsNullOrWhiteSpace(requestError))
            {
                ReportError("Pack backend is unavailable.", $"Authoritative inventory request failed: {requestError}");
            }
            else
            {
                ReportError("Pack backend returned no inventory.", "Inventory response and request error were both empty.");
            }
        }

        private IEnumerator RefreshBossPoolRoutine()
        {
            BossPoolResponse response = null;
            string requestError = null;
            yield return apiClient.GetBossPool(AlphaBossPoolId, result => response = result, error => requestError = error);
            if (response?.success == true && response.pool != null)
            {
                bossPool = response.pool;
                RefreshBossPoolDisplay();
                RefreshOpenControls();
            }
            else
            {
                Debug.LogError($"[Economy] Boss pool could not be loaded: {requestError}");
                if (bossPoolText != null)
                {
                    bossPoolText.text = "BOSS VAULT  |  Shared pool unavailable";
                }
            }
        }

        private IEnumerator RefreshServerOddsRoutine()
        {
            if (selectedPack == null)
            {
                ReportError("Select a pack before viewing odds.", "Authoritative odds were requested without a selected pack.");
                yield break;
            }

            string requestedPackId = selectedPack.id;
            string requestError = null;
            PackOddsResponse response = null;
            yield return apiClient.GetPackOdds(requestedPackId, result => response = result, error => requestError = error);
            if (selectedPack == null || selectedPack.id != requestedPackId)
            {
                yield break;
            }

            if (response?.success == true && response.slots != null && response.slots.Length == 5)
            {
                authoritativeOdds = response;
                authoritativeOddsPackId = requestedPackId;
                RefreshOdds();
                RefreshAttunementButtons();
            }
            else
            {
                ReportError("Authoritative pack odds are unavailable.", $"Odds request failed for pack '{requestedPackId}': {requestError}");
            }
        }

        private IEnumerator DebugGrantAndOpenRoutine(PackDefinition packToOpen, Lane attunement)
        {
            SetOpeningState(true);
            string requestError = null;
            PackGrantResponse response = null;
            yield return apiClient.GrantTestPack(playerId, packToOpen.id, 1, result => response = result, error => requestError = error);
            if (response?.inventory != null)
            {
                if (!ApplyAuthoritativeInventory(response.inventory))
                {
                    ReportError("Debug pack grant returned an invalid inventory.", "Grant response failed local player identity validation.");
                    SetOpeningState(false);
                    yield break;
                }

                SetStatus($"Server granted 1 {packToOpen.name}. Starting debug ritual...");
                yield return OpenPackRoutine(true, packToOpen, attunement);
            }
            else
            {
                ReportError("Debug pack grant failed.", $"Backend grant failed for pack '{packToOpen.id}': {requestError}");
            }

            SetOpeningState(false);
        }

        private IEnumerator GrantTestPacksRoutine(PackDefinition packToGrant, int count)
        {
            SetOpeningState(true);
            string requestError = null;
            PackGrantResponse response = null;
            yield return apiClient.GrantTestPack(playerId, packToGrant.id, count, result => response = result, error => requestError = error);
            if (response?.inventory != null && ApplyAuthoritativeInventory(response.inventory))
            {
                SetStatus($"Granted {response.grantedCount} {packToGrant.name} test pack(s).");
            }
            else
            {
                ReportError("Test pack grant failed or is disabled on this server.", $"Grant request for '{packToGrant.id}' failed: {requestError}");
            }

            SetOpeningState(false);
        }

        private IEnumerator SimulateOneHundredRoutine(PackDefinition packToSimulate, Lane attunement)
        {
            string requestError = null;
            PackSimulationResponse response = null;
            yield return apiClient.SimulatePackOpenings(
                packToSimulate.id,
                attunement.ToString(),
                100,
                result => response = result,
                error => requestError = error);

            if (response?.distribution == null)
            {
                ReportError("Server simulation failed.", $"Simulation failed for pack '{packToSimulate.id}': {requestError}");
                yield break;
            }

            string report =
                $"Server simulated {response.cardsOpened} cards:\n" +
                $"Common {response.distribution.Common}, Uncommon {response.distribution.Uncommon}, " +
                $"Rare {response.distribution.Rare}, Epic {response.distribution.Epic}, Legendary {response.distribution.Legendary}\n" +
                $"Lanes: Art {response.laneDistribution?.Art ?? 0}, Community {response.laneDistribution?.Community ?? 0}, " +
                $"Blockchain {response.laneDistribution?.Blockchain ?? 0}, Neutral {response.laneDistribution?.Neutral ?? 0}\n" +
                $"Duplicates {response.duplicateCount}, Shards {response.totalShardsAwarded}, Average shards/pack {response.averageShardsPerPack:0.0}";
            Debug.Log(report);
            SetStatus(report);
        }

        private IEnumerator ResetInventoryRoutine()
        {
            string requestError = null;
            PackResetResponse response = null;
            yield return apiClient.ResetTestPackInventory(playerId, result => response = result, error => requestError = error);
            if (response?.inventory != null)
            {
                if (!ApplyAuthoritativeInventory(response.inventory))
                {
                    ReportError("Server inventory reset returned invalid data.", "Reset response failed local player identity validation.");
                    yield break;
                }

                ClearRewards();
                pendingOpenRequestId = null;
                pendingOpenPackId = null;
                pendingOpenAttunement = null;
                LocalSaveSystem.ClearPendingPackOpen();
                SetStatus("Authoritative alpha inventory reset.");
            }
            else
            {
                ReportError("Server inventory reset failed.", $"Reset request failed for player '{playerId}': {requestError}");
            }
        }

        private bool ApplyAuthoritativeInventory(PackServerInventory inventory)
        {
            if (inventory == null)
            {
                Debug.LogError("[PackOpening] Cannot apply a null authoritative inventory snapshot.");
                return false;
            }

            if (!string.Equals(inventory.playerId, playerId, StringComparison.Ordinal))
            {
                Debug.LogError($"[PackOpening] Rejected inventory for player '{inventory.playerId ?? "<missing>"}'; expected '{playerId}'.");
                return false;
            }

            authoritativeInventory = inventory;
            inventoryService.ReplaceWithAuthoritativeSnapshot(inventory);
            RefreshAll();
            return true;
        }

        private int GetAuthoritativePackCount(string packId)
        {
            if (authoritativeInventory?.packs == null)
            {
                return 0;
            }

            PackServerPackEntry entry = authoritativeInventory.packs.FirstOrDefault(pack => pack != null && pack.packId == packId);
            return entry?.count ?? 0;
        }

        private string BuildResultSummary(PackRewardResult result)
        {
            if (result?.cards == null)
            {
                Debug.LogError("[PackOpening] Cannot build a ritual summary because the reward result or card list is missing.");
                return "Ritual complete, but the reward summary is unavailable.";
            }

            int newCards = result.cards.Count(card => !card.isDuplicate);
            int duplicates = result.cards.Count(card => card.isDuplicate);
            int remainingPacks = GetAuthoritativePackCount(result.packId);
            int shardBalance = authoritativeInventory?.appreciationShards ?? 0;
            int netShards = result.totalShardsAwarded - result.attunementShardsSpent;
            string attunementSummary = result.attunement == Lane.Neutral
                ? "Natural opening: no Appreciation Shards spent; all lanes rolled naturally."
                : $"{result.attunement} attunement: {result.attunementShardsSpent} Appreciation Shards spent; final mystery " +
                  (result.attunementSucceeded ? "matched the chosen lane." : "rolled outside the chosen lane.");
            return $"{result.packName} opened.\nCards acquired: {result.cards.Count}. New: {newCards}. Duplicates: {duplicates}.\n" +
                $"Pack Appreciation Shards: +{result.packShardsAwarded}. Duplicate Appreciation Shards: +{result.totalDuplicateShards}. Net: {netShards:+#;-#;0}.\n" +
                $"{attunementSummary}\nTotal Appreciation Shards: {shardBalance}. Remaining packs: {remainingPacks}.";
        }

        private void RequestSealBreak(Lane lane)
        {
            if (!isOpening || activeSeal != lane)
            {
                return;
            }

            sealBreakRequested = true;
            SetSealButtons(null);
        }

        private void SetSealButtons(Lane? enabledLane)
        {
            if (artSealButton != null)
            {
                artSealButton.interactable = enabledLane == Lane.Art;
            }

            if (communitySealButton != null)
            {
                communitySealButton.interactable = enabledLane == Lane.Community;
            }

            if (blockchainSealButton != null)
            {
                blockchainSealButton.interactable = enabledLane == Lane.Blockchain;
            }
        }

        private Button SealButtonFor(Lane lane)
        {
            switch (lane)
            {
                case Lane.Art: return artSealButton;
                case Lane.Community: return communitySealButton;
                case Lane.Blockchain: return blockchainSealButton;
                default: return null;
            }
        }

        private static int SealNumber(Lane lane)
        {
            switch (lane)
            {
                case Lane.Art: return 1;
                case Lane.Community: return 2;
                case Lane.Blockchain: return 3;
                default: return 0;
            }
        }

        private void SetOpeningState(bool opening)
        {
            isOpening = opening;
            if (animationController != null)
            {
                if (opening)
                {
                    animationController.StopPackIdleAnimation(true);
                }
                else if (packVisual != null && packVisual.activeInHierarchy)
                {
                    StartPackIdlePresentation();
                }
            }

            if (collectionButton != null)
            {
                collectionButton.interactable = !opening;
            }

            if (backButton != null)
            {
                backButton.interactable = true;
            }

            if (packPromptText != null && opening)
            {
                packPromptText.text = "RITUAL IN PROGRESS";
            }

            if (!opening)
            {
                if (openingFlow.State != PackOpeningState.Complete && rewardCards.Count == 0)
                {
                    openingFlow.Reset();
                    revealConfirmationRequested = false;
                }
                activeSeal = null;
                sealBreakRequested = false;
                SetSealButtons(null);
                if (packPromptText != null && packVisual != null && packVisual.activeInHierarchy)
                {
                    packPromptText.text = string.IsNullOrWhiteSpace(pendingOpenRequestId)
                        ? "TAP OPEN OR HOLD PACK"
                        : "TAP RETRY OR HOLD PACK";
                }
            }

            RefreshAttunementButtons();
            RefreshOpenControls();
        }

        private bool CanOpenSelectedPack()
        {
            if (!serverInventoryReady || selectedPack == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(pendingOpenRequestId) || GetAuthoritativePackCount(selectedPack.id) > 0;
        }

        private void RefreshOpenControls()
        {
            bool hasResultOnScreen = rewardCards.Count > 0 || (revealStage != null && revealStage.activeSelf);
            bool waitingForReveal = isOpening && openingFlow.State == PackOpeningState.WaitingForReveal;
            bool canOpen = !isOpening && CanOpenSelectedPack();
            if (packHoldInput != null)
            {
                packHoldInput.SetInteractable((canOpen && !hasResultOnScreen) || waitingForReveal);
            }

            if (fastOpenButton != null)
            {
                fastOpenButton.interactable = canOpen && !hasResultOnScreen;
            }

            if (openButton != null)
            {
                openButton.interactable = canOpen;
                SetButtonLabel(openButton, hasResultOnScreen
                    ? "OPEN ANOTHER PACK"
                    : string.IsNullOrWhiteSpace(pendingOpenRequestId) ? "OPEN PACK" : "RESUME PACK OPENING");
            }

            if (purchasePackButton != null)
            {
                bool purchasable = !isOpening && selectedPack != null && selectedPack.purchasable && selectedPack.shardCost > 0;
                int shardBalance = authoritativeInventory?.appreciationShards ?? 0;
                purchasePackButton.interactable = purchasable && shardBalance >= selectedPack.shardCost;
                SetButtonLabel(
                    purchasePackButton,
                    selectedPack == null
                        ? "SELECT A STORE PACK"
                        : selectedPack.purchasable
                            ? $"BUY PACK  |  {selectedPack.shardCost:N0} APPRECIATION SHARDS"
                            : "3 STARTER PACKS GRANTED");
            }

            if (shardStoreButton != null)
            {
                shardStoreButton.interactable = !isOpening;
                SetButtonLabel(shardStoreButton, shardStorePanel != null && shardStorePanel.activeSelf ? "CLOSE APPRECIATION SHARD STORE" : "OPEN APPRECIATION SHARD STORE");
            }

            int balance = authoritativeInventory?.appreciationShards ?? 0;
            int remainingBossCost = bossPool?.remainingShards ?? 2000;
            bool bossAvailable = !isOpening && bossPool != null && !bossPool.unlocked && remainingBossCost > 0;
            if (bossContributeButton != null)
            {
                int contribution = Mathf.Min(100, remainingBossCost);
                bossContributeButton.interactable = bossPool?.unlocked == true || (bossAvailable && balance >= contribution);
                SetButtonLabel(bossContributeButton, bossPool?.unlocked == true ? "ENTER BOSS BATTLES" : $"CONTRIBUTE {contribution:N0}");
            }
            if (bossUnlockButton != null)
            {
                bossUnlockButton.interactable = bossAvailable && balance >= remainingBossCost;
                SetButtonLabel(bossUnlockButton, bossPool?.unlocked == true ? "UNLOCKED" : $"FUND {remainingBossCost:N0}");
            }

            if (!isOpening && packPromptText != null && packVisual != null && packVisual.activeInHierarchy)
            {
                packPromptText.text = canOpen
                    ? (string.IsNullOrWhiteSpace(pendingOpenRequestId) ? "TAP OPEN OR HOLD PACK" : "TAP RETRY OR HOLD PACK")
                    : serverInventoryReady ? "NO OWNED PACKS" : "LOADING PACK INVENTORY";
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            Text text = button == null ? null : button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = label;
            }
        }

        private void EnsureValidAttunement(PackDefinition pack)
        {
            if (pack == null)
            {
                return;
            }

            if (!pack.attunementEnabled)
            {
                selectedAttunement = Lane.Neutral;
                return;
            }

            if (IsAttunementAllowed(pack, selectedAttunement))
            {
                return;
            }

            selectedAttunement = Lane.Neutral;
        }

        private static bool IsAttunementAllowed(PackDefinition pack, Lane lane)
        {
            if (pack == null)
            {
                return false;
            }

            if (lane == Lane.Neutral)
            {
                return true;
            }

            if (!pack.attunementEnabled)
            {
                return false;
            }

            return (pack.validAttunements ?? Array.Empty<string>())
                .Any(value => string.Equals(value, lane.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        private void ClearPendingOpenContext()
        {
            pendingOpenRequestId = null;
            pendingOpenPackId = null;
            pendingOpenAttunement = null;
            LocalSaveSystem.ClearPendingPackOpen();
        }

        private static bool IsDefinitiveOpenRejection(string error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            string[] rejectionSignals =
            {
                "INSUFFICIENT_SHARDS",
                "REQUEST_ID_CONFLICT",
                "PACK_NOT_FOUND",
                "PACK_CARD_POOL_INCOMPLETE",
                "does not own this pack",
                "Unknown pack definition",
                "Attunement must be"
            };
            return rejectionSignals.Any(signal => error.IndexOf(signal, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void ReportError(string userMessage, string technicalDetails)
        {
            Debug.LogError($"[PackOpening] {technicalDetails}");
            SetStatus(userMessage);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private static Color RarityColor(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Legendary:
                    return UIFactory.Accent;
                case Rarity.Epic:
                    return UIFactory.NeonPink;
                case Rarity.Rare:
                    return UIFactory.NeonCyan;
                case Rarity.Uncommon:
                    return new Color(0.38f, 0.78f, 0.46f);
                default:
                    return new Color(0.72f, 0.68f, 0.62f);
            }
        }

        private static Color LaneColor(Lane lane)
        {
            switch (lane)
            {
                case Lane.Art:
                    return new Color(0.96f, 0.34f, 0.76f);
                case Lane.Community:
                    return new Color(0.08f, 0.78f, 0.36f);
                case Lane.Blockchain:
                    return new Color(0.10f, 0.70f, 1.00f);
                default:
                    return UIFactory.MutedTextColor;
            }
        }
    }
}
