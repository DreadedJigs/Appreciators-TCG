using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AppreciatorsTcg.Data
{
    [Serializable]
    public class OriginalsTraitCatalogDocument
    {
        public int version;
        public string generatedAt;
        public int chainId;
        public string contractAddress;
        public string collectionName;
        public string symbol;
        public int totalSupply;
        public int importedTokenCount;
        public int excludedTokenCount;
        public List<OriginalsGameplayTraitMapping> approvedGameplayTraits;
        public List<OriginalsTraitTypeSummary> traitTypes;
    }

    [Serializable]
    public class OriginalsGameplayTraitMapping
    {
        public string gameplayId;
        public string displayName;
        public string gameplayGroup;
        public string status;
        public List<string> sourceTraitTypes;
        public List<string> aliases;
        public int tokenCount;
        public List<int> sampleTokenIds;
        public List<OriginalsMatchedTraitValue> matchedValues;

        public bool IsOnChainMatch => string.Equals(status, "matched", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public class OriginalsMatchedTraitValue
    {
        public string traitType;
        public string value;
        public int tokenCount;
    }

    [Serializable]
    public class OriginalsTraitTypeSummary
    {
        public string traitType;
        public int tokenCount;
        public int uniqueValueCount;
        public List<OriginalsTraitValueSummary> values;
    }

    [Serializable]
    public class OriginalsTraitValueSummary
    {
        public string value;
        public int count;
        public List<int> sampleTokenIds;
    }

    public static class OriginalsMetadataCatalog
    {
        private const string ResourceName = "Metadata/appreciators-originals-traits";
        private static OriginalsTraitCatalogDocument catalog;
        private static Dictionary<string, OriginalsGameplayTraitMapping> mappingsByGameplayId;

        public static OriginalsTraitCatalogDocument Catalog
        {
            get
            {
                EnsureLoaded();
                return catalog;
            }
        }

        public static OriginalsGameplayTraitMapping GetGameplayTrait(string gameplayId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(gameplayId))
            {
                return null;
            }

            mappingsByGameplayId.TryGetValue(gameplayId, out OriginalsGameplayTraitMapping mapping);
            return mapping;
        }

        private static void EnsureLoaded()
        {
            if (catalog != null)
            {
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
            {
                Debug.LogError($"Missing Originals metadata catalog at Resources/{ResourceName}.json. Run scripts/import-apechain-originals.mjs.");
                UseEmptyCatalog();
                return;
            }

            try
            {
                catalog = JsonUtility.FromJson<OriginalsTraitCatalogDocument>(asset.text);
            }
            catch (Exception exception)
            {
                Debug.LogError($"Could not parse Originals metadata catalog: {exception.Message}");
                UseEmptyCatalog();
                return;
            }

            if (catalog == null || catalog.approvedGameplayTraits == null)
            {
                Debug.LogError("Originals metadata catalog is missing approvedGameplayTraits.");
                UseEmptyCatalog();
                return;
            }

            mappingsByGameplayId = catalog.approvedGameplayTraits
                .Where(mapping => mapping != null && !string.IsNullOrWhiteSpace(mapping.gameplayId))
                .GroupBy(mapping => mapping.gameplayId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private static void UseEmptyCatalog()
        {
            catalog = new OriginalsTraitCatalogDocument
            {
                approvedGameplayTraits = new List<OriginalsGameplayTraitMapping>(),
                traitTypes = new List<OriginalsTraitTypeSummary>()
            };
            mappingsByGameplayId = new Dictionary<string, OriginalsGameplayTraitMapping>();
        }
    }
}
