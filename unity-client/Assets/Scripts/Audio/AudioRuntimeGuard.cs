using UnityEngine;

namespace AppreciatorsTcg.Audio
{
    public static class AudioRuntimeGuard
    {
        public static AudioListener EnsureListener(GameObject host)
        {
            AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
            if (listener == null && host != null)
            {
                listener = host.AddComponent<AudioListener>();
                Debug.Log("[Audio] Runtime listener created for the UI-only scene.");
            }

            if (listener != null)
            {
                listener.enabled = true;
            }

            return listener;
        }
    }
}
