using System.Linq;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class InviteMatchController : ScreenControllerBase
    {
        private const float PollIntervalSeconds = 2.5f;
        private BackendApiClient apiClient;
        private InputField codeInput;
        private Text roomText;
        private Text messageText;
        private Button startButton;
        private Button copyButton;
        private Button joinActionButton;
        private Button joinInlineButton;
        private RawImage qrImage;
        private Text qrLabel;
        private Texture2D qrTexture;
        private string lastQrInviteCode;
        private InviteRoom currentRoom;
        private string playerId;
        private bool suppressCodeEvents;
        private bool isSubmitting;
        private bool enteringMatch;
        private string pendingRequestMessage;

        private void Start()
        {
            apiClient = gameObject.AddComponent<BackendApiClient>();

            GameObject screen = CreateFullScreenStack("Invite 1v1");
            UIFactory.CreateText(screen.transform, "Be Original", 26, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);

            GameObject actions = UIFactory.CreateHorizontalStack(screen.transform, "InviteActions", UIFactory.Panel, 12, 12);
            LayoutElement actionsLayout = actions.AddComponent<LayoutElement>();
            actionsLayout.preferredHeight = 86;
            UIFactory.CreateButton(actions.transform, "Create Invite", CreateInvite, UIFactory.Green);
            joinActionButton = UIFactory.CreateButton(actions.transform, "Join Entered Code", JoinInvite, UIFactory.Blue);
            UIFactory.CreateButton(actions.transform, "Refresh Code", RefreshInvite, UIFactory.PanelAlt);

            GameObject codePanel = UIFactory.CreateVerticalStack(screen.transform, "InviteCodeEntry", UIFactory.PanelAlt, 8, 14);
            LayoutElement codeLayout = codePanel.AddComponent<LayoutElement>();
            codeLayout.preferredHeight = 225;
            UIFactory.CreateText(codePanel.transform, "ENTER INVITE CODE", 26, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            codeInput = UIFactory.CreateInputField(codePanel.transform, "TYPE CODE HERE", string.Empty);
            codeInput.characterLimit = 6;
            codeInput.contentType = InputField.ContentType.Alphanumeric;
            codeInput.textComponent.fontSize = 36;
            codeInput.textComponent.alignment = TextAnchor.MiddleCenter;
            if (codeInput.placeholder is Text placeholderText)
            {
                placeholderText.fontSize = 32;
                placeholderText.alignment = TextAnchor.MiddleCenter;
            }

            LayoutElement inputLayout = codeInput.gameObject.GetComponent<LayoutElement>();
            inputLayout.minHeight = 76;
            inputLayout.preferredHeight = 82;
            codeInput.gameObject.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.09f);
            codeInput.onValueChanged.AddListener(NormalizeInviteCode);
            codeInput.onEndEdit.AddListener(_ => JoinIfCodeReady());
            joinInlineButton = UIFactory.CreateButton(codePanel.transform, "Join This Code", JoinInvite, UIFactory.Blue);

            GameObject roomPanel = UIFactory.CreateVerticalStack(screen.transform, "RoomStatus", UIFactory.Panel, 10, 16);
            LayoutElement roomLayout = roomPanel.AddComponent<LayoutElement>();
            roomLayout.flexibleHeight = 1;

            GameObject roomBody = UIFactory.CreateHorizontalStack(roomPanel.transform, "RoomBody", Color.clear, 14, 0);
            HorizontalLayoutGroup roomBodyGroup = roomBody.GetComponent<HorizontalLayoutGroup>();
            roomBodyGroup.childForceExpandWidth = false;
            LayoutElement roomBodyLayout = roomBody.AddComponent<LayoutElement>();
            roomBodyLayout.flexibleHeight = 1;

            GameObject statusColumn = UIFactory.CreateVerticalStack(roomBody.transform, "RoomStatusText", Color.clear, 8, 0);
            LayoutElement statusLayout = statusColumn.AddComponent<LayoutElement>();
            statusLayout.flexibleWidth = 1;
            roomText = UIFactory.CreateText(statusColumn.transform, "No invite room selected.", 24, TextAnchor.UpperLeft, UIFactory.TextColor, FontStyle.Bold);
            messageText = UIFactory.CreateText(statusColumn.transform, $"Backend: {AppConfig.ApiBaseUrl}", 20, TextAnchor.UpperLeft, UIFactory.MutedTextColor);

            GameObject qrColumn = UIFactory.CreateVerticalStack(roomBody.transform, "InviteQr", UIFactory.PanelAlt, 6, 10);
            LayoutElement qrColumnLayout = qrColumn.AddComponent<LayoutElement>();
            qrColumnLayout.minWidth = 235;
            qrColumnLayout.preferredWidth = 260;
            qrColumnLayout.flexibleWidth = 0;
            qrLabel = UIFactory.CreateText(qrColumn.transform, "QR unlocks after invite", 18, TextAnchor.MiddleCenter, UIFactory.MutedTextColor, FontStyle.Bold);
            GameObject qrObject = new GameObject("InviteQrImage", typeof(RectTransform), typeof(RawImage));
            qrObject.transform.SetParent(qrColumn.transform, false);
            qrImage = qrObject.GetComponent<RawImage>();
            qrImage.color = new Color(1f, 1f, 1f, 0.25f);
            LayoutElement qrLayout = qrObject.AddComponent<LayoutElement>();
            qrLayout.minHeight = 190;
            qrLayout.preferredHeight = 210;
            qrLayout.minWidth = 190;
            qrLayout.preferredWidth = 210;

            GameObject footer = UIFactory.CreateHorizontalStack(screen.transform, "InviteFooter", UIFactory.Panel, 12, 12);
            LayoutElement footerLayout = footer.AddComponent<LayoutElement>();
            footerLayout.preferredHeight = 86;
            startButton = UIFactory.CreateButton(footer.transform, "Start 1v1", StartInvite, UIFactory.Accent);
            copyButton = UIFactory.CreateButton(footer.transform, "Copy Invite Link", CopyCode, UIFactory.PanelAlt);
            UIFactory.CreateButton(footer.transform, "Back", () => SceneManager.LoadScene("MainMenuScene"), UIFactory.PanelAlt);

            UpdateRoomView("Create an invite or enter a code to join.");
            LoadInviteCodeFromUrl();
        }

        private void CreateInvite()
        {
            SetBusy($"Creating invite room...\nBackend: {AppConfig.ApiBaseUrl}");
            try
            {
                StartCoroutine(apiClient.CreateInviteMatch(PlayerName(), DeckIds(), response =>
                {
                    if (ApplyMutationResponse(response, "Backend returned no invite code."))
                    {
                        UpdateRoomView($"Invite room created.\nShare: {BuildInviteLink(currentRoom.inviteCode)}");
                    }
                }, ShowError));
            }
            catch (System.Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void JoinInvite()
        {
            string inviteCode = CleanCode();
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                UpdateRoomView("Enter an invite code first.");
                return;
            }

            SetBusy($"Joining invite room...\nBackend: {AppConfig.ApiBaseUrl}");
            try
            {
                StartCoroutine(apiClient.JoinInviteMatch(inviteCode, PlayerName(), DeckIds(), response =>
                {
                    if (ApplyMutationResponse(response, "Backend returned no joined invite room."))
                    {
                        UpdateRoomView("Joined invite room.");
                    }
                }, ShowError));
            }
            catch (System.Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void RefreshInvite()
        {
            string inviteCode = CleanCode();
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                UpdateRoomView("Enter an invite code first.");
                return;
            }

            SetBusy($"Refreshing invite room...\nBackend: {AppConfig.ApiBaseUrl}");
            try
            {
                StartCoroutine(apiClient.GetInviteMatch(inviteCode, response =>
                {
                    if (response?.room == null || string.IsNullOrWhiteSpace(response.room.inviteCode))
                    {
                        ShowError("Backend returned no invite room.");
                        return;
                    }

                    currentRoom = response.room;
                    SetInviteCodeText(currentRoom.inviteCode);
                    if (currentRoom.status == "started")
                    {
                        EnterStartedMatch("Invite session already started.");
                        return;
                    }

                    UpdateRoomView("Invite room refreshed.");
                }, ShowError));
            }
            catch (System.Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void StartInvite()
        {
            if (currentRoom == null)
            {
                UpdateRoomView("Create or join an invite room first.");
                return;
            }

            SetBusy($"Starting invite match...\nBackend: {AppConfig.ApiBaseUrl}");
            try
            {
                StartCoroutine(apiClient.StartInviteMatch(currentRoom.inviteCode, PlayerName(), playerId, response =>
                {
                    if (ApplyMutationResponse(response, "Backend returned no started invite room."))
                    {
                        EnterStartedMatch(response.assignment?.message ?? "Invite match started.");
                    }
                }, ShowError));
            }
            catch (System.Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void CopyCode()
        {
            string inviteCode = CleanCode();
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                UpdateRoomView("No invite code to copy.");
                return;
            }

            GUIUtility.systemCopyBuffer = BuildInviteLink(inviteCode);
            UpdateRoomView("Invite link copied.");
        }

        private void SetBusy(string message)
        {
            isSubmitting = true;
            pendingRequestMessage = message;
            messageText.text = message;
            startButton.interactable = false;
            copyButton.interactable = false;
            SetJoinButtons(false);
            CancelInvoke(nameof(ShowRequestTimeout));
            Invoke(nameof(ShowRequestTimeout), 12f);
        }

        private void ShowError(string error)
        {
            UpdateRoomView($"Backend error: {error}");
        }

        private void UpdateRoomView(string message)
        {
            if (currentRoom == null)
            {
                roomText.text = "No invite room selected.";
            }
            else
            {
                string host = currentRoom.host == null ? "Open" : currentRoom.host.username;
                string guest = currentRoom.guest == null ? "Waiting" : currentRoom.guest.username;
                roomText.text =
                    $"Code: {currentRoom.inviteCode}\n" +
                    $"Status: {currentRoom.status}\n" +
                    $"Host: {host}\n" +
                    $"Guest: {guest}\n" +
                    $"Match: {currentRoom.matchId}";
            }

            messageText.text = message;
            UpdateQrCode();
            isSubmitting = false;
            pendingRequestMessage = string.Empty;
            CancelInvoke(nameof(ShowRequestTimeout));
            bool hasRoom = currentRoom != null;
            bool canStart = hasRoom && currentRoom.status == "ready";
            startButton.interactable = canStart;
            copyButton.interactable = hasRoom;
            SetJoinButtons(true);

            if (hasRoom && currentRoom.status == "waiting" && currentRoom.host != null && currentRoom.host.id == playerId)
            {
                messageText.text = $"{message}\nWaiting for guest. Start unlocks after they join.";
            }
            else if (hasRoom && currentRoom.status == "ready")
            {
                messageText.text = $"{message}\nRoom ready. Either player can start the match.";
            }
            else if (hasRoom && currentRoom.status == "started")
            {
                messageText.text = $"{message}\nEntering match...";
            }

            UpdatePolling();
        }

        private void ShowRequestTimeout()
        {
            if (!isSubmitting)
            {
                return;
            }

            UpdateRoomView($"{pendingRequestMessage}\nStill waiting. Check that the backend is reachable at {AppConfig.ApiBaseUrl}.");
        }

        private bool ApplyMutationResponse(InviteRoomMutationResponse response, string emptyMessage)
        {
            if (response?.room == null || string.IsNullOrWhiteSpace(response.room.inviteCode))
            {
                ShowError(emptyMessage);
                return false;
            }

            currentRoom = response.room;
            playerId = response.player?.id ?? playerId;
            SetInviteCodeText(currentRoom.inviteCode);
            return true;
        }

        private void UpdatePolling()
        {
            CancelInvoke(nameof(PollInviteStatus));
            if (enteringMatch || currentRoom == null || currentRoom.status == "started")
            {
                return;
            }

            InvokeRepeating(nameof(PollInviteStatus), PollIntervalSeconds, PollIntervalSeconds);
        }

        private void PollInviteStatus()
        {
            if (isSubmitting || enteringMatch || currentRoom == null)
            {
                return;
            }

            string inviteCode = currentRoom.inviteCode;
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                return;
            }

            StartCoroutine(apiClient.GetInviteMatch(inviteCode, response =>
            {
                if (response?.room == null)
                {
                    return;
                }

                currentRoom = response.room;
                SetInviteCodeText(currentRoom.inviteCode);

                if (currentRoom.status == "started")
                {
                    EnterStartedMatch("Invite session started by the other player.");
                    return;
                }

                UpdateRoomView(currentRoom.status == "ready" ? "Room synced." : "Waiting for guest...");
            }, _ => { }));
        }

        private void EnterStartedMatch(string message)
        {
            if (enteringMatch || currentRoom == null)
            {
                return;
            }

            enteringMatch = true;
            CancelInvoke(nameof(PollInviteStatus));
            LocalSaveSystem.SavePendingMatchContext(
                "Invite 1v1",
                currentRoom.inviteCode,
                currentRoom.matchId,
                OpponentName(),
                playerId,
                PlayerRole());

            UpdateRoomView($"{message}\nLoading match scene...");
            Invoke(nameof(LoadMatchScene), 0.75f);
        }

        private void LoadMatchScene()
        {
            SceneManager.LoadScene("MatchScene");
        }

        private string OpponentName()
        {
            if (currentRoom?.host == null)
            {
                return currentRoom?.guest?.username ?? "Invite Opponent";
            }

            if (!string.IsNullOrWhiteSpace(playerId) && currentRoom.host.id == playerId)
            {
                return currentRoom.guest?.username ?? "Invite Opponent";
            }

            return currentRoom.host.username;
        }

        private string PlayerRole()
        {
            if (currentRoom?.host != null && currentRoom.host.id == playerId)
            {
                return currentRoom.host.role;
            }

            if (currentRoom?.guest != null && currentRoom.guest.id == playerId)
            {
                return currentRoom.guest.role;
            }

            return string.Empty;
        }

        private string CleanCode()
        {
            return codeInput == null ? string.Empty : codeInput.text.Trim().ToUpperInvariant();
        }

        private void NormalizeInviteCode(string value)
        {
            if (suppressCodeEvents)
            {
                return;
            }

            string normalized = new string((value ?? string.Empty)
                .ToUpperInvariant()
                .Where(char.IsLetterOrDigit)
                .Take(6)
                .ToArray());

            if (codeInput != null && codeInput.text != normalized)
            {
                suppressCodeEvents = true;
                codeInput.text = normalized;
                suppressCodeEvents = false;
            }

            if (normalized.Length == 6)
            {
                JoinIfCodeReady();
            }
        }

        private void JoinIfCodeReady()
        {
            string inviteCode = CleanCode();
            if (isSubmitting || inviteCode.Length != 6)
            {
                return;
            }

            if (currentRoom != null && currentRoom.inviteCode == inviteCode)
            {
                return;
            }

            JoinInvite();
        }

        private void SetInviteCodeText(string inviteCode)
        {
            suppressCodeEvents = true;
            codeInput.text = inviteCode ?? string.Empty;
            suppressCodeEvents = false;
        }

        private void SetJoinButtons(bool interactable)
        {
            if (joinActionButton != null)
            {
                joinActionButton.interactable = interactable;
            }

            if (joinInlineButton != null)
            {
                joinInlineButton.interactable = interactable;
            }
        }

        private void LoadInviteCodeFromUrl()
        {
            string inviteCode = InviteCodeFromUrl(Application.absoluteURL);
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                return;
            }

            SetInviteCodeText(inviteCode);
            UpdateRoomView($"Invite code loaded from link: {inviteCode}");
            Invoke(nameof(JoinIfCodeReady), 0.35f);
        }

        private static string InviteCodeFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return string.Empty;
            }

            try
            {
                System.Uri uri = new System.Uri(url);
                string query = uri.Query.TrimStart('?');
                foreach (string part in query.Split('&'))
                {
                    string[] pieces = part.Split('=');
                    if (pieces.Length != 2)
                    {
                        continue;
                    }

                    string key = System.Uri.UnescapeDataString(pieces[0]).ToLowerInvariant();
                    if (key != "invite" && key != "code" && key != "join")
                    {
                        continue;
                    }

                    return new string(System.Uri.UnescapeDataString(pieces[1])
                        .ToUpperInvariant()
                        .Where(char.IsLetterOrDigit)
                        .Take(6)
                        .ToArray());
                }
            }
            catch
            {
                return string.Empty;
            }

            return string.Empty;
        }

        private static string BuildInviteLink(string inviteCode)
        {
            string pageUrl = string.IsNullOrWhiteSpace(Application.absoluteURL)
                ? "http://192.168.1.113:8088/"
                : Application.absoluteURL;

            int queryIndex = pageUrl.IndexOf('?');
            if (queryIndex >= 0)
            {
                pageUrl = pageUrl.Substring(0, queryIndex);
            }

            return $"{pageUrl}?invite={inviteCode}";
        }

        private void UpdateQrCode()
        {
            if (qrImage == null || qrLabel == null)
            {
                return;
            }

            string inviteCode = currentRoom?.inviteCode;
            if (string.IsNullOrWhiteSpace(inviteCode))
            {
                if (qrTexture != null)
                {
                    Destroy(qrTexture);
                    qrTexture = null;
                }

                lastQrInviteCode = string.Empty;
                qrImage.texture = null;
                qrImage.color = new Color(1f, 1f, 1f, 0.18f);
                qrLabel.text = "QR unlocks after invite";
                return;
            }

            if (lastQrInviteCode == inviteCode && qrTexture != null)
            {
                qrImage.texture = qrTexture;
                qrImage.color = Color.white;
                qrLabel.text = $"Scan to join\n{inviteCode}";
                return;
            }

            try
            {
                if (qrTexture != null)
                {
                    Destroy(qrTexture);
                    qrTexture = null;
                }

                string inviteLink = BuildInviteLink(inviteCode);
                qrTexture = QrCodeTexture.Create(inviteLink, 6, 4);
                lastQrInviteCode = inviteCode;
                qrImage.texture = qrTexture;
                qrImage.color = Color.white;
                qrLabel.text = $"Scan to join\n{inviteCode}";
            }
            catch (System.Exception exception)
            {
                qrImage.texture = null;
                qrImage.color = new Color(1f, 1f, 1f, 0.18f);
                qrLabel.text = $"QR unavailable\n{exception.Message}";
            }
        }

        private void OnDestroy()
        {
            if (qrTexture != null)
            {
                Destroy(qrTexture);
                qrTexture = null;
            }
        }

        private static string PlayerName()
        {
            return LocalSaveSystem.LoadPlayerName();
        }

        private static string[] DeckIds()
        {
            return PlayerDeckService.LoadDeckIdsOrStarter().ToArray();
        }
    }
}
