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

        private IEnumerator Get(string path, System.Action<string> onSuccess, System.Action<string> onError)
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

            float timeoutAt = Time.realtimeSinceStartup + RequestTimeoutSeconds;
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
                request.timeout = RequestTimeoutSeconds;
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

        private IEnumerator GetJson<T>(string path, System.Action<T> onSuccess, System.Action<string> onError)
        {
            yield return Get(path, json =>
            {
                T parsed = JsonUtility.FromJson<T>(json);
                onSuccess?.Invoke(parsed);
            }, onError);
        }

        private IEnumerator PostJson<T>(string path, object payload, System.Action<T> onSuccess, System.Action<string> onError)
        {
            string url = BuildUrl(path);
            string json = JsonUtility.ToJson(payload);
            byte[] body = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                request.timeout = RequestTimeoutSeconds;
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    T parsed = JsonUtility.FromJson<T>(request.downloadHandler.text);
                    onSuccess?.Invoke(parsed);
                }
                else
                {
                    onError?.Invoke(DescribeFailure(url, request));
                }
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
