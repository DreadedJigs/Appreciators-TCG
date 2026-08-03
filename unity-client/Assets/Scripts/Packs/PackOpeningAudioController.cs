using System.Collections.Generic;
using AppreciatorsTcg.Audio;
using UnityEngine;

namespace AppreciatorsTcg.Packs
{
    public sealed class PackOpeningAudioController : MonoBehaviour
    {
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private AudioSource source;
        private bool firstCueLogged;

        private void Awake()
        {
            AudioRuntimeGuard.EnsureListener(gameObject);
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 0;
            source.volume = 1f;

            Load("pack-start", "Pixabay/attack_whoosh");
            Load("seal-break", "Pixabay/combat_impact");
            Load("card-reveal", "Pixabay/card_draw");
            Load("rare-reveal", "Pixabay/reward_chime");
            Load("duplicate", "Pixabay/card_place");
            Load("summary", "Pixabay/reward_chime");
        }

        public void PlayPackStartSfx()
        {
            Play("pack-start", 0.58f, 0.94f);
        }

        public void PlaySealBreakSfx()
        {
            Play("seal-break", 0.66f, Random.Range(0.94f, 1.07f));
        }

        public void PlayCardRevealSfx()
        {
            Play("card-reveal", 0.48f, Random.Range(0.98f, 1.10f));
        }

        public void PlayRareRevealSfx()
        {
            Play("rare-reveal", 0.68f, 1.02f);
        }

        public void PlayDuplicateSfx()
        {
            Play("duplicate", 0.56f, 1.06f);
        }

        public void PlaySummarySfx()
        {
            Play("summary", 0.62f, 1f);
        }

        private void Load(string key, string resourceName)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Audio/Battle/{resourceName}");
            if (clip == null)
            {
                Debug.LogError($"[PackAudio] Sound '{resourceName}' is missing from Resources/Audio/Battle.");
                return;
            }

            clips[key] = clip;
        }

        private void Play(string key, float volume, float pitch)
        {
            if (source == null || !clips.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                Debug.LogWarning($"[PackAudio] Cannot play '{key}' because its audio source or clip is unavailable.");
                return;
            }

            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
            if (!firstCueLogged)
            {
                firstCueLogged = true;
                Debug.Log($"[PackAudio] Playback active: {key} / {clip.name} at {volume:0.00}.");
            }
        }
    }
}
