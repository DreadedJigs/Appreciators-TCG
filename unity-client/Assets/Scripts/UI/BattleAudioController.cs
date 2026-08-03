using System.Collections.Generic;
using AppreciatorsTcg.Audio;
using UnityEngine;

namespace AppreciatorsTcg.UI
{
    public sealed class BattleAudioController : MonoBehaviour
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

            Load("select", "Pixabay/card_draw");
            Load("invalid", "Pixabay/combat_impact");
            Load("place", "Pixabay/card_place");
            Load("attack", "Freesound/quick_sword_draw_100618");
            Load("impact", "Pixabay/combat_impact");
            Load("defeat", "Pixabay/combat_impact");
            Load("resource-gain", "Pixabay/reward_chime");
            Load("resource-spend", "Pixabay/card_draw");
            Load("shield", "Pixabay/reward_chime");
            Load("rally", "Pixabay/attack_whoosh");
            Load("end-turn", "Pixabay/card_place");
        }

        public void PlayCardSelected() => Play("select", 0.72f);
        public void PlayInvalid() => Play("invalid", 0.62f);
        public void PlayCardPlaced() => Play("place", 0.82f);
        public void PlayAttack() => Play("attack", 0.78f);
        public void PlayImpact() => Play("impact", 0.76f);
        public void PlayDefeat() => Play("defeat", 0.86f);
        public void PlayResourceGain() => Play("resource-gain", 0.78f);
        public void PlayResourceSpend() => Play("resource-spend", 0.68f);
        public void PlayShield() => Play("shield", 0.72f);
        public void PlayRally() => Play("rally", 0.74f);
        public void PlayEndTurn() => Play("end-turn", 0.76f);

        private void Load(string key, string resourceName)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Audio/Battle/{resourceName}");
            if (clip == null)
            {
                Debug.LogError($"Battle sound '{resourceName}' is missing from Resources/Audio/Battle.");
                return;
            }

            clips[key] = clip;
        }

        private void Play(string key, float volume)
        {
            if (source == null || !clips.TryGetValue(key, out AudioClip clip) || clip == null)
            {
                return;
            }

            source.PlayOneShot(clip, volume);
            if (!firstCueLogged)
            {
                firstCueLogged = true;
                Debug.Log($"[BattleAudio] Playback active: {key} / {clip.name} at {volume:0.00}.");
            }
        }
    }
}
