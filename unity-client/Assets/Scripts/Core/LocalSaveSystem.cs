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
    }
}
