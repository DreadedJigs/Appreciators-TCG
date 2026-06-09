using System;
using UnityEngine;

namespace AppreciatorsTcg.Core
{
    public static class AppConfig
    {
        public const string DefaultApiBaseUrl = "http://localhost:3001";
        private static string configuredDefault;

        public static string ApiBaseUrl
        {
            get
            {
                string saved = LocalSaveSystem.LoadApiBaseUrl();
                return string.IsNullOrWhiteSpace(saved) ? ConfiguredDefaultApiBaseUrl : saved;
            }
        }

        private static string ConfiguredDefaultApiBaseUrl
        {
            get
            {
                if (configuredDefault != null)
                {
                    return configuredDefault;
                }

                TextAsset config = Resources.Load<TextAsset>("app-config");
                if (config != null)
                {
                    AppConfigFile parsed = JsonUtility.FromJson<AppConfigFile>(config.text);
                    configuredDefault = string.IsNullOrWhiteSpace(parsed?.apiBaseUrl) ? DefaultApiBaseUrl : parsed.apiBaseUrl;
                }
                else
                {
                    configuredDefault = DefaultApiBaseUrl;
                }

                return configuredDefault;
            }
        }
    }

    [Serializable]
    public class AppConfigFile
    {
        public string apiBaseUrl = AppConfig.DefaultApiBaseUrl;
    }
}
