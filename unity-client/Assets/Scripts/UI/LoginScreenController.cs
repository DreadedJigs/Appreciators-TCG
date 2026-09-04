using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.Packs;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class LoginScreenController : ScreenControllerBase
    {
        private InputField nameInput;
        private InputField passwordInput;
        private RawImage mobileQrImage;
        private Text mobileQrStatus;
        private Text mobileQrUrl;
        private Texture2D mobileQrTexture;
        private string lastMobileUrl;
        private Button loginButton;
        private Text loginStatus;
        private bool loginInProgress;
        private RectTransform headerRect;
        private RectTransform playerEntryRect;
        private RectTransform mobileEntryRect;
        private RectTransform themeButtonRect;
        private Vector2Int lastResponsiveSize;
        private Text marqueeText;
        private Text loginIntroText;
        private Text loginFlowText;

        private void Start()
        {
            RectTransform playmat = UIFactory.CreateBrandMenuRoot(Root);
            GameObject header = UIFactory.CreatePanel(playmat, "LoginHeader", UIFactory.GlassPanel);
            headerRect = header.GetComponent<RectTransform>();
            UIFactory.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0.18f, 0.79f), new Vector2(0.82f, 0.955f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(header, UIFactory.NeonCyan);
            UIFactory.ApplyBrandStarfield(header);
            Text marquee = UIFactory.CreateText(header.transform, "A P P R E C I A T O R S   T C G", 43, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            marqueeText = marquee;
            UIFactory.SetAnchors(marquee.rectTransform, new Vector2(0.06f, 0.48f), new Vector2(0.94f, 0.90f), Vector2.zero, Vector2.zero);
            marquee.resizeTextForBestFit = true;
            marquee.resizeTextMinSize = 28;
            marquee.resizeTextMaxSize = 43;
            Text original = UIFactory.CreateText(header.transform, "B E   O R I G I N A L", 20, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.SetAnchors(original.rectTransform, new Vector2(0.20f, 0.10f), new Vector2(0.80f, 0.40f), Vector2.zero, Vector2.zero);

            GameObject panel = UIFactory.CreateVerticalStack(playmat, "PlayerEntry", UIFactory.GlassPanel, 11, 18);
            playerEntryRect = panel.GetComponent<RectTransform>();
            UIFactory.SetAnchors(panel.GetComponent<RectTransform>(), new Vector2(0.07f, 0.25f), new Vector2(0.49f, 0.72f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(panel, UIFactory.Green);
            UIFactory.MakePanelTransparent(panel);
            UIFactory.CreateText(panel.transform, "ENTER THE PLAYMAT", 27, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            loginIntroText = UIFactory.CreateText(panel.transform, "Create a secure account for online matches, cloud saves, and verified wallet access.", 18, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);
            loginFlowText = UIFactory.CreateText(panel.transform, "DRAW TWO  •  COMMIT ONE  •  END TURN  •  DISCARD  •  COMBAT", 16, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);

            nameInput = UIFactory.CreateInputField(panel.transform, "Player name", LocalSaveSystem.LoadPlayerName());
            passwordInput = UIFactory.CreateInputField(panel.transform, "Password (12+ characters)", string.Empty);
            passwordInput.contentType = InputField.ContentType.Password;
            GameObject secureAccountActions = UIFactory.CreateHorizontalStack(panel.transform, "SecureAccountActions", Color.clear, 8, 0);
            UIFactory.CreateButton(secureAccountActions.transform, "CREATE ACCOUNT", CreateSecureAccount, UIFactory.Green);
            UIFactory.CreateButton(secureAccountActions.transform, "SIGN IN", SignInSecureAccount, UIFactory.Blue);
            loginButton = UIFactory.CreateButton(panel.transform, "PLAY AS GUEST", Login, UIFactory.PortalViolet);
            loginStatus = UIFactory.CreateText(panel.transform, "Secure accounts sync online progress. Guest play stays on this device.", 16, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
            UIFactory.CreateButton(panel.transform, "CONNECT WALLET / WEB3", () => SceneManager.LoadScene("Web3MockScene"), UIFactory.Blue);

            GameObject accessPanel = UIFactory.CreateVerticalStack(playmat, "MobileEntry", UIFactory.GlassPanel, 6, 14);
            mobileEntryRect = accessPanel.GetComponent<RectTransform>();
            UIFactory.SetAnchors(accessPanel.GetComponent<RectTransform>(), new Vector2(0.51f, 0.25f), new Vector2(0.93f, 0.72f), Vector2.zero, Vector2.zero);
            UIFactory.MakeDimensionalPanel(accessPanel, UIFactory.Accent);
            UIFactory.MakePanelTransparent(accessPanel);
            UIFactory.CreateText(accessPanel.transform, "MOBILE PLAYMAT", 24, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            CreateMobileAccessQr(accessPanel.transform);

            string themeLabel = ThemeService.IsDark ? "LIGHT MODE" : "DARK MODE";
            Button themeButton = UIFactory.CreateButton(playmat, themeLabel, ToggleTheme, UIFactory.PortalViolet);
            themeButtonRect = themeButton.GetComponent<RectTransform>();
            UIFactory.SetAnchors(themeButton.GetComponent<RectTransform>(), new Vector2(0.80f, 0.925f), new Vector2(0.975f, 0.985f), Vector2.zero, Vector2.zero);

            string immediateUrl = TryGetWebGlMobileAccessUrl();
            if (!string.IsNullOrWhiteSpace(immediateUrl))
            {
                SetMobileQrUrl(immediateUrl, "Scan For Mobile Access");
            }
            else
            {
                StartCoroutine(LoadMobileAccessUrl());
            }
            ApplyResponsiveLoginLayout(true);
        }

        private void LateUpdate()
        {
            ApplyResponsiveLoginLayout(false);
        }

        private void ApplyResponsiveLoginLayout(bool force)
        {
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (!force && size == lastResponsiveSize) return;
            lastResponsiveSize = size;
            bool phone = ResponsiveCanvasScaler.IsPhoneLayout;
            SetRect(headerRect, phone ? new Rect(0.12f, 0.785f, 0.68f, 0.19f) : new Rect(0.18f, 0.79f, 0.64f, 0.165f));
            SetRect(playerEntryRect, phone ? new Rect(0.035f, 0.055f, 0.455f, 0.70f) : new Rect(0.07f, 0.25f, 0.42f, 0.47f));
            SetRect(mobileEntryRect, phone ? new Rect(0.51f, 0.055f, 0.455f, 0.70f) : new Rect(0.51f, 0.25f, 0.42f, 0.47f));
            SetRect(themeButtonRect, phone ? new Rect(0.82f, 0.865f, 0.17f, 0.105f) : new Rect(0.80f, 0.925f, 0.175f, 0.06f));
            if (marqueeText != null)
            {
                marqueeText.fontSize = phone ? 32 : 43;
                marqueeText.resizeTextMinSize = phone ? 18 : 28;
                marqueeText.resizeTextMaxSize = phone ? 32 : 43;
            }
            if (playerEntryRect != null)
            {
                VerticalLayoutGroup group = playerEntryRect.GetComponent<VerticalLayoutGroup>();
                if (group != null)
                {
                    group.spacing = phone ? 6f : 11f;
                    group.padding = phone ? new RectOffset(10, 10, 10, 10) : new RectOffset(18, 18, 18, 18);
                }
            }
            ConfigurePhoneCopy(loginIntroText, phone,
                "Create an account for online play and cloud saves.",
                "Create a secure account for online matches, cloud saves, and verified wallet access.",
                phone ? 16 : 18,
                phone ? 42f : -1f);
            ConfigurePhoneCopy(loginFlowText, phone,
                "DRAW 2  •  PLAY 1  •  DISCARD  •  COMBAT",
                "DRAW TWO  •  COMMIT ONE  •  END TURN  •  DISCARD  •  COMBAT",
                16,
                phone ? 28f : -1f);
            ConfigurePhoneCopy(loginStatus, phone,
                "Sign in for online sync. Guests remain local.",
                "Secure accounts sync online progress. Guest play stays on this device.",
                16,
                phone ? 30f : -1f);
            if (phone && mobileEntryRect != null)
            {
                Transform qrRow = mobileEntryRect.Find("MobileAccessQr");
                Transform qrFrame = qrRow == null ? null : qrRow.Find("MobileAccessQrFrame");
                Transform copy = qrRow == null ? null : qrRow.Find("MobileAccessCopy");
                SetLayoutSize(qrFrame, 140f, 150f);
                SetLayoutSize(copy, 180f, 220f);
            }
        }

        private static void SetLayoutSize(Transform target, float minWidth, float preferredWidth)
        {
            LayoutElement layout = target == null ? null : target.GetComponent<LayoutElement>();
            if (layout == null) return;
            layout.minWidth = minWidth;
            layout.preferredWidth = preferredWidth;
        }

        private static void ConfigurePhoneCopy(Text text, bool phone, string phoneCopy, string desktopCopy, int maxSize, float preferredHeight)
        {
            if (text == null) return;
            text.text = phone ? phoneCopy : desktopCopy;
            text.fontSize = maxSize;
            text.resizeTextForBestFit = phone;
            text.resizeTextMinSize = phone ? 12 : Mathf.Max(12, maxSize - 4);
            text.resizeTextMaxSize = maxSize;
            text.verticalOverflow = phone ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            LayoutElement layout = text.GetComponent<LayoutElement>();
            if (phone && preferredHeight > 0f)
            {
                layout = layout ?? text.gameObject.AddComponent<LayoutElement>();
                layout.minHeight = preferredHeight;
                layout.preferredHeight = preferredHeight;
                layout.flexibleHeight = 0f;
            }
            else if (layout != null)
            {
                layout.minHeight = -1f;
                layout.preferredHeight = -1f;
                layout.flexibleHeight = 1f;
            }
        }

        private static void SetRect(RectTransform rect, Rect normalized)
        {
            if (rect == null) return;
            UIFactory.SetAnchors(rect, new Vector2(normalized.xMin, normalized.yMin), new Vector2(normalized.xMax, normalized.yMax), Vector2.zero, Vector2.zero);
        }

        private static void ToggleTheme()
        {
            ThemeService.Toggle();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (mobileQrTexture != null)
            {
                Destroy(mobileQrTexture);
                mobileQrTexture = null;
            }
        }

        private void Login()
        {
            if (!loginInProgress)
            {
                StartCoroutine(LoginRoutine());
            }
        }

        private void CreateSecureAccount()
        {
            if (!loginInProgress) StartCoroutine(SecureAccountRoutine(true));
        }

        private void SignInSecureAccount()
        {
            if (!loginInProgress) StartCoroutine(SecureAccountRoutine(false));
        }

        private IEnumerator SecureAccountRoutine(bool create)
        {
            string playerName = string.IsNullOrWhiteSpace(nameInput.text) ? string.Empty : nameInput.text.Trim();
            string password = passwordInput == null ? string.Empty : passwordInput.text;
            if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(password))
            {
                loginStatus.text = "ENTER AN ACCOUNT NAME AND PASSWORD.";
                loginStatus.color = UIFactory.Red;
                yield break;
            }

            loginInProgress = true;
            loginButton.interactable = false;
            loginStatus.text = create ? "CREATING SECURE ACCOUNT..." : "SIGNING IN SECURELY...";
            loginStatus.color = UIFactory.Accent;
            BackendApiClient apiClient = gameObject.AddComponent<BackendApiClient>();
            SecureAccountSessionResponse response = null;
            string requestError = null;
            if (create)
            {
                yield return apiClient.RegisterSecureAccount(playerName, password, value => response = value, error => requestError = error);
            }
            else
            {
                yield return apiClient.LoginSecureAccount(playerName, password, value => response = value, error => requestError = error);
            }

            if (response?.success != true || response.account == null || string.IsNullOrWhiteSpace(response.account.id))
            {
                loginStatus.text = $"SECURE ACCOUNT FAILED — {ReadableAccountError(requestError)}";
                loginStatus.color = UIFactory.Red;
                loginButton.interactable = true;
                loginInProgress = false;
                yield break;
            }

            LocalSaveSystem.SavePlayerName(response.account.username);
            LocalSaveSystem.SavePlayerId(response.account.id);
            passwordInput.text = string.Empty;
            loginStatus.text = "SECURE SESSION READY — RESTORING CLOUD PLAY DATA...";
            loginStatus.color = UIFactory.Green;
            CloudSaveResponse cloudSave = null;
            string cloudError = null;
            yield return apiClient.GetCloudSave(value => cloudSave = value, error => cloudError = error);
            if (cloudSave?.success == true)
            {
                if (cloudSave.version > 0)
                {
                    LocalSaveSystem.ApplyCloudSave(cloudSave);
                }
                else
                {
                    CloudSaveResponse uploadedSave = null;
                    CloudSaveRequest initialSave = new CloudSaveRequest
                    {
                        expectedVersion = 0,
                        snapshot = LocalSaveSystem.CaptureCloudSave()
                    };
                    yield return apiClient.SaveCloudSave(initialSave, value => uploadedSave = value, error => cloudError = error);
                    if (uploadedSave?.success == true)
                    {
                        LocalSaveSystem.SaveCloudSaveVersion(uploadedSave.version);
                    }
                }
                CloudSaveSyncService.MarkSynchronized();
            }
            else if (!string.IsNullOrWhiteSpace(cloudError))
            {
                Debug.LogWarning($"[CloudSave] Initial sync deferred: {cloudError}");
            }
            yield return new WaitForSecondsRealtime(0.35f);
            SceneManager.LoadScene("MainMenuScene");
        }

        private static string ReadableAccountError(string error)
        {
            if (string.IsNullOrWhiteSpace(error)) return "PLEASE TRY AGAIN";
            return error.Length > 120 ? error.Substring(0, 120) : error;
        }

        private IEnumerator LoginRoutine()
        {
            loginInProgress = true;
            loginButton.interactable = false;
            string playerName = string.IsNullOrWhiteSpace(nameInput.text) ? "Guest" : nameInput.text.Trim();
            string stablePlayerId = LocalSaveSystem.SaveAccountIdentity(playerName);
            loginStatus.text = "RESTORING PACKS AND APPRECIATION SHARDS...";
            loginStatus.color = UIFactory.Accent;

            BackendApiClient apiClient = gameObject.AddComponent<BackendApiClient>();
            AccountLoginResponse loginResponse = null;
            string loginError = null;
            yield return apiClient.LoginAccount(playerName, stablePlayerId, value => loginResponse = value, error => loginError = error);

            PackServerInventory inventory = loginResponse?.inventory;
            if (loginResponse?.success == true && loginResponse.profile != null && !string.IsNullOrWhiteSpace(loginResponse.profile.id))
            {
                LocalSaveSystem.SavePlayerId(loginResponse.profile.id);
            }
            else
            {
                // Compatibility path for an already-running alpha backend while the
                // new session endpoint is rolling out. Inventory is still keyed by
                // the deterministic account id, so it restores across networks.
                PackInventoryResponse inventoryResponse = null;
                yield return apiClient.GetPackInventory(stablePlayerId, value => inventoryResponse = value, error => loginError = error);
                inventory = inventoryResponse?.inventory;
            }

            if (inventory != null)
            {
                new PackInventoryService(new PackSaveService()).ReplaceWithAuthoritativeSnapshot(inventory);
                LocalSaveSystem.ApplyAccountProgress(inventory.progress);
                int unopenedPacks = inventory.packs == null ? 0 : System.Linq.Enumerable.Sum(inventory.packs, entry => entry == null ? 0 : Math.Max(0, entry.count));
                loginStatus.text = $"RESTORED  {unopenedPacks} PACKS  •  {inventory.appreciationShards:N0} APPRECIATION SHARDS";
                PlayerMatchStats stats = inventory.progress == null ? null : inventory.progress.stats;
                if (stats != null)
                {
                    loginStatus.text = $"RESTORED  {unopenedPacks} PACKS  |  {inventory.appreciationShards:N0} SHARDS  |  {stats.wins}W-{stats.losses}L";
                }
                loginStatus.color = UIFactory.Green;
            }
            else
            {
                loginStatus.text = "OFFLINE SAVE LOADED — ONLINE SYNC WILL RETRY FROM THE MENU";
                loginStatus.color = UIFactory.Accent;
                Debug.LogWarning($"[Account] Online inventory restore deferred: {loginError}");
            }

            yield return new WaitForSecondsRealtime(0.45f);
            SceneManager.LoadScene("MainMenuScene");
        }

        private void CreateMobileAccessQr(Transform parent)
        {
            GameObject row = UIFactory.CreateHorizontalStack(parent, "MobileAccessQr", new Color(0.03f, 0.12f, 0.34f, 0.92f), 12, 12);
            UIFactory.MakePanelTransparent(row, false);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            if (group != null)
            {
                group.childForceExpandWidth = false;
                group.childForceExpandHeight = true;
                group.childAlignment = TextAnchor.MiddleCenter;
            }

            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 178;
            rowLayout.preferredHeight = 196;
            rowLayout.flexibleHeight = 0;

            GameObject qrFrame = UIFactory.CreatePanel(row.transform, "MobileAccessQrFrame", UIFactory.Cream);
            LayoutElement qrFrameLayout = qrFrame.AddComponent<LayoutElement>();
            qrFrameLayout.minWidth = 156;
            qrFrameLayout.preferredWidth = 174;
            qrFrameLayout.flexibleWidth = 0;
            qrFrameLayout.minHeight = 156;
            qrFrameLayout.preferredHeight = 174;
            qrFrameLayout.flexibleHeight = 0;

            GameObject qrObject = new GameObject("MobileAccessQrImage", typeof(RectTransform), typeof(RawImage));
            qrObject.transform.SetParent(qrFrame.transform, false);
            mobileQrImage = qrObject.GetComponent<RawImage>();
            mobileQrImage.color = new Color(1f, 1f, 1f, 0.16f);
            RectTransform qrRect = qrObject.GetComponent<RectTransform>();
            UIFactory.SetAnchors(qrRect, Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));

            GameObject copy = UIFactory.CreateVerticalStack(row.transform, "MobileAccessCopy", Color.clear, 3, 0);
            LayoutElement copyLayout = copy.AddComponent<LayoutElement>();
            copyLayout.minWidth = 260;
            copyLayout.preferredWidth = 360;
            copyLayout.flexibleWidth = 1;

            mobileQrStatus = UIFactory.CreateText(copy.transform, "Scan For Mobile Access", 24, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            Text help = UIFactory.CreateText(copy.transform, "Open this alpha from a phone on the same Wi-Fi.", 17, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);
            help.resizeTextForBestFit = true;
            help.resizeTextMinSize = 12;
            help.resizeTextMaxSize = 17;
            mobileQrUrl = UIFactory.CreateText(copy.transform, "Preparing QR...", 15, TextAnchor.MiddleLeft, UIFactory.TextColor);
            mobileQrUrl.resizeTextForBestFit = true;
            mobileQrUrl.resizeTextMinSize = 10;
            mobileQrUrl.resizeTextMaxSize = 15;
        }

        private IEnumerator LoadMobileAccessUrl()
        {
            string fallbackUrl = BuildFallbackMobileUrl();
            string endpoint = BuildMobileUrlEndpoint();

            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                using (UnityWebRequest request = UnityWebRequest.Get(endpoint))
                {
                    request.timeout = 2;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        string body = request.downloadHandler.text;
                        if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith("{", StringComparison.Ordinal))
                        {
                            MobileUrlResponse response = JsonUtility.FromJson<MobileUrlResponse>(body);
                            if (!string.IsNullOrWhiteSpace(response.mobileUrl))
                            {
                                SetMobileQrUrl(response.mobileUrl, "Scan For Mobile Access");
                                yield break;
                            }
                        }
                    }
                }
            }

            SetMobileQrUrl(fallbackUrl, IsLoopbackUrl(fallbackUrl) ? "PC Preview QR" : "Scan For Mobile Access");
        }

        private static string TryGetWebGlMobileAccessUrl()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                return AppreciatorsGetMobileAccessUrl();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Mobile access URL bridge unavailable, falling back to UnityWebRequest: {exception.Message}");
            }
#endif
            return null;
        }

        private void SetMobileQrUrl(string url, string status)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                url = "http://127.0.0.1:8088/";
            }

            url = ShortenQrUrl(url);
            mobileQrStatus.text = status;
            mobileQrUrl.text = url;

            if (lastMobileUrl == url && mobileQrTexture != null)
            {
                mobileQrImage.texture = mobileQrTexture;
                mobileQrImage.color = Color.white;
                return;
            }

            if (mobileQrTexture != null)
            {
                Destroy(mobileQrTexture);
                mobileQrTexture = null;
            }

            try
            {
                mobileQrTexture = QrCodeTexture.Create(url, 5, 4);
                mobileQrImage.texture = mobileQrTexture;
                mobileQrImage.color = Color.white;
                lastMobileUrl = url;
            }
            catch (Exception exception)
            {
                mobileQrImage.texture = null;
                mobileQrImage.color = new Color(1f, 1f, 1f, 0.16f);
                mobileQrStatus.text = "QR unavailable";
                mobileQrUrl.text = exception.Message;
                Debug.LogError($"Failed to create mobile access QR: {exception.Message}");
            }
        }

        private static string BuildMobileUrlEndpoint()
        {
            if (!TryGetOrigin(Application.absoluteURL, out string origin))
            {
                return "http://127.0.0.1:8088/__appreciators/mobile-url";
            }

            return $"{origin}/__appreciators/mobile-url";
        }

        private static string BuildFallbackMobileUrl()
        {
            if (!string.IsNullOrWhiteSpace(Application.absoluteURL))
            {
                return Application.absoluteURL;
            }

            return "http://127.0.0.1:8088/?mobile=1";
        }

        private static string ShortenQrUrl(string url)
        {
            if (Encoding.UTF8.GetByteCount(url) <= 106)
            {
                return url;
            }

            if (TryGetOrigin(url, out string origin))
            {
                return $"{origin}/?mobile=1";
            }

            return url.Substring(0, Mathf.Min(url.Length, 100));
        }

        private static bool TryGetOrigin(string url, out string origin)
        {
            origin = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            try
            {
                Uri uri = new Uri(url);
                origin = uri.GetLeftPart(UriPartial.Authority);
                return !string.IsNullOrWhiteSpace(origin);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsLoopbackUrl(string url)
        {
            try
            {
                Uri uri = new Uri(url);
                return uri.Host == "127.0.0.1" || uri.Host == "localhost" || uri.IsLoopback;
            }
            catch
            {
                return false;
            }
        }

        [Serializable]
        private class MobileUrlResponse
        {
            public string mobileUrl;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string AppreciatorsGetMobileAccessUrl();
#endif
    }
}
