using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AppreciatorsTcg.Core
{
    public static class LocalSaveSystem
    {
        private const string PlayerNameKey = "appreciators.playerName";
        private const string DeckKey = "appreciators.deckIds";
        private const string ApiBaseUrlKey = "appreciators.apiBaseUrl";
        private const string PendingMatchModeKey = "appreciators.pendingMatchMode";
        private const string PendingInviteCodeKey = "appreciators.pendingInviteCode";
        private const string PendingMatchIdKey = "appreciators.pendingMatchId";
        private const string PendingOpponentNameKey = "appreciators.pendingOpponentName";
        private const string PendingPlayerIdKey = "appreciators.pendingPlayerId";
        private const string PendingPlayerRoleKey = "appreciators.pendingPlayerRole";
        private const string MockWalletAddressKey = "appreciators.mockWalletAddress";
        private const string MockWalletVerifiedKey = "appreciators.mockWalletVerified";

        public static void SavePlayerName(string playerName)
        {
            string safeName = string.IsNullOrWhiteSpace(playerName) ? "Guest" : playerName.Trim();
            PlayerPrefs.SetString(PlayerNameKey, safeName);
            PlayerPrefs.Save();
        }

        public static string LoadPlayerName()
        {
            return PlayerPrefs.GetString(PlayerNameKey, "Guest");
        }

        public static bool HasSavedDeck()
        {
            return PlayerPrefs.HasKey(DeckKey) && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(DeckKey));
        }

        public static void SaveDeckIds(IEnumerable<string> deckIds)
        {
            PlayerPrefs.SetString(DeckKey, string.Join("|", deckIds));
            PlayerPrefs.Save();
        }

        public static List<string> LoadDeckIds()
        {
            string saved = PlayerPrefs.GetString(DeckKey, string.Empty);
            if (string.IsNullOrWhiteSpace(saved))
            {
                return new List<string>();
            }

            return saved.Split('|').Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        }

        public static void SaveApiBaseUrl(string apiBaseUrl)
        {
            PlayerPrefs.SetString(ApiBaseUrlKey, apiBaseUrl?.Trim() ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static string LoadApiBaseUrl()
        {
            return PlayerPrefs.GetString(ApiBaseUrlKey, string.Empty);
        }

        public static void SaveMockWallet(string walletAddress, bool verified)
        {
            PlayerPrefs.SetString(MockWalletAddressKey, walletAddress?.Trim() ?? string.Empty);
            PlayerPrefs.SetInt(MockWalletVerifiedKey, verified ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static string LoadMockWalletAddress()
        {
            return PlayerPrefs.GetString(MockWalletAddressKey, string.Empty);
        }

        public static bool LoadMockWalletVerified()
        {
            return PlayerPrefs.GetInt(MockWalletVerifiedKey, 0) == 1;
        }

        public static void ClearMockWallet()
        {
            PlayerPrefs.DeleteKey(MockWalletAddressKey);
            PlayerPrefs.DeleteKey(MockWalletVerifiedKey);
            PlayerPrefs.Save();
        }

        public static void SavePendingMatchContext(string mode, string inviteCode, string matchId, string opponentName, string playerId, string playerRole)
        {
            PlayerPrefs.SetString(PendingMatchModeKey, mode ?? string.Empty);
            PlayerPrefs.SetString(PendingInviteCodeKey, inviteCode ?? string.Empty);
            PlayerPrefs.SetString(PendingMatchIdKey, matchId ?? string.Empty);
            PlayerPrefs.SetString(PendingOpponentNameKey, opponentName ?? string.Empty);
            PlayerPrefs.SetString(PendingPlayerIdKey, playerId ?? string.Empty);
            PlayerPrefs.SetString(PendingPlayerRoleKey, playerRole ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static string LoadPendingMatchMode()
        {
            return PlayerPrefs.GetString(PendingMatchModeKey, string.Empty);
        }

        public static string LoadPendingInviteCode()
        {
            return PlayerPrefs.GetString(PendingInviteCodeKey, string.Empty);
        }

        public static string LoadPendingMatchId()
        {
            return PlayerPrefs.GetString(PendingMatchIdKey, string.Empty);
        }

        public static string LoadPendingOpponentName()
        {
            return PlayerPrefs.GetString(PendingOpponentNameKey, string.Empty);
        }

        public static string LoadPendingPlayerId()
        {
            return PlayerPrefs.GetString(PendingPlayerIdKey, string.Empty);
        }

        public static string LoadPendingPlayerRole()
        {
            return PlayerPrefs.GetString(PendingPlayerRoleKey, string.Empty);
        }

        public static void ClearPendingMatchContext()
        {
            PlayerPrefs.DeleteKey(PendingMatchModeKey);
            PlayerPrefs.DeleteKey(PendingInviteCodeKey);
            PlayerPrefs.DeleteKey(PendingMatchIdKey);
            PlayerPrefs.DeleteKey(PendingOpponentNameKey);
            PlayerPrefs.DeleteKey(PendingPlayerIdKey);
            PlayerPrefs.DeleteKey(PendingPlayerRoleKey);
            PlayerPrefs.Save();
        }
    }
}
