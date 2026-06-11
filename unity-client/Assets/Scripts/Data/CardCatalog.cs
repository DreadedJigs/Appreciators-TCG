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
                "regular_body",
                "no_head_body",
                "beer_helmet",
                "devil_dog_companion",
                "ghost_companion",
                "pink_lemonade_background",
                "tropical_background",
                "blue_skin",
                "pink_skin",
                "yellow_skin",
                "unicorn_head",
                "blockchain_background"
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
