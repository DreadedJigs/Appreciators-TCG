using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Data;

namespace AppreciatorsTcg.Core
{
    public static class PlayerDeckService
    {
        public static List<string> LoadDeckIdsOrStarter()
        {
            List<string> savedDeck = LocalSaveSystem.LoadDeckIds();
            return ValidateDeck(savedDeck) ? savedDeck : CardCatalog.StarterDeckIds();
        }

        public static List<CardDefinition> LoadDeckOrStarter()
        {
            return CardCatalog.GetCards(LoadDeckIdsOrStarter());
        }

        public static bool SaveDeck(List<string> deckIds, out string message)
        {
            if (!ValidateDeck(deckIds))
            {
                message = $"Deck must contain exactly {GameConstants.DeckSize} valid cards.";
                return false;
            }

            LocalSaveSystem.SaveDeckIds(deckIds);
            message = "Deck saved locally.";
            return true;
        }

        public static bool ValidateDeck(List<string> deckIds)
        {
            if (deckIds == null || deckIds.Count != GameConstants.DeckSize)
            {
                return false;
            }

            return deckIds.All(id => CardCatalog.GetCard(id) != null);
        }
    }
}
