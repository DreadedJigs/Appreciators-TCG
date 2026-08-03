using System;
using UnityEngine;

namespace AppreciatorsTcg.Data
{
    [Serializable]
    public sealed class CardMetaSeason
    {
        public int season;
        public string name;
        public int cards;
        public int common;
        public int uncommon;
        public int rare;
        public int epic;
        public int legendary;
        public int crown;
        public int crownTokenId;
    }

    [Serializable]
    public sealed class CardMetaArchetype
    {
        public string archetype;
        public string domain;
        public string pillar;
        public string corePlan;
    }

    [Serializable]
    public sealed class CardMetaManifestDocument
    {
        public string version;
        public string sourceWorkbook;
        public string contract;
        public string chain;
        public int totalCards;
        public int totalAbilities;
        public int totalSeasons;
        public int crownCards;
        public string metadataStatus;
        public CardMetaSeason[] seasons;
        public CardMetaArchetype[] archetypes;
    }

    public static class CardMetaManifest
    {
        private static CardMetaManifestDocument cached;

        public static CardMetaManifestDocument Load()
        {
            if (cached != null)
            {
                return cached;
            }

            TextAsset asset = Resources.Load<TextAsset>("card-meta-manifest");
            if (asset == null)
            {
                Debug.LogError("Missing Resources/card-meta-manifest.json.");
                return null;
            }

            cached = JsonUtility.FromJson<CardMetaManifestDocument>(asset.text);
            return cached;
        }
    }
}
