using System;
using System.Collections;
using System.Runtime.InteropServices;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class Web3MockScreenController : ScreenControllerBase
    {
        private const string LocalOneOfOneQaWallet = "0x1111111111111111111111111111111111110001";
        private BackendApiClient apiClient;
        private string playerId;
        private InputField walletInput;
        private InputField apiInput;
        private InputField adminWalletInput;
        private Text accountText;
        private Text connectionText;
        private Text eligibilityText;
        private Text ownershipText;
        private Text ownershipApprovalMark;
        private Image assetPreviewImage;
        private Sprite verifiedOwnerPreviewSprite;
        private PremiumTextShimmer bossModeShimmer;
        private Text messageText;
        private Button linkButton;
        private Button syncButton;
        private Button disconnectButton;
        private GameObject adminControls;
        private WalletAccountStatus walletStatus;
        private WalletChallengeResponse activeChallenge;
        private string pendingWalletAddress;
        private int selectedBossAssetIndex;
        private bool requestActive;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AppreciatorsRequestWalletConnection(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void AppreciatorsSignWalletMessage(string gameObjectName, string walletAddress, string message);
#endif

        private void Start()
        {
            apiClient = gameObject.AddComponent<BackendApiClient>();
            playerId = LocalSaveSystem.LoadOrCreatePlayerId();

            GameObject shell = UIFactory.CreatePanel(Root, "WalletAccessShell", UIFactory.GlassPanel);
            UIFactory.SetAnchors(shell.GetComponent<RectTransform>(), new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f), Vector2.zero, Vector2.zero);
            GameObject header = UIFactory.CreateVerticalStack(shell.transform, "WalletHeader", UIFactory.PanelAlt, 0, 5);
            UIFactory.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0.03f, 0.83f), new Vector2(0.97f, 0.97f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(header, UIFactory.NeonCyan);
            Text headerTitle = UIFactory.CreateText(header.transform, "WALLET & HOLDER ACCESS", 31, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            SetHeight(headerTitle.gameObject, 34);
            Text headerSubtitle = UIFactory.CreateText(header.transform, "SIGNED WALLET  •  APECHAIN ASSETS  •  VERIFIED 1-OF-1 ACCESS", 14, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            SetHeight(headerSubtitle.gameObject, 22);

            GameObject accountPanel = CreateAccountPanel(shell.transform);
            UIFactory.SetAnchors(accountPanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.65f), new Vector2(0.34f, 0.80f), Vector2.zero, Vector2.zero);
            GameObject connectionPanel = CreateConnectionPanel(shell.transform);
            UIFactory.SetAnchors(connectionPanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.20f), new Vector2(0.34f, 0.63f), Vector2.zero, Vector2.zero);
            GameObject eligibilityPanel = CreateEligibilityPanel(shell.transform);
            UIFactory.SetAnchors(eligibilityPanel.GetComponent<RectTransform>(), new Vector2(0.355f, 0.52f), new Vector2(0.68f, 0.80f), Vector2.zero, Vector2.zero);
            GameObject ownershipPanel = CreateOwnershipPanel(shell.transform);
            UIFactory.SetAnchors(ownershipPanel.GetComponent<RectTransform>(), new Vector2(0.355f, 0.20f), new Vector2(0.68f, 0.50f), Vector2.zero, Vector2.zero);
            GameObject apiPanel = CreateApiPanel(shell.transform);
            UIFactory.SetAnchors(apiPanel.GetComponent<RectTransform>(), new Vector2(0.695f, 0.52f), new Vector2(0.97f, 0.80f), Vector2.zero, Vector2.zero);
            GameObject boundaryPanel = UIFactory.CreateVerticalStack(shell.transform, "VerificationBoundary", UIFactory.PanelAlt, 5, 10);
            UIFactory.SetAnchors(boundaryPanel.GetComponent<RectTransform>(), new Vector2(0.695f, 0.20f), new Vector2(0.97f, 0.50f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(boundaryPanel, UIFactory.Accent);
            Text safetyTitle = UIFactory.CreateText(boundaryPanel.transform, "RELEASE SAFETY", 19, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            SetHeight(safetyTitle.gameObject, 26);
            Text safetyCopy = UIFactory.CreateText(boundaryPanel.transform, "SIGNATURE PROVES WALLET CONTROL\nAPECHAIN ownerOf UNLOCKS BOSS ACCESS", 12, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            safetyCopy.lineSpacing = 1.08f;
            SetHeight(safetyCopy.gameObject, 60);

            GameObject footer = UIFactory.CreateHorizontalStack(shell.transform, "WalletFooter", Color.clear, 8, 0);
            UIFactory.SetAnchors(footer.GetComponent<RectTransform>(), new Vector2(0.03f, 0.035f), new Vector2(0.97f, 0.145f), Vector2.zero, Vector2.zero);
            UIFactory.CreateButton(footer.transform, "BOSS BATTLES", () => SceneManager.LoadScene("BossBattleScene"), UIFactory.Red);
            UIFactory.CreateButton(footer.transform, "SAVE API URL", SaveApiUrl, UIFactory.Blue);
            UIFactory.CreateButton(footer.transform, "MAIN MENU", () => SceneManager.LoadScene("MainMenuScene"), UIFactory.PanelAlt);
            messageText = UIFactory.CreateText(shell.transform, "Loading saved wallet status...", 17, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
            UIFactory.SetAnchors(messageText.rectTransform, new Vector2(0.03f, 0.15f), new Vector2(0.97f, 0.195f), Vector2.zero, Vector2.zero);
            messageText.resizeTextForBestFit = true;
            messageText.resizeTextMinSize = 10;
            messageText.resizeTextMaxSize = 14;
            messageText.verticalOverflow = VerticalWrapMode.Truncate;
            StartCoroutine(RefreshWalletRoutine());
        }

        private GameObject CreateAccountPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "AccountIdentity", UIFactory.PanelAlt, 2, 7);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Accent);
            Text title = UIFactory.CreateText(panel.transform, "SAVED GAME ACCOUNT", 18, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            SetHeight(title.gameObject, 24);
            string shortId = playerId.Length > 12 ? playerId.Substring(0, 12) + "..." : playerId;
            accountText = UIFactory.CreateText(
                panel.transform,
                $"{LocalSaveSystem.LoadPlayerName().ToUpperInvariant()}   •   ACCOUNT {shortId}",
                13,
                TextAnchor.MiddleCenter,
                UIFactory.Cream,
                FontStyle.Bold);
            accountText.resizeTextForBestFit = true;
            accountText.resizeTextMinSize = 10;
            accountText.resizeTextMaxSize = 13;
            SetHeight(accountText.gameObject, 30);
            return panel;
        }

        private GameObject CreateConnectionPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "WalletConnection", UIFactory.GlassPanel, 6, 10);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.NeonCyan);
            Text title = UIFactory.CreateText(panel.transform, "APECHAIN WALLET", 18, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);
            SetHeight(title.gameObject, 25);
            string savedWallet = LocalSaveSystem.LoadMockWalletAddress();
            walletInput = UIFactory.CreateInputField(panel.transform, "0x WALLET ADDRESS", savedWallet);
            SetHeight(walletInput.gameObject, 52);
            CompactInput(walletInput);
            GameObject actions = UIFactory.CreateHorizontalStack(panel.transform, "WalletActions", Color.clear, 8, 0);
            linkButton = UIFactory.CreateButton(actions.transform, "CONNECT", ConnectInjectedWallet, UIFactory.Green);
            syncButton = UIFactory.CreateButton(actions.transform, "SYNC", SyncOwnership, UIFactory.Blue);
            disconnectButton = UIFactory.CreateButton(actions.transform, "DISCONNECT", DisconnectWallet, UIFactory.PanelAlt);
            Button qaButton = UIFactory.CreateButton(actions.transform, "QA 1/1", LoadQaWallet, UIFactory.PortalViolet);
            CompactButton(linkButton);
            CompactButton(syncButton);
            CompactButton(disconnectButton);
            CompactButton(qaButton);
            connectionText = UIFactory.CreateText(panel.transform, "DISCONNECTED", 13, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
            connectionText.resizeTextForBestFit = true;
            connectionText.resizeTextMinSize = 10;
            connectionText.resizeTextMaxSize = 13;
            connectionText.verticalOverflow = VerticalWrapMode.Truncate;
            SetHeight(connectionText.gameObject, 34);
            return panel;
        }

        private GameObject CreateEligibilityPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "BossEligibility", UIFactory.Panel, 6, 12);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Red);
            Text title = UIFactory.CreateText(panel.transform, "BOSS BATTLE ROLE", 20, TextAnchor.MiddleCenter, UIFactory.Red, FontStyle.Bold);
            SetHeight(title.gameObject, 28);
            eligibilityText = UIFactory.CreateText(
                panel.transform,
                "MEMBER\nVERIFIED 1-OF-1 OWNERSHIP REQUIRED",
                13,
                TextAnchor.MiddleCenter,
                UIFactory.Cream,
                FontStyle.Bold);
            eligibilityText.lineSpacing = 1.12f;
            SetHeight(eligibilityText.gameObject, 48);
            bossModeShimmer = eligibilityText.gameObject.AddComponent<PremiumTextShimmer>();
            bossModeShimmer.Configure(eligibilityText);
            bossModeShimmer.enabled = false;
            return panel;
        }

        private GameObject CreateOwnershipPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "OwnershipPreview", UIFactory.Panel, 6, 12);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Green);
            Text title = UIFactory.CreateText(panel.transform, "OWNERSHIP PREVIEW", 20, TextAnchor.MiddleCenter, UIFactory.Green, FontStyle.Bold);
            SetHeight(title.gameObject, 28);
            GameObject assetRow = UIFactory.CreateHorizontalStack(panel.transform, "OwnedAssets", Color.clear, 7, 0);
            SetHeight(assetRow, 72);
            GameObject preview = UIFactory.CreatePanel(assetRow.transform, "AssetPreview", UIFactory.PanelAlt);
            assetPreviewImage = preview.GetComponent<Image>();
            assetPreviewImage.preserveAspect = true;
            LayoutElement previewLayout = preview.AddComponent<LayoutElement>();
            previewLayout.minWidth = 62;
            previewLayout.preferredWidth = 68;
            previewLayout.flexibleWidth = 0;
            ownershipApprovalMark = UIFactory.CreateText(preview.transform, "☻", 52, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.Stretch(ownershipApprovalMark.rectTransform);
            ownershipApprovalMark.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            ownershipApprovalMark.raycastTarget = false;
            Outline markOutline = ownershipApprovalMark.gameObject.AddComponent<Outline>();
            markOutline.effectColor = UIFactory.Ink;
            markOutline.effectDistance = new Vector2(2f, -2f);
            ownershipApprovalMark.gameObject.SetActive(false);
            ownershipText = UIFactory.CreateText(
                assetRow.transform,
                "COSMETIC OWNERSHIP PREVIEW\nNEVER GRANTS BOSS ELIGIBILITY",
                13,
                TextAnchor.MiddleCenter,
                UIFactory.MutedTextColor);
            ownershipText.lineSpacing = 1.10f;
            LayoutElement ownershipLayout = ownershipText.gameObject.AddComponent<LayoutElement>();
            ownershipLayout.flexibleWidth = 1;
            GameObject bossChoice = UIFactory.CreateHorizontalStack(panel.transform, "BossAssetChoice", Color.clear, 6, 0);
            SetHeight(bossChoice, 34);
            Button previous = UIFactory.CreateButton(bossChoice.transform, "< 1/1", SelectPreviousBossAsset, UIFactory.PortalViolet);
            Button choose = UIFactory.CreateButton(bossChoice.transform, "USE AS BOSS", SaveSelectedBossAsset, UIFactory.Green);
            Button next = UIFactory.CreateButton(bossChoice.transform, "1/1 >", SelectNextBossAsset, UIFactory.PortalViolet);
            CompactButton(previous);
            CompactButton(choose);
            CompactButton(next);
            return panel;
        }

        private GameObject CreateApiPanel(Transform parent)
        {
            GameObject panel = UIFactory.CreateVerticalStack(parent, "BackendConnection", UIFactory.PanelAlt, 5, 12);
            Text title = UIFactory.CreateText(panel.transform, "BACKEND API", 19, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            SetHeight(title.gameObject, 28);
            apiInput = UIFactory.CreateInputField(panel.transform, AppConfig.DefaultApiBaseUrl, AppConfig.ApiBaseUrl);
            SetHeight(apiInput.gameObject, 54);
            CompactInput(apiInput);
            adminControls = UIFactory.CreateHorizontalStack(panel.transform, "AdminControls", Color.clear, 6, 0);
            SetHeight(adminControls, 42);
            adminWalletInput = UIFactory.CreateInputField(adminControls.transform, "ADMIN WALLET", string.Empty);
            CompactInput(adminWalletInput);
            Button addAdmin = UIFactory.CreateButton(adminControls.transform, "ADD ADMIN", AddAdmin, UIFactory.Red);
            CompactButton(addAdmin);
            adminControls.SetActive(false);
            return panel;
        }

        private IEnumerator RefreshWalletRoutine()
        {
            if (requestActive) yield break;
            requestActive = true;
            WalletAccountResponse response = null;
            string requestError = null;
            yield return apiClient.GetWalletAccount(playerId, value => response = value, error => requestError = error);
            requestActive = false;
            if (response?.wallet != null)
            {
                walletStatus = response.wallet;
                ApplyWalletStatus();
                SetMessage(response.verificationBoundary, UIFactory.MutedTextColor);
            }
            else
            {
                ApplyOfflineStatus();
                SetMessage($"Wallet status could not sync. {ReadableError(requestError)}", UIFactory.Red);
            }
        }

        private void LinkWallet()
        {
            if (requestActive) return;
            string address = walletInput == null ? string.Empty : walletInput.text.Trim();
            StartCoroutine(LinkWalletRoutine(address));
        }

        private void ConnectInjectedWallet()
        {
            if (requestActive) return;
            SetRequestState(true, "Opening the wallet and switching to ApeChain...");
#if UNITY_WEBGL && !UNITY_EDITOR
            AppreciatorsRequestWalletConnection(gameObject.name);
#else
            SetRequestState(false, "Injected wallet connection is available in the WebGL build. Use QA 1/1 for local Editor testing.", UIFactory.Accent);
#endif
        }

        public void OnInjectedWalletConnected(string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                OnInjectedWalletError("The wallet returned an empty account.");
                return;
            }
            pendingWalletAddress = walletAddress.Trim();
            if (walletInput != null) walletInput.text = pendingWalletAddress;
            StartCoroutine(CreateChallengeRoutine());
        }

        public void OnInjectedWalletSignature(string signature)
        {
            if (activeChallenge == null || string.IsNullOrWhiteSpace(signature))
            {
                OnInjectedWalletError("The wallet did not return a usable signature.");
                return;
            }
            StartCoroutine(VerifySignatureRoutine(signature.Trim()));
        }

        public void OnInjectedWalletError(string error)
        {
            activeChallenge = null;
            pendingWalletAddress = string.Empty;
            SetRequestState(false, string.IsNullOrWhiteSpace(error) ? "Wallet connection was cancelled." : error, UIFactory.Red);
        }

        private IEnumerator CreateChallengeRoutine()
        {
            WalletChallengeResponse response = null;
            string requestError = null;
            SetMessage("Creating a one-time wallet signature challenge...", UIFactory.Accent);
            yield return apiClient.CreateWalletChallenge(playerId, pendingWalletAddress, value => response = value, error => requestError = error);
            if (response?.success != true)
            {
                SetRequestState(false, ReadableError(requestError), UIFactory.Red);
                yield break;
            }
            activeChallenge = response;
            SetMessage("Approve the message signature. This does not spend assets or authorize a transaction.", UIFactory.Accent);
#if UNITY_WEBGL && !UNITY_EDITOR
            AppreciatorsSignWalletMessage(gameObject.name, pendingWalletAddress, activeChallenge.message);
#else
            SetRequestState(false, "Message signing requires the WebGL wallet bridge.", UIFactory.Red);
#endif
        }

        private IEnumerator VerifySignatureRoutine(string signature)
        {
            WalletAccountResponse response = null;
            string requestError = null;
            SetMessage("Verifying signature and reading Appreciators Originals on ApeChain...", UIFactory.Accent);
            yield return apiClient.VerifyWalletChallenge(
                playerId,
                pendingWalletAddress,
                activeChallenge.challengeId,
                signature,
                value => response = value,
                error => requestError = error);
            activeChallenge = null;
            if (response?.wallet == null)
            {
                SetRequestState(false, ReadableError(requestError), UIFactory.Red);
                yield break;
            }
            walletStatus = response.wallet;
            LocalSaveSystem.SaveMockWallet(walletStatus.walletAddress, walletStatus.signatureVerified);
            walletInput.text = walletStatus.walletAddress;
            ApplyWalletStatus();
            SetRequestState(false, response.message, walletStatus.oneOfOneEligible ? UIFactory.Green : UIFactory.Accent);
        }

        private IEnumerator LinkWalletRoutine(string address)
        {
            requestActive = true;
            SetMessage("Linking wallet preview to the saved game account...", UIFactory.Accent);
            WalletAccountResponse response = null;
            string requestError = null;
            yield return apiClient.LinkWalletAccount(playerId, address, value => response = value, error => requestError = error);
            requestActive = false;
            if (response?.wallet != null)
            {
                walletStatus = response.wallet;
                LocalSaveSystem.SaveMockWallet(walletStatus.walletAddress, true);
                walletInput.text = walletStatus.walletAddress;
                ApplyWalletStatus();
                SetMessage(response.message, walletStatus.oneOfOneEligible ? UIFactory.Green : UIFactory.Accent);
            }
            else SetMessage(ReadableError(requestError), UIFactory.Red);
        }

        private void SyncOwnership()
        {
            if (requestActive || walletStatus == null || string.IsNullOrWhiteSpace(walletStatus.walletAddress)) return;
            StartCoroutine(RefreshWalletRoutine());
        }

        private IEnumerator SyncOwnershipRoutine()
        {
            requestActive = true;
            SetMessage("Loading the cosmetic ownership preview...", UIFactory.Accent);
            NftSyncResponse response = null;
            string requestError = null;
            yield return apiClient.SyncMockNftOwnership(walletStatus.walletAddress, value => response = value, error => requestError = error);
            requestActive = false;
            if (response?.synced == true)
            {
                ownershipText.text =
                    $"ORIGINALS  {AssetList(response.originals)}\n" +
                    $"COMPANIONS  {AssetList(response.companions)}\n" +
                    $"COSMETICS  {ListText(response.cosmetics)}\n" +
                    "PREVIEW ONLY • NO GAMEPLAY POWER";
                ownershipText.color = UIFactory.Cream;
                SetMessage(response.message, UIFactory.Green);
            }
            else SetMessage(ReadableError(requestError), UIFactory.Red);
        }

        private void DisconnectWallet()
        {
            if (requestActive) return;
            StartCoroutine(DisconnectRoutine());
        }

        private IEnumerator DisconnectRoutine()
        {
            requestActive = true;
            WalletAccountResponse response = null;
            string requestError = null;
            yield return apiClient.DisconnectWalletAccount(playerId, value => response = value, error => requestError = error);
            requestActive = false;
            if (response?.wallet != null)
            {
                walletStatus = response.wallet;
                LocalSaveSystem.ClearMockWallet();
                walletInput.text = string.Empty;
                ownershipText.text = "Wallet disconnected. Ownership preview cleared.";
                ApplyWalletStatus();
                SetMessage("Wallet preview disconnected from this saved account.", UIFactory.Green);
            }
            else SetMessage(ReadableError(requestError), UIFactory.Red);
        }

        private void LoadQaWallet()
        {
            walletInput.text = LocalOneOfOneQaWallet;
            SetMessage("Loading the local 1-of-1 QA holder. Production Boss Mode still requires a real signed wallet.", UIFactory.PortalViolet);
            LinkWallet();
        }

        private void SaveApiUrl()
        {
            LocalSaveSystem.SaveApiBaseUrl(apiInput.text);
            SetMessage("Backend API URL saved. Reload this screen to reconnect with the new endpoint.", UIFactory.Green);
        }

        private void ApplyWalletStatus()
        {
            if (walletStatus == null) return;
            bool connected = walletStatus.connectionState != "disconnected" && !string.IsNullOrWhiteSpace(walletStatus.walletAddress);
            connectionText.text = connected
                ? walletStatus.oneOfOneEligible
                    ? "LINKED  •  APECHAIN  •  1-OF-1 VERIFIED"
                    : "LINKED  •  APECHAIN  •  MEMBER"
                : "DISCONNECTED  •  WALLET OPTIONAL";
            connectionText.color = connected ? UIFactory.NeonCyan : UIFactory.MutedTextColor;
            eligibilityText.text = walletStatus.oneOfOneEligible && walletStatus.ownershipVerified
                ? "BOSS MODE OPEN\n1-OF-1 VERIFIED"
                : "MEMBER\nVERIFIED 1-OF-1 OWNERSHIP REQUIRED";
            eligibilityText.color = walletStatus.oneOfOneEligible ? UIFactory.Green : UIFactory.Cream;
            if (bossModeShimmer != null) bossModeShimmer.enabled = walletStatus.oneOfOneEligible && walletStatus.ownershipVerified;
            WalletOwnedAsset[] assets = walletStatus.assets ?? Array.Empty<WalletOwnedAsset>();
            if (assets.Length == 0) selectedBossAssetIndex = 0;
            else
            {
                string savedTokenId = LocalSaveSystem.LoadSelectedBossTokenId();
                int savedIndex = Array.FindIndex(assets, asset => asset != null && asset.tokenId.ToString() == savedTokenId);
                if (savedIndex >= 0) selectedBossAssetIndex = savedIndex;
                selectedBossAssetIndex = Mathf.Clamp(selectedBossAssetIndex, 0, assets.Length - 1);
            }
            string assetList = assets.Length == 0
                ? "NO 1-OF-1 ASSETS IN THIS WALLET"
                : string.Join("  •  ", Array.ConvertAll(assets, asset => $"{asset.name} #{asset.tokenId}"));
            string selectedLabel = assets.Length > 0 ? $"\nBOSS SELECTED  {assets[selectedBossAssetIndex].name} #{assets[selectedBossAssetIndex].tokenId}" : string.Empty;
            ownershipText.text = $"ORIGINALS  {walletStatus.originalsBalance}\n{assetList}{selectedLabel}";
            ownershipText.color = assets.Length > 0 ? UIFactory.Green : UIFactory.Cream;
            if (assetPreviewImage != null)
            {
                assetPreviewImage.sprite = null;
                assetPreviewImage.color = assets.Length > 0 ? Color.white : new Color(UIFactory.PortalViolet.r, UIFactory.PortalViolet.g, UIFactory.PortalViolet.b, 0.24f);
                if (walletStatus.ownershipVerified) ApplyVerifiedOwnerPreview();
                else if (assets.Length > 0 && !string.IsNullOrWhiteSpace(assets[selectedBossAssetIndex].image)) StartCoroutine(LoadAssetPreviewRoutine(assets[selectedBossAssetIndex].image));
            }
            if (ownershipApprovalMark != null)
            {
                ownershipApprovalMark.gameObject.SetActive(walletStatus.ownershipVerified);
            }
            linkButton.interactable = !requestActive;
            syncButton.interactable = !requestActive && connected;
            disconnectButton.interactable = !requestActive && connected;
            if (adminControls != null) adminControls.SetActive(walletStatus.isAdmin && walletStatus.signatureVerified);
        }

        private void AddAdmin()
        {
            if (requestActive || adminWalletInput == null) return;
            StartCoroutine(AddAdminRoutine(adminWalletInput.text.Trim()));
        }

        private IEnumerator AddAdminRoutine(string walletAddress)
        {
            requestActive = true;
            AdminGrantResponse response = null;
            string requestError = null;
            yield return apiClient.GrantAdminAccess(playerId, walletAddress, value => response = value, error => requestError = error);
            requestActive = false;
            if (response?.success == true)
            {
                adminWalletInput.text = string.Empty;
                SetMessage(response.message, UIFactory.Green);
            }
            else SetMessage(ReadableError(requestError), UIFactory.Red);
            ApplyWalletStatus();
        }

        private void SelectPreviousBossAsset()
        {
            SelectBossAsset(-1);
        }

        private void SelectNextBossAsset()
        {
            SelectBossAsset(1);
        }

        private void SelectBossAsset(int direction)
        {
            WalletOwnedAsset[] assets = walletStatus?.assets ?? Array.Empty<WalletOwnedAsset>();
            if (assets.Length == 0) return;
            selectedBossAssetIndex = (selectedBossAssetIndex + direction + assets.Length) % assets.Length;
            LocalSaveSystem.SaveSelectedBossTokenId(assets[selectedBossAssetIndex].tokenId.ToString());
            ApplyWalletStatus();
            SetMessage($"Selected {assets[selectedBossAssetIndex].name} as the Boss Battle 1-of-1.", UIFactory.Green);
        }

        private void SaveSelectedBossAsset()
        {
            WalletOwnedAsset[] assets = walletStatus?.assets ?? Array.Empty<WalletOwnedAsset>();
            if (assets.Length == 0)
            {
                SetMessage("No server-verified 1-of-1 asset is available to select.", UIFactory.Red);
                return;
            }
            LocalSaveSystem.SaveSelectedBossTokenId(assets[selectedBossAssetIndex].tokenId.ToString());
            SetMessage($"{assets[selectedBossAssetIndex].name} will enter Boss Battle as your selected 1-of-1.", UIFactory.Green);
        }

        private void ApplyVerifiedOwnerPreview()
        {
            if (assetPreviewImage == null) return;
            if (verifiedOwnerPreviewSprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>("Wallet/VerifiedOwnerCardReverse");
                if (texture != null)
                {
                    verifiedOwnerPreviewSprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }
            if (verifiedOwnerPreviewSprite == null) return;
            assetPreviewImage.sprite = verifiedOwnerPreviewSprite;
            assetPreviewImage.preserveAspect = true;
            assetPreviewImage.color = Color.white;
        }

        private void ApplyOfflineStatus()
        {
            string saved = LocalSaveSystem.LoadMockWalletAddress();
            connectionText.text = string.IsNullOrWhiteSpace(saved) ? "DISCONNECTED" : $"LOCAL SAVE  {saved}\nSERVER STATUS UNAVAILABLE";
            eligibilityText.text = "MEMBER\nBoss eligibility cannot be granted while the server is unavailable.";
            syncButton.interactable = false;
            disconnectButton.interactable = !string.IsNullOrWhiteSpace(saved);
        }

        private void SetMessage(string message, Color color)
        {
            if (messageText == null) return;
            messageText.text = message;
            messageText.color = color;
        }

        private void SetRequestState(bool active, string message, Color? color = null)
        {
            requestActive = active;
            if (linkButton != null) linkButton.interactable = !active;
            if (syncButton != null) syncButton.interactable = !active && walletStatus != null && !string.IsNullOrWhiteSpace(walletStatus.walletAddress);
            if (disconnectButton != null) disconnectButton.interactable = !active && walletStatus != null && !string.IsNullOrWhiteSpace(walletStatus.walletAddress);
            SetMessage(message, color ?? UIFactory.Accent);
        }

        private IEnumerator LoadAssetPreviewRoutine(string imageUrl)
        {
            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success || assetPreviewImage == null) yield break;
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            if (texture == null) yield break;
            assetPreviewImage.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            assetPreviewImage.color = Color.white;
        }

        private static string AssetList(MockOwnedAsset[] assets)
        {
            if (assets == null || assets.Length == 0) return "None";
            string[] labels = new string[assets.Length];
            for (int i = 0; i < assets.Length; i++) labels[i] = assets[i].name;
            return string.Join(", ", labels);
        }

        private static string ListText(string[] values) => values == null || values.Length == 0 ? "None" : string.Join(", ", values);

        private static string ReadableError(string error)
        {
            if (string.IsNullOrWhiteSpace(error)) return "The wallet service did not respond.";
            int marker = error.IndexOf("\"message\":\"", StringComparison.Ordinal);
            if (marker >= 0)
            {
                int start = marker + 11;
                int end = error.IndexOf('"', start);
                if (end > start) return error.Substring(start, end - start);
            }
            return error.Length > 180 ? error.Substring(0, 180) + "..." : error;
        }

        private static void SetHeight(GameObject target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        private static void CompactButton(Button button)
        {
            if (button == null) return;
            SetHeight(button.gameObject, 48);
            Text label = button.GetComponentInChildren<Text>();
            if (label == null) return;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = 16;
        }

        private static void CompactInput(InputField input)
        {
            if (input == null) return;
            if (input.textComponent != null)
            {
                input.textComponent.fontSize = 16;
                input.textComponent.resizeTextForBestFit = true;
                input.textComponent.resizeTextMinSize = 10;
                input.textComponent.resizeTextMaxSize = 16;
                input.textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            }
            Text placeholder = input.placeholder as Text;
            if (placeholder == null) return;
            placeholder.fontSize = 16;
            placeholder.resizeTextForBestFit = true;
            placeholder.resizeTextMinSize = 10;
            placeholder.resizeTextMaxSize = 16;
            placeholder.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }
}
