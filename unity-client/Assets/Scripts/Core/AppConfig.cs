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
                string runtimeOverride = ResolveRuntimeApiOverride();
                if (!string.IsNullOrWhiteSpace(runtimeOverride))
                {
                    return runtimeOverride;
                }

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

            if (string.IsNullOrWhiteSpace(Application.absoluteURL))
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

                // The hosted API remains the default even when a tester opens the
                // WebGL build from localhost or a LAN address. This lets invite and
                // account state work between players on different public networks.
                // Add ?localBackend=1 when explicitly testing the local Node server.
                if (IsLocalDevelopmentHost(pageUri.Host) && IsLocalhostUrl(configuredUrl))
                {
                    return $"{pageUri.Scheme}://{pageUri.Host}:{LocalBackendPort}";
                }

                return configuredUrl;
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

        private static bool IsLocalDevelopmentHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            string normalized = host.Trim('[', ']').ToLowerInvariant();
            if (normalized == "localhost" || normalized == "127.0.0.1" || normalized == "::1" ||
                normalized.StartsWith("192.168.") || normalized.StartsWith("10."))
            {
                return true;
            }

            string[] parts = normalized.Split('.');
            return parts.Length == 4 && parts[0] == "172" && int.TryParse(parts[1], out int secondOctet) &&
                secondOctet >= 16 && secondOctet <= 31;
        }

        private static string ResolveRuntimeApiOverride()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer || string.IsNullOrWhiteSpace(Application.absoluteURL))
            {
                return string.Empty;
            }

            try
            {
                Uri pageUri = new Uri(Application.absoluteURL);
                string query = pageUri.Query.TrimStart('?');
                foreach (string pair in query.Split('&'))
                {
                    string[] parts = pair.Split(new[] { '=' }, 2);
                    string key = Uri.UnescapeDataString(parts[0]);
                    string value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
                    if (string.Equals(key, "localBackend", StringComparison.OrdinalIgnoreCase) && value == "1")
                    {
                        return $"{pageUri.Scheme}://{pageUri.Host}:{LocalBackendPort}";
                    }

                    if (string.Equals(key, "apiBaseUrl", StringComparison.OrdinalIgnoreCase) &&
                        Uri.TryCreate(value, UriKind.Absolute, out Uri apiUri) &&
                        (apiUri.Scheme == Uri.UriSchemeHttp || apiUri.Scheme == Uri.UriSchemeHttps))
                    {
                        return value.TrimEnd('/');
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AppConfig] Could not parse WebGL API override: {exception.Message}");
            }

            return string.Empty;
        }
    }

    [Serializable]
    public class AppConfigFile
    {
        public string apiBaseUrl = AppConfig.DefaultApiBaseUrl;
    }
}
