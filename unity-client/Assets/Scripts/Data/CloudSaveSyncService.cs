using System.Collections;
using AppreciatorsTcg.Core;
using UnityEngine;

namespace AppreciatorsTcg.Data
{
    /// <summary>
    /// Debounced account-save synchronizer. Credentials stay in BackendApiClient's
    /// process memory; this component persists only player settings and progression
    /// that LocalSaveSystem explicitly allows in a cloud snapshot.
    /// </summary>
    public sealed class CloudSaveSyncService : MonoBehaviour
    {
        private const float PollIntervalSeconds = 6f;
        private static CloudSaveSyncService instance;

        private BackendApiClient apiClient;
        private string lastUploadedFingerprint = string.Empty;
        private bool saveInFlight;
        private bool conflictDetected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            GameObject host = new GameObject("CloudSaveSyncService");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<CloudSaveSyncService>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            apiClient = gameObject.AddComponent<BackendApiClient>();
            StartCoroutine(SynchronizeLoop());
        }

        /// <summary>
        /// Called after a successful restore or upload so that receiving a cloud
        /// snapshot never causes an unnecessary write-back on the next tick.
        /// </summary>
        public static void MarkSynchronized()
        {
            if (instance == null) return;
            instance.lastUploadedFingerprint = Fingerprint(LocalSaveSystem.CaptureCloudSave());
            instance.conflictDetected = false;
        }

        private IEnumerator SynchronizeLoop()
        {
            WaitForSecondsRealtime interval = new WaitForSecondsRealtime(PollIntervalSeconds);
            while (true)
            {
                yield return interval;
                if (!BackendApiClient.HasSecureSession || saveInFlight || conflictDetected)
                {
                    continue;
                }

                CloudSaveSnapshot snapshot = LocalSaveSystem.CaptureCloudSave();
                string fingerprint = Fingerprint(snapshot);
                if (fingerprint == lastUploadedFingerprint)
                {
                    continue;
                }

                saveInFlight = true;
                CloudSaveResponse response = null;
                string error = null;
                CloudSaveRequest request = new CloudSaveRequest
                {
                    expectedVersion = LocalSaveSystem.LoadCloudSaveVersion(),
                    snapshot = snapshot
                };
                yield return apiClient.SaveCloudSave(request, value => response = value, value => error = value);
                saveInFlight = false;

                if (response?.success == true)
                {
                    LocalSaveSystem.SaveCloudSaveVersion(response.version);
                    lastUploadedFingerprint = fingerprint;
                    continue;
                }

                // Never silently overwrite a newer save from another device. The
                // next secure sign-in restores the authoritative snapshot; until
                // then local play remains intact and writes are paused.
                if (!string.IsNullOrWhiteSpace(error) && error.Contains("CLOUD_SAVE_CONFLICT"))
                {
                    conflictDetected = true;
                    Debug.LogWarning("[CloudSave] A newer save exists on another device. Sign in again to restore it before saving further changes.");
                }
                else if (!string.IsNullOrWhiteSpace(error))
                {
                    Debug.LogWarning($"[CloudSave] Sync deferred: {error}");
                }
            }
        }

        private static string Fingerprint(CloudSaveSnapshot snapshot)
        {
            return JsonUtility.ToJson(snapshot ?? new CloudSaveSnapshot());
        }
    }
}
