using System;
using UnityEngine;

namespace AppreciatorsTcg.Core
{
    public static class AppConfig
    {
        public const string DefaultApiBaseUrl = "http://localhost:3001";
        private const int LocalBackendPort = 3001;
        private static string configuredDefault;

        public static string ApiBaseUrl
        {
            get
            {
                string saved = LocalSaveSystem.LoadApiBaseUrl();
                return string.IsNullOrWhiteSpace(saved) ? ConfiguredDefaultApiBaseUrl : ResolveWebGlLanDefault(saved);
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

                configuredDefault = ResolveWebGlLanDefault(configuredDefault);
                return configuredDefault;
            }
        }

        private static string ResolveWebGlLanDefault(string configuredUrl)
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                return configuredUrl;
            }

            if (!IsLocalhostUrl(configuredUrl) || string.IsNullOrWhiteSpace(Application.absoluteURL))
            {
                return configuredUrl;
            }

            try
            {
                Uri pageUri = new Uri(Application.absoluteURL);
                if (string.IsNullOrWhiteSpace(pageUri.Host))
                {
                    return configuredUrl;
                }

                return $"{pageUri.Scheme}://{pageUri.Host}:{LocalBackendPort}";
            }
            catch
            {
                return configuredUrl;
            }
        }

        private static bool IsLocalhostUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return true;
            }

            try
            {
                Uri uri = new Uri(url);
                return uri.Host == "localhost" || uri.Host == "127.0.0.1" || uri.Host == "::1";
            }
            catch
            {
                return false;
            }
        }
    }

    [Serializable]
    public class AppConfigFile
    {
        public string apiBaseUrl = AppConfig.DefaultApiBaseUrl;
    }
}
