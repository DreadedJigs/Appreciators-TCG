using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Data;
using UnityEngine;

namespace AppreciatorsTcg.Core
{
    [Serializable]
    public class PlayerDeckProfile
    {
        public string id;
        public string name;
        public List<string> cardIds = new List<string>();

        public PlayerDeckProfile Clone()
        {
            return new PlayerDeckProfile
            {
                id = id,
                name = name,
                cardIds = cardIds == null ? new List<string>() : new List<string>(cardIds)
            };
        }
    }

    [Serializable]
    public class PlayerDeckCollection
    {
        public List<PlayerDeckProfile> decks = new List<PlayerDeckProfile>();
        public string activeDeckId = PlayerDeckService.StarterDeckId;
    }

    public static class PlayerDeckService
    {
        public const string StarterDeckId = "starter";

        public static List<string> LoadDeckIdsOrStarter()
        {
            return GetActiveDeck().cardIds.ToList();
        }

        public static List<CardDefinition> LoadDeckOrStarter()
        {
            return CardCatalog.GetCards(LoadDeckIdsOrStarter());
        }

        public static bool SaveDeck(List<string> deckIds, out string message)
        {
            PlayerDeckProfile active = GetActiveDeck();
            string deckId = active.id == StarterDeckId ? string.Empty : active.id;
            string deckName = active.id == StarterDeckId ? "My Deck" : active.name;
            return SaveNamedDeck(deckId, deckName, deckIds, out _, out message);
        }

        public static IReadOnlyList<PlayerDeckProfile> GetSelectableDecks()
        {
            PlayerDeckCollection collection = LoadCollectionWithMigration();
            List<PlayerDeckProfile> result = new List<PlayerDeckProfile> { StarterDeck() };
            result.AddRange(collection.decks
                .Where(deck => deck != null && ValidateDeck(deck.cardIds))
                .Select(deck => deck.Clone()));
            return result;
        }

        public static IReadOnlyList<PlayerDeckProfile> GetNamedDecks()
        {
            return LoadCollectionWithMigration().decks
                .Where(deck => deck != null && ValidateDeck(deck.cardIds))
                .Select(deck => deck.Clone())
                .ToList();
        }

        public static PlayerDeckProfile GetActiveDeck()
        {
            PlayerDeckCollection collection = LoadCollectionWithMigration();
            PlayerDeckProfile active = collection.decks.FirstOrDefault(deck =>
                deck != null && deck.id == collection.activeDeckId && ValidateDeck(deck.cardIds));
            return active?.Clone() ?? StarterDeck();
        }

        public static PlayerDeckProfile GetDeck(string deckId)
        {
            if (string.IsNullOrWhiteSpace(deckId) || deckId == StarterDeckId)
            {
                return StarterDeck();
            }

            PlayerDeckProfile deck = LoadCollectionWithMigration().decks.FirstOrDefault(candidate =>
                candidate != null && candidate.id == deckId && ValidateDeck(candidate.cardIds));
            return deck?.Clone();
        }

        public static bool SelectDeck(string deckId, out string message)
        {
            PlayerDeckProfile deck = GetDeck(deckId);
            if (deck == null)
            {
                message = "That saved deck is unavailable.";
                return false;
            }

            PlayerDeckCollection collection = LoadCollectionWithMigration();
            collection.activeDeckId = deck.id;
            LocalSaveSystem.SaveDeckCollection(collection);
            LocalSaveSystem.SaveDeckIds(deck.cardIds);
            message = $"{deck.name} selected for battle.";
            return true;
        }

        public static bool SaveNamedDeck(
            string deckId,
            string deckName,
            List<string> deckIds,
            out PlayerDeckProfile savedDeck,
            out string message)
        {
            savedDeck = null;
            string safeName = SanitizeDeckName(deckName);
            if (string.IsNullOrWhiteSpace(safeName))
            {
                message = "Enter a deck name before saving.";
                return false;
            }

            if (!ValidateDeck(deckIds))
            {
                message = $"Deck must contain exactly {GameConstants.DeckSize} valid cards (2 copies normal, 1 Legendary/Mythic/Crown).";
                return false;
            }

            PlayerDeckCollection collection = LoadCollectionWithMigration();
            string targetId = string.IsNullOrWhiteSpace(deckId) || deckId == StarterDeckId
                ? $"deck_{Guid.NewGuid():N}"
                : deckId;
            PlayerDeckProfile existing = collection.decks.FirstOrDefault(deck => deck != null && deck.id == targetId);
            if (existing == null)
            {
                existing = new PlayerDeckProfile { id = targetId };
                collection.decks.Add(existing);
            }

            existing.name = safeName;
            existing.cardIds = new List<string>(deckIds);
            collection.activeDeckId = targetId;
            LocalSaveSystem.SaveDeckCollection(collection);
            LocalSaveSystem.SaveDeckIds(existing.cardIds);
            savedDeck = existing.Clone();
            message = $"{safeName} saved and selected for battle.";
            return true;
        }

        public static bool DeleteNamedDeck(string deckId, out string message)
        {
            if (string.IsNullOrWhiteSpace(deckId) || deckId == StarterDeckId)
            {
                message = "The starter deck cannot be deleted.";
                return false;
            }

            PlayerDeckCollection collection = LoadCollectionWithMigration();
            int removed = collection.decks.RemoveAll(deck => deck != null && deck.id == deckId);
            if (removed == 0)
            {
                message = "That saved deck is unavailable.";
                return false;
            }

            if (collection.activeDeckId == deckId)
            {
                collection.activeDeckId = StarterDeckId;
                LocalSaveSystem.SaveDeckIds(CardCatalog.StarterDeckIds());
            }

            LocalSaveSystem.SaveDeckCollection(collection);
            message = "Deck deleted. Starter Deck is ready if no other deck is selected.";
            return true;
        }

        public static bool ValidateDeck(List<string> deckIds)
        {
            if (deckIds == null || deckIds.Count != GameConstants.DeckSize)
            {
                return false;
            }

            return deckIds
                .GroupBy(id => id)
                .All(group =>
                {
                    CardDefinition card = CardCatalog.GetCard(group.Key);
                    return card != null && group.Count() <= MaxCopies(card);
                });
        }

        public static int MaxCopies(CardDefinition card)
        {
            if (card == null)
            {
                return 0;
            }

            bool premium = string.Equals(card.rarity, GameConstants.Legendary, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.rarity, GameConstants.Mythic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(card.rarity, GameConstants.OneOfOne, StringComparison.OrdinalIgnoreCase);
            return premium ? GameConstants.MaxPremiumCardCopies : GameConstants.MaxNormalCardCopies;
        }

        private static PlayerDeckCollection LoadCollectionWithMigration()
        {
            PlayerDeckCollection collection = LocalSaveSystem.LoadDeckCollection();
            collection.decks = collection.decks ?? new List<PlayerDeckProfile>();

            bool changed = collection.decks.RemoveAll(deck => deck == null || string.IsNullOrWhiteSpace(deck.id)) > 0;
            foreach (PlayerDeckProfile deck in collection.decks)
            {
                List<string> repaired = RepairDeck(deck.cardIds);
                if (deck.cardIds == null || !deck.cardIds.SequenceEqual(repaired))
                {
                    deck.cardIds = repaired;
                    changed = true;
                    Debug.LogWarning($"Deck '{deck.name ?? deck.id}' referenced retired or missing cards and was repaired with active starter cards.");
                }
            }

            if (collection.decks.Count == 0)
            {
                List<string> legacyDeck = LocalSaveSystem.LoadDeckIds();
                List<string> repairedLegacyDeck = RepairDeck(legacyDeck);
                if (ValidateDeck(repairedLegacyDeck))
                {
                    PlayerDeckProfile migrated = new PlayerDeckProfile
                    {
                        id = "deck_migrated",
                        name = "My Deck",
                        cardIds = repairedLegacyDeck
                    };
                    collection.decks.Add(migrated);
                    collection.activeDeckId = migrated.id;
                    changed = true;
                }
            }

            bool activeIsStarter = collection.activeDeckId == StarterDeckId;
            bool activeExists = collection.decks.Any(deck => deck.id == collection.activeDeckId && ValidateDeck(deck.cardIds));
            if (!activeIsStarter && !activeExists)
            {
                collection.activeDeckId = StarterDeckId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(collection.activeDeckId))
            {
                collection.activeDeckId = StarterDeckId;
                changed = true;
            }

            if (changed)
            {
                LocalSaveSystem.SaveDeckCollection(collection);
            }

            return collection;
        }

        private static List<string> RepairDeck(IEnumerable<string> cardIds)
        {
            List<string> repaired = new List<string>(GameConstants.DeckSize);
            foreach (string id in cardIds ?? Enumerable.Empty<string>())
            {
                TryAddWithinCopyLimit(repaired, id);
                if (repaired.Count >= GameConstants.DeckSize)
                {
                    break;
                }
            }

            foreach (string fallbackId in CardCatalog.StarterDeckIds())
            {
                if (repaired.Count >= GameConstants.DeckSize)
                {
                    break;
                }

                TryAddWithinCopyLimit(repaired, fallbackId);
            }

            if (repaired.Count < GameConstants.DeckSize)
            {
                foreach (string fallbackId in CardCatalog.AllCards.Select(card => card.id))
                {
                    if (repaired.Count >= GameConstants.DeckSize)
                    {
                        break;
                    }

                    TryAddWithinCopyLimit(repaired, fallbackId);
                }
            }

            return repaired;
        }

        private static bool TryAddWithinCopyLimit(List<string> deck, string cardId)
        {
            CardDefinition card = CardCatalog.GetCard(cardId);
            if (card == null || deck.Count(id => id == cardId) >= MaxCopies(card))
            {
                return false;
            }

            deck.Add(cardId);
            return true;
        }

        private static PlayerDeckProfile StarterDeck()
        {
            return new PlayerDeckProfile
            {
                id = StarterDeckId,
                name = "Starter Deck",
                cardIds = CardCatalog.StarterDeckIds()
            };
        }

        private static string SanitizeDeckName(string deckName)
        {
            if (string.IsNullOrWhiteSpace(deckName))
            {
                return string.Empty;
            }

            string trimmed = deckName.Trim();
            return trimmed.Length <= 24 ? trimmed : trimmed.Substring(0, 24);
        }
    }
}
