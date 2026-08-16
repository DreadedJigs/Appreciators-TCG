using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AppreciatorsTcg.Core
{
    public static class LocalSaveSystem
    {
        private const string PlayerNameKey = "appreciators.playerName";
        private const string DeckKey = "appreciators.deckIds";
        private const string NamedDecksKey = "appreciators.namedDecks.v1";
        private const string ApiBaseUrlKey = "appreciators.apiBaseUrl";
        private const string PendingMatchModeKey = "appreciators.pendingMatchMode";
        private const string PendingInviteCodeKey = "appreciators.pendingInviteCode";
        private const string PendingMatchIdKey = "appreciators.pendingMatchId";
        private const string PendingOpponentNameKey = "appreciators.pendingOpponentName";
        private const string PendingPlayerIdKey = "appreciators.pendingPlayerId";
        private const string PendingPlayerRoleKey = "appreciators.pendingPlayerRole";
        private const string PendingBossAssetImageKey = "appreciators.pendingBossAssetImage";
        private const string MockWalletAddressKey = "appreciators.mockWalletAddress";
        private const string MockWalletVerifiedKey = "appreciators.mockWalletVerified";
        private const string SelectedBossTokenIdKey = "appreciators.selectedBossTokenId";
        private const string PlayerIdKey = "appreciators.playerId";
        private const string AccountNameKey = "appreciators.accountName.v1";
        private const string PendingPackRequestIdKey = "appreciators.pendingPackRequestId";
        private const string PendingPackIdKey = "appreciators.pendingPackId";
        private const string PendingPackAttunementKey = "appreciators.pendingPackAttunement";
        private const string ThemeKey = "appreciators.theme.v1";
        private const string ThemeDefaultVersionKey = "appreciators.theme.default.version";
        private const string TutorialStepKey = "appreciators.tutorial.step.v2";
        private const string TutorialCoreKey = "appreciators.tutorial.core.v2";
        private const string TutorialCompletedKey = "appreciators.tutorial.completed.v1";
        private const string ReducedMotionKey = "appreciators.accessibility.reducedMotion.v1";

        public static void SaveTheme(AppreciatorsTheme theme)
        {
            PlayerPrefs.SetString(ThemeKey, theme.ToString());
            PlayerPrefs.Save();
        }

        public static AppreciatorsTheme LoadTheme()
        {
            return Enum.TryParse(PlayerPrefs.GetString(ThemeKey, AppreciatorsTheme.Dark.ToString()), true, out AppreciatorsTheme theme)
                ? theme
                : AppreciatorsTheme.Dark;
        }

        public static void EnsureDarkModeDefault()
        {
            // Migrate existing alpha installs once so every mode enters this
            // release in dark mode. Later player toggles remain persistent.
            const int currentDefaultVersion = 1;
            if (PlayerPrefs.GetInt(ThemeDefaultVersionKey, 0) >= currentDefaultVersion)
            {
                return;
            }

            PlayerPrefs.SetString(ThemeKey, AppreciatorsTheme.Dark.ToString());
            PlayerPrefs.SetInt(ThemeDefaultVersionKey, currentDefaultVersion);
            PlayerPrefs.Save();
        }

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

        public static bool HasCreatedAccount() => PlayerPrefs.HasKey(AccountNameKey) && !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(AccountNameKey));

        public static string LoadOrCreatePlayerId()
        {
            string playerId = PlayerPrefs.GetString(PlayerIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                return playerId;
            }

            playerId = $"player_{Guid.NewGuid():N}";
            PlayerPrefs.SetString(PlayerIdKey, playerId);
            PlayerPrefs.Save();
            return playerId;
        }

        public static string SaveAccountIdentity(string playerName)
        {
            string safeName = string.IsNullOrWhiteSpace(playerName) ? "Guest" : playerName.Trim();
            string playerId = CreateStablePlayerId(safeName);
            PlayerPrefs.SetString(PlayerNameKey, safeName);
            PlayerPrefs.SetString(AccountNameKey, safeName.ToLowerInvariant());
            PlayerPrefs.SetString(PlayerIdKey, playerId);
            PlayerPrefs.Save();
            return playerId;
        }

        public static void SavePlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            PlayerPrefs.SetString(PlayerIdKey, playerId.Trim());
            PlayerPrefs.Save();
        }

        public static string CreateStablePlayerId(string playerName)
        {
            string normalized = string.IsNullOrWhiteSpace(playerName)
                ? "guest"
                : new string(playerName.Trim().ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
            if (string.IsNullOrWhiteSpace(normalized))
            {
                normalized = "guest";
            }

            // Deterministic alpha identity lets the same named player restore the
            // server-backed inventory from another browser or network.
            ulong hash = 14695981039346656037UL;
            foreach (char character in normalized)
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
            return $"account_{hash:x16}";
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

        public static void SaveDeckCollection(PlayerDeckCollection collection)
        {
            if (collection == null)
            {
                Debug.LogError("Cannot save a null player deck collection.");
                return;
            }

            PlayerPrefs.SetString(NamedDecksKey, JsonUtility.ToJson(collection));
            PlayerPrefs.Save();
        }

        public static PlayerDeckCollection LoadDeckCollection()
        {
            string json = PlayerPrefs.GetString(NamedDecksKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PlayerDeckCollection();
            }

            try
            {
                PlayerDeckCollection collection = JsonUtility.FromJson<PlayerDeckCollection>(json);
                if (collection == null)
                {
                    Debug.LogError("Saved named deck data was empty. Starting with a clean deck collection.");
                    return new PlayerDeckCollection();
                }

                collection.decks = collection.decks ?? new List<PlayerDeckProfile>();
                return collection;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not load named deck data: {exception.Message}");
                return new PlayerDeckCollection();
            }
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

        public static void SaveSelectedBossTokenId(string tokenId)
        {
            PlayerPrefs.SetString(SelectedBossTokenIdKey, tokenId ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static string LoadSelectedBossTokenId()
        {
            return PlayerPrefs.GetString(SelectedBossTokenIdKey, string.Empty);
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

        public static void SavePendingBossAssetImage(string imageUrl)
        {
            PlayerPrefs.SetString(PendingBossAssetImageKey, imageUrl ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static string LoadPendingBossAssetImage()
        {
            return PlayerPrefs.GetString(PendingBossAssetImageKey, string.Empty);
        }

        public static void ClearPendingMatchContext()
        {
            PlayerPrefs.DeleteKey(PendingMatchModeKey);
            PlayerPrefs.DeleteKey(PendingInviteCodeKey);
            PlayerPrefs.DeleteKey(PendingMatchIdKey);
            PlayerPrefs.DeleteKey(PendingOpponentNameKey);
            PlayerPrefs.DeleteKey(PendingPlayerIdKey);
            PlayerPrefs.DeleteKey(PendingPlayerRoleKey);
            PlayerPrefs.DeleteKey(PendingBossAssetImageKey);
            PlayerPrefs.Save();
        }

        public static void SavePendingPackOpen(string requestId, string packId, string attunement)
        {
            PlayerPrefs.SetString(PendingPackRequestIdKey, requestId ?? string.Empty);
            PlayerPrefs.SetString(PendingPackIdKey, packId ?? string.Empty);
            PlayerPrefs.SetString(PendingPackAttunementKey, attunement ?? string.Empty);
            PlayerPrefs.Save();
        }

        public static bool TryLoadPendingPackOpen(out string requestId, out string packId, out string attunement)
        {
            requestId = PlayerPrefs.GetString(PendingPackRequestIdKey, string.Empty);
            packId = PlayerPrefs.GetString(PendingPackIdKey, string.Empty);
            attunement = PlayerPrefs.GetString(PendingPackAttunementKey, string.Empty);
            return !string.IsNullOrWhiteSpace(requestId) && !string.IsNullOrWhiteSpace(packId) && !string.IsNullOrWhiteSpace(attunement);
        }

        public static void ClearPendingPackOpen()
        {
            PlayerPrefs.DeleteKey(PendingPackRequestIdKey);
            PlayerPrefs.DeleteKey(PendingPackIdKey);
            PlayerPrefs.DeleteKey(PendingPackAttunementKey);
            PlayerPrefs.Save();
        }

        public static void SaveTutorialProgress(int step, bool coreDemonstrated)
        {
            PlayerPrefs.SetInt(TutorialStepKey, Math.Max(0, step));
            PlayerPrefs.SetInt(TutorialCoreKey, coreDemonstrated ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static int LoadTutorialStep() => Math.Max(0, PlayerPrefs.GetInt(TutorialStepKey, 0));

        public static bool LoadTutorialCoreDemonstrated() => PlayerPrefs.GetInt(TutorialCoreKey, 0) == 1;

        public static bool HasCompletedTutorial()
        {
            // Version 2 stored the final tutorial step but no explicit completion flag.
            return PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1 || LoadTutorialStep() >= 16;
        }

        public static void MarkTutorialCompleted()
        {
            PlayerPrefs.SetInt(TutorialCompletedKey, 1);
            PlayerPrefs.Save();
        }

        public static void ApplyAccountProgress(AppreciatorsTcg.Data.PlayerProgress progress)
        {
            if (progress != null && progress.tutorialCompleted)
            {
                MarkTutorialCompleted();
            }
        }

        public static void ResetTutorialProgress()
        {
            PlayerPrefs.DeleteKey(TutorialStepKey);
            PlayerPrefs.DeleteKey(TutorialCoreKey);
            PlayerPrefs.DeleteKey(TutorialCompletedKey);
            PlayerPrefs.Save();
        }

        public static bool LoadReducedMotion() => PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;

        public static void SaveReducedMotion(bool enabled)
        {
            PlayerPrefs.SetInt(ReducedMotionKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
