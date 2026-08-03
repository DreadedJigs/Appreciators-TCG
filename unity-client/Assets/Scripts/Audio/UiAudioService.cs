using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace AppreciatorsTcg.Audio
{
    public static class UiAudioService
    {
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static AudioSource uiSource;
        private static AudioSource actionSource;
        private static AudioSource bannerSource;
        private static bool uiLogged;
        private static bool actionLogged;
        private static bool bannerLogged;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AppreciatorsResumeWebAudio();
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (uiSource != null) return;

            GameObject host = new GameObject("AppreciatorsAudioRuntime");
            Object.DontDestroyOnLoad(host);
            AudioRuntimeGuard.EnsureListener(host);
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat("appreciators_master_volume", 1f));

            uiSource = CreateSource(host);
            actionSource = CreateSource(host);
            bannerSource = CreateSource(host);

            Load("draw", "Pixabay/card_draw");
            Load("place", "Pixabay/card_place");
            Load("whoosh", "Pixabay/attack_whoosh");
            Load("impact", "Pixabay/combat_impact");
            Load("discard", "Freesound/carddrop_447013");
            Load("reward", "Pixabay/reward_chime");
        }

        public static void PlayButton()
        {
            Initialize();
            Play(uiSource, "place", 0.46f, 1.08f);
            if (!uiLogged)
            {
                uiLogged = true;
                Debug.Log("[UiAudio] Universal button cue active.");
            }
        }

        public static void PlayPhaseSweep(string label)
        {
            Initialize();
            // Phase transitions sit beneath card and combat cues in the mix.
            // 0.4824 is exactly 33% below the previous 0.72 level.
            Play(bannerSource, "whoosh", 0.4824f, 0.92f);
            if (!bannerLogged)
            {
                bannerLogged = true;
                Debug.Log($"[UiAudio] Flying phase-banner cue active: {label} at 0.4824.");
            }
        }

        public static void PlayCardDraw()
        {
            Initialize();
            Play(actionSource, "draw", 0.78f, Random.Range(0.98f, 1.07f));
            LogActionOnce("card draw");
        }

        public static void PlayDiscard()
        {
            Initialize();
            Play(actionSource, "discard", 0.72f, 1f);
            LogActionOnce("discard");
        }

        public static void PlayReward()
        {
            Initialize();
            Play(actionSource, "reward", 0.82f, 1f);
            LogActionOnce("reward");
        }

        public static void PlayCancel()
        {
            Initialize();
            Play(actionSource, "draw", 0.54f, 0.84f);
            LogActionOnce("cancel / return");
        }

        public static void PlayInspect()
        {
            Initialize();
            Play(actionSource, "draw", 0.58f, 0.92f);
            LogActionOnce("card inspection");
        }

        private static AudioSource CreateSource(GameObject host)
        {
            AudioSource source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 0;
            source.volume = 1f;
            return source;
        }

        private static void Load(string key, string resourceName)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Audio/Battle/{resourceName}");
            if (clip == null)
            {
                Debug.LogError($"[UiAudio] Sound '{resourceName}' is missing from Resources/Audio/Battle.");
                return;
            }

            Clips[key] = clip;
        }

        private static void Play(AudioSource source, string key, float volume, float pitch)
        {
            if (source == null || !Clips.TryGetValue(key, out AudioClip clip) || clip == null) return;
            ResumeWebAudioIfNeeded();
            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }

        private static void ResumeWebAudioIfNeeded()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                AppreciatorsResumeWebAudio();
            }
            catch
            {
                // The WebGL page also installs a gesture-level fallback. Audio
                // playback should never interrupt game input if a browser omits
                // its Web Audio context or changes the exposed Unity internals.
            }
#endif
        }

        private static void LogActionOnce(string action)
        {
            if (actionLogged) return;
            actionLogged = true;
            Debug.Log($"[UiAudio] Gameplay action cues active: {action}.");
        }
    }
}
