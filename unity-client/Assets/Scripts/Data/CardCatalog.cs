using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using UnityEngine;

namespace AppreciatorsTcg.Data
{
    public static class CardCatalog
    {
        private const string ResourceName = "prototype-cards";
        private static List<CardDefinition> allCards;
        private static Dictionary<string, CardDefinition> cardsById;

        public static IReadOnlyList<CardDefinition> AllCards
        {
            get
            {
                EnsureLoaded();
                return allCards;
            }
        }

        public static CardDefinition GetCard(string id)
        {
            EnsureLoaded();
            cardsById.TryGetValue(id, out CardDefinition card);
            return card;
        }

        public static List<CardDefinition> GetCards(IEnumerable<string> ids)
        {
            EnsureLoaded();
            return ids.Select(GetCard).Where(card => card != null).ToList();
        }

        public static List<string> StarterDeckIds()
        {
            return new List<string>
            {
                "original_ape",
                "blue_face_original",
                "gallery_original",
                "chain_original",
                "community_original",
                "rally_original",
                "kaizo",
                "spike",
                "community_pup",
                "gold_x_emblem",
                "dread_trait",
                "art_gallery"
            };
        }

        private static void EnsureLoaded()
        {
            if (allCards != null)
            {
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
            {
                Debug.LogError($"Missing card data at Resources/{ResourceName}.json");
                allCards = new List<CardDefinition>();
                cardsById = new Dictionary<string, CardDefinition>();
                return;
            }

            CardCollection collection = JsonUtility.FromJson<CardCollection>(asset.text);
            allCards = collection?.cards ?? new List<CardDefinition>();
            cardsById = allCards
                .Where(card => !string.IsNullOrWhiteSpace(card.id))
                .GroupBy(card => card.id)
                .ToDictionary(group => group.Key, group => group.First());
        }
    }
}
