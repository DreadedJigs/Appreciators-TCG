using UnityEngine;

namespace AppreciatorsTcg.Audio
{
    /// <summary>
    /// Persistent, local-only music playlist.  Music is intentionally kept
    /// separate from the master SFX source so a player can tune the soundtrack
    /// without changing battle feedback.
    /// </summary>
    public static class BackgroundMusicService
    {
        private const string VolumeKey = "appreciators_music_volume";
        private const string RepeatKey = "appreciators_music_repeat";

        private static readonly string[] TrackNames = { "Starfield Funk", "Pixel Sparkle", "Luminous Wave" };
        private static readonly string[] ResourcePaths = { "Audio/Music/StarfieldFunk", "Audio/Music/PixelSparkle", "Audio/Music/LuminousWave" };

        private static AudioSource source;
        private static AudioClip[] playlist;
        private static MusicRuntime runtime;
        private static int trackIndex;
        private static float volume;
        private static bool repeat;
        private static float startedAt;
        private static bool initialized;

        public static string CurrentTrackName => TrackNames[Mathf.Clamp(trackIndex, 0, TrackNames.Length - 1)];
        public static bool IsPlaying => source != null && source.isPlaying;
        public static bool RepeatEnabled => repeat;
        public static int VolumePercent => Mathf.RoundToInt(volume * 100f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (!Application.isPlaying || initialized)
            {
                return;
            }

            initialized = true;
            playlist = new AudioClip[ResourcePaths.Length];
            for (int i = 0; i < ResourcePaths.Length; i++)
            {
                playlist[i] = Resources.Load<AudioClip>(ResourcePaths[i]);
            }

            GameObject host = new GameObject("AppreciatorsMusicRuntime");
            Object.DontDestroyOnLoad(host);
            AudioRuntimeGuard.EnsureListener(host);

            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 64;
            volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.62f));
            repeat = PlayerPrefs.GetInt(RepeatKey, 1) == 1;
            source.volume = volume;

            runtime = host.AddComponent<MusicRuntime>();
            PlayCurrent();
        }

        public static void TogglePlayPause()
        {
            Initialize();
            if (source == null || source.clip == null)
            {
                return;
            }

            if (source.isPlaying)
            {
                source.Pause();
                return;
            }

            source.UnPause();
            if (!source.isPlaying)
            {
                source.Play();
            }
            startedAt = Time.realtimeSinceStartup;
        }

        public static void Skip()
        {
            Initialize();
            if (playlist == null || playlist.Length == 0)
            {
                return;
            }

            trackIndex = (trackIndex + 1) % playlist.Length;
            PlayCurrent();
        }

        public static void ToggleRepeat()
        {
            Initialize();
            repeat = !repeat;
            PlayerPrefs.SetInt(RepeatKey, repeat ? 1 : 0);
            PlayerPrefs.Save();
            if (source != null)
            {
                source.loop = repeat;
            }
        }

        public static void CycleVolume()
        {
            Initialize();
            // 0% -> 35% -> 62% -> 82% -> 100% -> 0%
            float[] steps = { 0f, 0.35f, 0.62f, 0.82f, 1f };
            int next = 0;
            for (int i = 0; i < steps.Length; i++)
            {
                if (Mathf.Abs(volume - steps[i]) < 0.02f)
                {
                    next = (i + 1) % steps.Length;
                    break;
                }
            }

            volume = steps[next];
            if (source != null)
            {
                source.volume = volume;
            }
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.Save();
        }

        internal static void Tick()
        {
            if (source == null || source.clip == null || repeat || source.isPlaying)
            {
                return;
            }

            // Avoid treating a browser's pre-gesture audio suspension as the
            // end of a song. A completed clip will have had time to run.
            if (Time.realtimeSinceStartup - startedAt >= source.clip.length + 0.2f)
            {
                Skip();
            }
        }

        private static void PlayCurrent()
        {
            if (source == null || playlist == null || playlist.Length == 0)
            {
                return;
            }

            AudioClip clip = playlist[trackIndex];
            if (clip == null)
            {
                Debug.LogWarning($"[Music] Missing playlist clip at {ResourcePaths[trackIndex]}.");
                return;
            }

            source.Stop();
            source.clip = clip;
            source.loop = repeat;
            source.volume = volume;
            source.Play();
            startedAt = Time.realtimeSinceStartup;
        }
    }

    internal sealed class MusicRuntime : MonoBehaviour
    {
        private void Update()
        {
            BackgroundMusicService.Tick();
        }
    }
}
