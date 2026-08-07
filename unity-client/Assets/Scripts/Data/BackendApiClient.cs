using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AppreciatorsTcg.Data
{
    public class BackendApiClient : MonoBehaviour
    {
        private const int RequestTimeoutSeconds = 10;
        private const int PackRequestTimeoutSeconds = 45;
        private Action<string> pendingGetSuccess;
        private Action<string> pendingGetError;
        private bool pendingGetComplete;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AppreciatorsFetchGet(string url, string gameObjectName, string successMethod, string errorMethod);
#endif

        private void Awake()
        {
            gameObject.name = $"BackendApiClientHost_{GetInstanceID()}";
        }

        public IEnumerator GetCards(System.Action<string> onSuccess, System.Action<string> onError)
        {
            yield return Get("/api/cards", onSuccess, onError);
        }

        public IEnumerator CheckHealth(System.Action<string> onSuccess, System.Action<string> onError)
        {
            yield return Get("/health", onSuccess, onError);
        }

        public IEnumerator CreateInviteMatch(string username, string[] deckIds, string playerId, System.Action<InviteRoomMutationResponse> onSuccess, System.Action<string> onError)
        {
            string query = $"{BuildInviteQuery(username, deckIds)}&playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}";
            yield return GetJson($"/api/matchmaking/invite/new?{query}", onSuccess, onError);
        }

        public IEnumerator GetInviteMatch(string inviteCode, System.Action<InviteRoomStatusResponse> onSuccess, System.Action<string> onError)
        {
            yield return GetJson($"/api/matchmaking/invite/{UnityWebRequest.EscapeURL(inviteCode)}", onSuccess, onError);
        }

        public IEnumerator JoinInviteMatch(string inviteCode, string username, string[] deckIds, string playerId, System.Action<InviteRoomMutationResponse> onSuccess, System.Action<string> onError)
        {
            string query = $"{BuildInviteQuery(username, deckIds)}&playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}";
            yield return GetJson($"/api/matchmaking/invite/{UnityWebRequest.EscapeURL(inviteCode)}/join-link?{query}", onSuccess, onError);
        }

        public IEnumerator AnnounceInvitePresence(string username, string[] deckIds, string playerId, System.Action<InviteLobbyResponse> onSuccess, System.Action<string> onError)
        {
            string query = $"{BuildInviteQuery(username, deckIds)}&playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}";
            yield return GetJson($"/api/matchmaking/invite-lobby/announce?{query}", onSuccess, onError);
        }

        public IEnumerator GetInviteLobby(string username, string playerId, System.Action<InviteLobbyResponse> onSuccess, System.Action<string> onError)
        {
            string query = $"username={UnityWebRequest.EscapeURL(username ?? "Guest")}&playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}";
            yield return GetJson($"/api/matchmaking/invite-lobby?{query}", onSuccess, onError);
        }

        public IEnumerator ChallengeInvitePlayer(string targetPlayerId, string username, string[] deckIds, string playerId, System.Action<InviteRoomMutationResponse> onSuccess, System.Action<string> onError)
        {
            string query =
                $"{BuildInviteQuery(username, deckIds)}" +
                $"&playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}" +
                $"&targetPlayerId={UnityWebRequest.EscapeURL(targetPlayerId ?? string.Empty)}";
            yield return GetJson($"/api/matchmaking/invite-lobby/challenge?{query}", onSuccess, onError);
        }

        public IEnumerator StartInviteMatch(string inviteCode, string username, string playerId, System.Action<InviteRoomMutationResponse> onSuccess, System.Action<string> onError)
        {
            string query = $"{BuildInviteQuery(username, null)}&playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}";
            yield return GetJson($"/api/matchmaking/invite/{UnityWebRequest.EscapeURL(inviteCode)}/start-link?{query}", onSuccess, onError);
        }

        public IEnumerator GetInviteActions(string inviteCode, int afterSequence, System.Action<InviteActionListResponse> onSuccess, System.Action<string> onError)
        {
            yield return GetJson($"/api/matchmaking/invite/{UnityWebRequest.EscapeURL(inviteCode)}/actions?after={afterSequence}", onSuccess, onError);
        }

        public IEnumerator GetInviteMatchState(string inviteCode, System.Action<InviteMatchStateResponse> onSuccess, System.Action<string> onError)
        {
            yield return GetJson($"/api/matchmaking/invite/{UnityWebRequest.EscapeURL(inviteCode)}/state", onSuccess, onError);
        }

        public IEnumerator RespondInviteTermination(string inviteCode, string playerId, string decision, System.Action<InviteMatchStateResponse> onSuccess, System.Action<string> onError)
        {
            string query =
                $"playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}" +
                $"&decision={UnityWebRequest.EscapeURL(decision ?? "request")}";
            yield return GetJson($"/api/matchmaking/invite/{UnityWebRequest.EscapeURL(inviteCode)}/termination-link?{query}", onSuccess, onError);
        }

        public IEnumerator RecordInviteAction(
            string inviteCode,
            string playerId,
            string actionId,
            string type,
            string cardId,
            string lane,
            int turn,
            System.Action<InviteActionMutationResponse> onSuccess,
            System.Action<string> onError)
        {
            string query =
                $"playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}" +
                $"&actionId={UnityWebRequest.EscapeURL(actionId ?? string.Empty)}" +
                $"&type={UnityWebRequest.EscapeURL(type ?? string.Empty)}" +
                $"&cardId={UnityWebRequest.EscapeURL(cardId ?? string.Empty)}" +
                $"&lane={UnityWebRequest.EscapeURL(lane ?? string.Empty)}" +
                $"&turn={turn}";

            yield return GetJson($"/api/matchmaking/invite/{UnityWebRequest.EscapeURL(inviteCode)}/action?{query}", onSuccess, onError);
        }

        public IEnumerator VerifyMockWallet(string walletAddress, string username, System.Action<WalletVerifyResponse> onSuccess, System.Action<string> onError)
        {
            string query = $"walletAddress={UnityWebRequest.EscapeURL(walletAddress ?? string.Empty)}&username={UnityWebRequest.EscapeURL(username ?? "Guest")}";
            yield return GetJson($"/api/wallet/verify-link?{query}", onSuccess, onError);
        }

        public IEnumerator SyncMockNftOwnership(string walletAddress, System.Action<NftSyncResponse> onSuccess, System.Action<string> onError)
        {
            string query = $"walletAddress={UnityWebRequest.EscapeURL(walletAddress ?? string.Empty)}";
            yield return GetJson($"/api/nft/sync-link?{query}", onSuccess, onError);
        }

        public IEnumerator SimulateMockMint(string walletAddress, int quantity, System.Action<MintSimulationResponse> onSuccess, System.Action<string> onError)
        {
            string query = $"walletAddress={UnityWebRequest.EscapeURL(walletAddress ?? string.Empty)}&quantity={quantity}";
            yield return GetJson($"/api/mint/simulate-link?{query}", onSuccess, onError);
        }

        public IEnumerator GetPackInventory(string playerId, System.Action<PackInventoryResponse> onSuccess, System.Action<string> onError)
        {
            yield return GetJson($"/api/packs/inventory?playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}", onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator LoginAccount(string username, string playerId, System.Action<AccountLoginResponse> onSuccess, System.Action<string> onError)
        {
            AccountLoginRequest payload = new AccountLoginRequest
            {
                username = username,
                playerId = playerId
            };
            yield return PostJson("/api/session/login", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator GetPackOdds(string packId, System.Action<PackOddsResponse> onSuccess, System.Action<string> onError)
        {
            yield return GetJson($"/api/packs/odds/{UnityWebRequest.EscapeURL(packId ?? string.Empty)}", onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator GrantTestPack(string playerId, string packId, int count, System.Action<PackGrantResponse> onSuccess, System.Action<string> onError)
        {
            PackGrantRequest payload = new PackGrantRequest
            {
                playerId = playerId,
                packId = packId,
                count = Mathf.Clamp(count, 1, 10)
            };
            yield return PostJson("/api/packs/grant-test-pack", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator OpenPack(string requestId, string playerId, string packId, string attunement, System.Action<SignedPackRewardResponse> onSuccess, System.Action<string> onError)
        {
            PackOpenRequest payload = new PackOpenRequest
            {
                requestId = requestId,
                playerId = playerId,
                packId = packId,
                attunement = attunement
            };
            yield return PostJson("/api/packs/open", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator PurchasePack(string requestId, string playerId, string packId, System.Action<PackPurchaseResponse> onSuccess, System.Action<string> onError)
        {
            PackPurchaseRequest payload = new PackPurchaseRequest
            {
                requestId = requestId,
                playerId = playerId,
                packId = packId
            };
            yield return PostJson("/api/packs/purchase", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator ClaimMatchResultReward(string playerId, string matchId, string result, string mode, System.Action<MatchWinRewardResponse> onSuccess, System.Action<string> onError)
        {
            MatchWinRewardRequest payload = new MatchWinRewardRequest
            {
                playerId = playerId,
                matchId = matchId,
                result = result,
                mode = mode
            };
            yield return PostJson("/api/economy/match-result", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator ClaimMatchWinReward(string playerId, string matchId, System.Action<MatchWinRewardResponse> onSuccess, System.Action<string> onError)
        {
            MatchWinRewardRequest payload = new MatchWinRewardRequest
            {
                playerId = playerId,
                matchId = matchId,
                result = "Victory",
                mode = "Casual"
            };
            yield return PostJson("/api/economy/match-win", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator ClaimTutorialCompletionReward(string playerId, System.Action<TutorialRewardResponse> onSuccess, System.Action<string> onError)
        {
            PackPlayerRequest payload = new PackPlayerRequest { playerId = playerId };
            yield return PostJson("/api/economy/tutorial-complete", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator GetBossPool(string poolId, System.Action<BossPoolResponse> onSuccess, System.Action<string> onError)
        {
            yield return GetJson($"/api/economy/boss-pool?poolId={UnityWebRequest.EscapeURL(poolId ?? string.Empty)}", onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator ContributeBossShards(string requestId, string playerId, string poolId, int amount, System.Action<BossContributionResponse> onSuccess, System.Action<string> onError)
        {
            BossContributionRequest payload = new BossContributionRequest
            {
                requestId = requestId,
                playerId = playerId,
                poolId = poolId,
                amount = Mathf.Max(1, amount)
            };
            yield return PostJson("/api/economy/boss-contribute", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator GetBossBattle(string poolId, string playerId, System.Action<BossBattleResponse> onSuccess, System.Action<string> onError)
        {
            string path = $"/api/boss-battles/{UnityWebRequest.EscapeURL(poolId ?? string.Empty)}?playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}";
            yield return GetJson(path, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator JoinBossParty(string poolId, string playerId, string displayName, System.Action<BossBattleResponse> onSuccess, System.Action<string> onError)
        {
            yield return PostBossMutation(poolId, "join", playerId, displayName, false, string.Empty, onSuccess, onError);
        }

        public IEnumerator LeaveBossParty(string poolId, string playerId, System.Action<BossBattleResponse> onSuccess, System.Action<string> onError)
        {
            yield return PostBossMutation(poolId, "leave", playerId, string.Empty, false, string.Empty, onSuccess, onError);
        }

        public IEnumerator SetBossPartyReady(string poolId, string playerId, bool ready, System.Action<BossBattleResponse> onSuccess, System.Action<string> onError)
        {
            yield return PostBossMutation(poolId, "ready", playerId, string.Empty, ready, string.Empty, onSuccess, onError);
        }

        public IEnumerator ClaimBossRole(string poolId, string playerId, string displayName, System.Action<BossBattleResponse> onSuccess, System.Action<string> onError)
        {
            yield return PostBossMutation(poolId, "claim-boss", playerId, displayName, false, string.Empty, onSuccess, onError);
        }

        public IEnumerator ReleaseBossRole(string poolId, string playerId, System.Action<BossBattleResponse> onSuccess, System.Action<string> onError)
        {
            yield return PostBossMutation(poolId, "release-boss", playerId, string.Empty, false, string.Empty, onSuccess, onError);
        }

        public IEnumerator ChallengeBoss(string poolId, string playerId, System.Action<BossBattleResponse> onSuccess, System.Action<string> onError)
        {
            yield return PostBossMutation(poolId, "challenge", playerId, string.Empty, false, string.Empty, onSuccess, onError);
        }

        public IEnumerator PracticeBossAgainstAi(string poolId, string playerId, string selectedBossTokenId, System.Action<BossBattleResponse> onSuccess, System.Action<string> onError)
        {
            yield return PostBossMutation(poolId, "practice", playerId, string.Empty, false, selectedBossTokenId, onSuccess, onError);
        }

        public IEnumerator GetWalletAccount(string playerId, System.Action<WalletAccountResponse> onSuccess, System.Action<string> onError)
        {
            yield return GetJson($"/api/wallet/account?playerId={UnityWebRequest.EscapeURL(playerId ?? string.Empty)}", onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator LinkWalletAccount(string playerId, string walletAddress, System.Action<WalletAccountResponse> onSuccess, System.Action<string> onError)
        {
            WalletAccountRequest payload = new WalletAccountRequest { playerId = playerId, walletAddress = walletAddress };
            yield return PostJson("/api/wallet/account/link", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator CreateWalletChallenge(string playerId, string walletAddress, System.Action<WalletChallengeResponse> onSuccess, System.Action<string> onError)
        {
            WalletAccountRequest payload = new WalletAccountRequest { playerId = playerId, walletAddress = walletAddress };
            yield return PostJson("/api/wallet/account/challenge", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator VerifyWalletChallenge(
            string playerId,
            string walletAddress,
            string challengeId,
            string signature,
            System.Action<WalletAccountResponse> onSuccess,
            System.Action<string> onError)
        {
            WalletVerificationRequest payload = new WalletVerificationRequest
            {
                playerId = playerId,
                walletAddress = walletAddress,
                challengeId = challengeId,
                signature = signature
            };
            yield return PostJson("/api/wallet/account/verify", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator DisconnectWalletAccount(string playerId, System.Action<WalletAccountResponse> onSuccess, System.Action<string> onError)
        {
            WalletAccountRequest payload = new WalletAccountRequest { playerId = playerId, walletAddress = string.Empty };
            yield return PostJson("/api/wallet/account/disconnect", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator GrantAdminAccess(string playerId, string walletAddress, System.Action<AdminGrantResponse> onSuccess, System.Action<string> onError)
        {
            AdminGrantRequest payload = new AdminGrantRequest { playerId = playerId, walletAddress = walletAddress };
            yield return PostJson("/api/admin/wallets/add", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        private IEnumerator PostBossMutation(
            string poolId,
            string action,
            string playerId,
            string displayName,
            bool ready,
            string selectedBossTokenId,
            System.Action<BossBattleResponse> onSuccess,
            System.Action<string> onError)
        {
            BossBattlePlayerRequest payload = new BossBattlePlayerRequest
            {
                playerId = playerId,
                displayName = displayName,
                ready = ready,
                selectedBossTokenId = selectedBossTokenId
            };
            yield return PostJson(
                $"/api/boss-battles/{UnityWebRequest.EscapeURL(poolId ?? string.Empty)}/{UnityWebRequest.EscapeURL(action ?? string.Empty)}",
                payload,
                onSuccess,
                onError,
                PackRequestTimeoutSeconds);
        }

        public IEnumerator SimulatePackOpenings(string packId, string attunement, int count, System.Action<PackSimulationResponse> onSuccess, System.Action<string> onError)
        {
            PackSimulationRequest payload = new PackSimulationRequest
            {
                packId = packId,
                attunement = attunement,
                count = count
            };
            yield return PostJson("/api/packs/simulate", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public IEnumerator ResetTestPackInventory(string playerId, System.Action<PackResetResponse> onSuccess, System.Action<string> onError)
        {
            PackPlayerRequest payload = new PackPlayerRequest { playerId = playerId };
            yield return PostJson("/api/packs/reset-test-inventory", payload, onSuccess, onError, PackRequestTimeoutSeconds);
        }

        public void OnFetchSuccess(string json)
        {
            pendingGetComplete = true;
            Action<string> callback = pendingGetSuccess;
            ClearPendingGet();
            callback?.Invoke(json);
        }

        public void OnFetchError(string error)
        {
            pendingGetComplete = true;
            Action<string> callback = pendingGetError;
            ClearPendingGet();
            callback?.Invoke(error);
        }

        private IEnumerator Get(string path, System.Action<string> onSuccess, System.Action<string> onError, int timeoutSeconds = RequestTimeoutSeconds)
        {
            string url = BuildUrl(path);

#if UNITY_WEBGL && !UNITY_EDITOR
            while (pendingGetSuccess != null || pendingGetError != null)
            {
                yield return null;
            }

            pendingGetComplete = false;
            pendingGetSuccess = onSuccess;
            pendingGetError = onError;
            AppreciatorsFetchGet(url, gameObject.name, nameof(OnFetchSuccess), nameof(OnFetchError));

            float timeoutAt = Time.realtimeSinceStartup + timeoutSeconds;
            while (!pendingGetComplete && Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            if (!pendingGetComplete)
            {
                pendingGetComplete = true;
                Action<string> callback = pendingGetError;
                ClearPendingGet();
                callback?.Invoke($"Timed out ({url})");
            }
#else
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(request.downloadHandler.text);
                }
                else
                {
                    onError?.Invoke(DescribeFailure(url, request));
                }
            }
#endif
        }

        private IEnumerator GetJson<T>(string path, System.Action<T> onSuccess, System.Action<string> onError, int timeoutSeconds = RequestTimeoutSeconds)
        {
            yield return Get(path, json =>
            {
                TryParseJson(path, json, onSuccess, onError);
            }, onError, timeoutSeconds);
        }

        private IEnumerator PostJson<T>(string path, object payload, System.Action<T> onSuccess, System.Action<string> onError, int timeoutSeconds = RequestTimeoutSeconds)
        {
            if (payload == null)
            {
                string payloadError = $"Cannot POST a null JSON payload to '{path}'.";
                Debug.LogError($"[BackendApi] {payloadError}");
                onError?.Invoke(payloadError);
                yield break;
            }

            string url = BuildUrl(path);
            string json = JsonUtility.ToJson(payload);
            byte[] body = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.timeout = timeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    TryParseJson(path, request.downloadHandler.text, onSuccess, onError);
                }
                else
                {
                    onError?.Invoke(DescribeFailure(url, request));
                }
            }
        }

        private static void TryParseJson<T>(string path, string json, Action<T> onSuccess, Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                string emptyError = $"Backend returned an empty JSON response for '{path}'.";
                Debug.LogError($"[BackendApi] {emptyError}");
                onError?.Invoke(emptyError);
                return;
            }

            try
            {
                T parsed = JsonUtility.FromJson<T>(json);
                if (ReferenceEquals(parsed, null))
                {
                    string nullError = $"Backend JSON for '{path}' parsed to a null {typeof(T).Name}.";
                    Debug.LogError($"[BackendApi] {nullError}");
                    onError?.Invoke(nullError);
                    return;
                }

                onSuccess?.Invoke(parsed);
            }
            catch (Exception exception)
            {
                string parseError = $"Could not parse backend JSON for '{path}' as {typeof(T).Name}: {exception.Message}";
                Debug.LogError($"[BackendApi] {parseError}");
                onError?.Invoke(parseError);
            }
        }

        private static string BuildUrl(string path)
        {
            return $"{AppConfig.ApiBaseUrl.TrimEnd('/')}{path}";
        }

        private static string BuildInviteQuery(string username, string[] deckIds)
        {
            string deckList = deckIds == null ? string.Empty : string.Join(",", deckIds);
            return $"username={UnityWebRequest.EscapeURL(username ?? "Guest")}&deckIds={UnityWebRequest.EscapeURL(deckList)}";
        }

        private static string DescribeFailure(string url, UnityWebRequest request)
        {
            string details = string.IsNullOrWhiteSpace(request.downloadHandler?.text)
                ? request.error
                : request.downloadHandler.text;

            return $"{details} ({url})";
        }

        private void ClearPendingGet()
        {
            pendingGetSuccess = null;
            pendingGetError = null;
        }
    }
}
