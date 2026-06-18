using System;
using System.Linq;
using System.Runtime.InteropServices;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class InviteMatchController : ScreenControllerBase
    {
        private const float PollIntervalSeconds = 2.5f;
        private const float LobbyPollIntervalSeconds = 4f;
        private const string InvitePlayerIdKey = "AppreciatorsInvitePlayerId";
        private BackendApiClient apiClient;
        private InputField codeInput;
        private Text roomText;
        private Text messageText;
        private Text lobbySummaryText;
        private Button startButton;
        private Button copyButton;
        private Text copyButtonLabel;
        private Button joinActionButton;
        private Button joinInlineButton;
        private Transform lobbyListRoot;
        private RawImage qrImage;
        private Text qrLabel;
        private Texture2D qrTexture;
        private string lastQrInviteCode;
        private string lastInviteLinkToCopy;
        private InviteRoom currentRoom;
        private InviteLobbyPlayer[] availablePlayers = new InviteLobbyPlayer[0];
        private InviteRoom[] incomingChallenges = new InviteRoom[0];
        private string playerId;
        private bool suppressCodeEvents;
        private bool isSubmitting;
        private bool enteringMatch;
        private string pendingRequestMessage;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AppreciatorsCopyText(string text, string gameObjectName, string successMethod, string errorMethod);
#endif

        private void Start()
        {
            apiClient = gameObject.AddComponent<BackendApiClient>();
            PlayerId();

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
            codeLayout.preferredHeight = 198;
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

            GameObject lobbyPanel = UIFactory.CreateVerticalStack(screen.transform, "AvailablePlayers", UIFactory.Panel, 6, 12);
            LayoutElement lobbyLayout = lobbyPanel.AddComponent<LayoutElement>();
            lobbyLayout.preferredHeight = 176;
            lobbyLayout.flexibleHeight = 0;
            GameObject lobbyHeader = UIFactory.CreateHorizontalStack(lobbyPanel.transform, "AvailablePlayersHeader", Color.clear, 8, 0);
            HorizontalLayoutGroup lobbyHeaderGroup = lobbyHeader.GetComponent<HorizontalLayoutGroup>();
            lobbyHeaderGroup.childForceExpandWidth = false;
            LayoutElement lobbyHeaderLayout = lobbyHeader.AddComponent<LayoutElement>();
            lobbyHeaderLayout.preferredHeight = 34;
            lobbySummaryText = UIFactory.CreateText(lobbyHeader.transform, "AVAILABLE PLAYERS", 22, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            LayoutElement summaryLayout = lobbySummaryText.gameObject.AddComponent<LayoutElement>();
            summaryLayout.flexibleWidth = 1;
            Button refreshPlayersButton = UIFactory.CreateButton(lobbyHeader.transform, "Refresh", PollInviteLobby, UIFactory.PanelAlt);
            LayoutElement refreshLayout = refreshPlayersButton.gameObject.GetComponent<LayoutElement>();
            refreshLayout.minWidth = 150;
            refreshLayout.preferredWidth = 160;
            refreshLayout.minHeight = 34;
            refreshLayout.preferredHeight = 38;

            GameObject lobbyList = UIFactory.CreateVerticalStack(lobbyPanel.transform, "AvailablePlayersList", Color.clear, 4, 0);
            LayoutElement lobbyListLayout = lobbyList.AddComponent<LayoutElement>();
            lobbyListLayout.flexibleHeight = 1;
            lobbyListRoot = lobbyList.transform;

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
            copyButtonLabel = copyButton.GetComponentInChildren<Text>();
            UIFactory.CreateButton(footer.transform, "Back", () => SceneManager.LoadScene("MainMenuScene"), UIFactory.PanelAlt);

            RenderLobby();
            UpdateRoomView("Create an invite or enter a code to join.");
            AnnounceInvitePresence();
            InvokeRepeating(nameof(PollInviteLobby), LobbyPollIntervalSeconds, LobbyPollIntervalSeconds);
            LoadInviteCodeFromUrl();
        }

        private void CreateInvite()
        {
            SetBusy($"Creating invite room...\nBackend: {AppConfig.ApiBaseUrl}");
            try
            {
                StartCoroutine(apiClient.CreateInviteMatch(PlayerName(), DeckIds(), PlayerId(), response =>
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
                StartCoroutine(apiClient.JoinInviteMatch(inviteCode, PlayerName(), DeckIds(), PlayerId(), response =>
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
                StartCoroutine(apiClient.StartInviteMatch(currentRoom.inviteCode, PlayerName(), PlayerId(), response =>
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

            lastInviteLinkToCopy = BuildInviteLink(inviteCode);

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                AppreciatorsCopyText(lastInviteLinkToCopy, gameObject.name, nameof(OnCopySucceeded), nameof(OnCopyFailed));
            }
            catch (Exception exception)
            {
                OnCopyFailed(exception.Message);
            }
#else
            GUIUtility.systemCopyBuffer = lastInviteLinkToCopy;
            OnCopySucceeded(string.Empty);
#endif
        }

        public void OnCopySucceeded(string _)
        {
            GUIUtility.systemCopyBuffer = lastInviteLinkToCopy;
            ShowCopyFeedback("COPIED", $"Invite link copied.\n{lastInviteLinkToCopy}");
        }

        public void OnCopyFailed(string error)
        {
            UpdateRoomView($"Copy failed: {error}\nInvite link: {lastInviteLinkToCopy}");
        }

        private void ShowCopyFeedback(string label, string message)
        {
            if (copyButtonLabel != null)
            {
                copyButtonLabel.text = label;
            }

            if (messageText != null)
            {
                messageText.text = message;
            }

            CancelInvoke(nameof(ResetCopyButtonLabel));
            Invoke(nameof(ResetCopyButtonLabel), 1.4f);
        }

        private void ResetCopyButtonLabel()
        {
            if (copyButtonLabel != null)
            {
                copyButtonLabel.text = "Copy Invite Link";
            }
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
            bool canCopy = hasRoom || CleanCode().Length == 6;
            startButton.interactable = canStart;
            copyButton.interactable = canCopy;
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
            SetPlayerId(response.player?.id ?? playerId);
            SetInviteCodeText(currentRoom.inviteCode);
            AnnounceInvitePresence();
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

        private void AnnounceInvitePresence()
        {
            if (apiClient == null || enteringMatch)
            {
                return;
            }

            StartCoroutine(apiClient.AnnounceInvitePresence(PlayerName(), DeckIds(), PlayerId(), ApplyLobbyResponse, _ => { }));
        }

        private void PollInviteLobby()
        {
            if (apiClient == null || enteringMatch)
            {
                return;
            }

            StartCoroutine(apiClient.GetInviteLobby(PlayerName(), PlayerId(), ApplyLobbyResponse, _ => { }));
        }

        private void ApplyLobbyResponse(InviteLobbyResponse response)
        {
            if (response == null)
            {
                return;
            }

            SetPlayerId(response.playerId);
            availablePlayers = response.players ?? new InviteLobbyPlayer[0];
            incomingChallenges = response.challenges ?? new InviteRoom[0];
            RenderLobby();
        }

        private void RenderLobby()
        {
            if (lobbyListRoot == null || lobbySummaryText == null)
            {
                return;
            }

            UIFactory.ClearChildren(lobbyListRoot);
            int challengeCount = incomingChallenges?.Length ?? 0;
            int playerCount = availablePlayers?.Length ?? 0;
            lobbySummaryText.text = challengeCount > 0
                ? $"AVAILABLE PLAYERS - {challengeCount} CHALLENGE"
                : $"AVAILABLE PLAYERS - {playerCount} ONLINE";

            int rows = 0;
            if (challengeCount > 0)
            {
                foreach (InviteRoom challenge in incomingChallenges.Take(2))
                {
                    InviteRoom captured = challenge;
                    string challenger = captured?.host?.username ?? "Player";
                    AddLobbyRow(
                        $"Incoming from {challenger} ({captured?.inviteCode})",
                        "Accept",
                        () => AcceptChallenge(captured),
                        UIFactory.Green);
                    rows += 1;
                }
            }

            int maxPlayerRows = challengeCount > 0 ? 2 : 3;
            foreach (InviteLobbyPlayer player in (availablePlayers ?? new InviteLobbyPlayer[0]).Take(maxPlayerRows))
            {
                InviteLobbyPlayer captured = player;
                AddLobbyRow(
                    $"{captured.username} - {captured.status} - deck {captured.deckSize}",
                    "Challenge",
                    () => ChallengePlayer(captured),
                    UIFactory.Blue);
                rows += 1;
            }

            if (rows == 0)
            {
                Text emptyText = UIFactory.CreateText(
                    lobbyListRoot,
                    "No available players yet. Ask another player to open Invite 1v1.",
                    17,
                    TextAnchor.MiddleLeft,
                    UIFactory.MutedTextColor);
                LayoutElement emptyLayout = emptyText.gameObject.AddComponent<LayoutElement>();
                emptyLayout.preferredHeight = 44;
            }
        }

        private void AddLobbyRow(string label, string buttonLabel, UnityAction action, Color buttonColor)
        {
            GameObject row = UIFactory.CreateHorizontalStack(lobbyListRoot, buttonLabel, new Color(0.02f, 0.035f, 0.070f, 0.72f), 8, 6);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            LayoutElement rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 42;
            rowLayout.preferredHeight = 46;
            rowLayout.flexibleHeight = 0;

            Text labelText = UIFactory.CreateText(row.transform, label, 17, TextAnchor.MiddleLeft, UIFactory.TextColor, FontStyle.Bold);
            LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1;

            Button rowButton = UIFactory.CreateButton(row.transform, buttonLabel, action, buttonColor);
            LayoutElement buttonLayout = rowButton.gameObject.GetComponent<LayoutElement>();
            buttonLayout.minWidth = 152;
            buttonLayout.preferredWidth = 164;
            buttonLayout.minHeight = 38;
            buttonLayout.preferredHeight = 42;
            buttonLayout.flexibleWidth = 0;
        }

        private void ChallengePlayer(InviteLobbyPlayer target)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.id))
            {
                UpdateRoomView("That player is no longer available.");
                return;
            }

            SetBusy($"Challenging {target.username}...\nBackend: {AppConfig.ApiBaseUrl}");
            try
            {
                StartCoroutine(apiClient.ChallengeInvitePlayer(target.id, PlayerName(), DeckIds(), PlayerId(), response =>
                {
                    if (ApplyMutationResponse(response, "Backend returned no challenge room."))
                    {
                        string challenged = response.challengedPlayer?.username ?? target.username;
                        UpdateRoomView($"Challenge sent to {challenged}.\nShare fallback: {BuildInviteLink(currentRoom.inviteCode)}");
                    }
                }, ShowError));
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
        }

        private void AcceptChallenge(InviteRoom challenge)
        {
            if (challenge == null || string.IsNullOrWhiteSpace(challenge.inviteCode))
            {
                UpdateRoomView("That challenge is no longer available.");
                return;
            }

            currentRoom = challenge;
            SetInviteCodeText(challenge.inviteCode);
            JoinInvite();
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

        private string PlayerId()
        {
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                return playerId;
            }

            playerId = PlayerPrefs.GetString(InvitePlayerIdKey, string.Empty);
            if (string.IsNullOrWhiteSpace(playerId))
            {
                playerId = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(InvitePlayerIdKey, playerId);
                PlayerPrefs.Save();
            }

            return playerId;
        }

        private void SetPlayerId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            playerId = value;
            PlayerPrefs.SetString(InvitePlayerIdKey, playerId);
            PlayerPrefs.Save();
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
            CancelInvoke(nameof(PollInviteStatus));
            CancelInvoke(nameof(PollInviteLobby));
            CancelInvoke(nameof(ShowRequestTimeout));
            CancelInvoke(nameof(ResetCopyButtonLabel));

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
