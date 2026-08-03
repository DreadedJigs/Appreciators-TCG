using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Data;
using UnityEngine;

namespace AppreciatorsTcg.Packs
{
    public class PackInventoryService
    {
        private readonly IPackSaveService saveService;

        public PackInventoryState State { get; private set; }

        public PackInventoryService(IPackSaveService saveService)
        {
            this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            State = saveService.Load() ?? new PackInventoryState();
            State.cards = State.cards ?? new List<PlayerCardInventoryEntry>();
            State.packs = State.packs ?? new List<PlayerPackInventoryEntry>();
        }

        public int AppreciationShards => State.appreciationShards;

        public int OwnedCardCount => State.cards.Count(entry => entry != null && entry.ownedCount > 0);

        public HashSet<string> OwnedCardIds()
        {
            return new HashSet<string>(State.cards
                .Where(entry => entry != null && entry.ownedCount > 0 && !string.IsNullOrWhiteSpace(entry.cardId))
                .Select(entry => entry.cardId));
        }

        public int GetPackCount(string packId)
        {
            PlayerPackInventoryEntry entry = State.packs.FirstOrDefault(item => item != null && item.packId == packId);
            return entry?.count ?? 0;
        }

        public void ReplaceWithAuthoritativeSnapshot(PackServerInventory inventory)
        {
            if (inventory == null)
            {
                Debug.LogError("[PackOpening] Local inventory mirror rejected a null authoritative snapshot.");
                return;
            }

            State.appreciationShards = inventory.appreciationShards;
            State.cards = inventory.cards?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.cardId))
                .Select(entry =>
                {
                    entry.ownedCount = entry.ownedCount > 0 ? entry.ownedCount : entry.quantityOwned;
                    entry.quantityOwned = entry.ownedCount;
                    entry.playerId = string.IsNullOrWhiteSpace(entry.playerId) ? inventory.playerId : entry.playerId;
                    return entry;
                })
                .ToList() ?? new List<PlayerCardInventoryEntry>();
            State.packs = inventory.packs?
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.packId))
                .Select(entry =>
                {
                    int count = Math.Max(0, entry.count > 0 ? entry.count : entry.quantityOwned);
                    return new PlayerPackInventoryEntry
                    {
                        playerId = inventory.playerId,
                        packId = entry.packId,
                        count = count,
                        quantityOwned = count,
                        updatedAt = entry.updatedAt
                    };
                })
                .ToList() ?? new List<PlayerPackInventoryEntry>();
            Save();
        }

#if UNITY_EDITOR
        public void GrantPack(string packId, int count = 1)
        {
            if (string.IsNullOrWhiteSpace(packId) || count <= 0)
            {
                return;
            }

            PlayerPackInventoryEntry entry = State.packs.FirstOrDefault(item => item != null && item.packId == packId);
            if (entry == null)
            {
                entry = new PlayerPackInventoryEntry { packId = packId, count = 0 };
                State.packs.Add(entry);
            }

            entry.count += count;
            Save();
        }

        public bool TryConsumePack(string packId)
        {
            PlayerPackInventoryEntry entry = State.packs.FirstOrDefault(item => item != null && item.packId == packId);
            if (entry == null || entry.count <= 0)
            {
                return false;
            }

            entry.count--;
            Save();
            return true;
        }

        public void ApplyRewardResult(PackRewardResult result)
        {
            if (result?.cards == null)
            {
                Debug.LogError("[PackOpening] Editor inventory rejected a missing reward result or card list.");
                return;
            }

            if (result.attunementShardsSpent > State.appreciationShards)
            {
                Debug.LogError($"[PackOpening] Editor inventory cannot spend {result.attunementShardsSpent} attunement shards from a balance of {State.appreciationShards}.");
                return;
            }

            State.appreciationShards -= Math.Max(0, result.attunementShardsSpent);
            State.appreciationShards += Math.Max(0, result.packShardsAwarded);

            string timestamp = DateTime.UtcNow.ToString("O");

            foreach (PackRewardCardResult reward in result.cards)
            {
                if (reward?.card == null || string.IsNullOrWhiteSpace(reward.card.id))
                {
                    continue;
                }

                PlayerCardInventoryEntry entry = State.cards.FirstOrDefault(item => item != null && item.cardId == reward.card.id);
                if (entry == null)
                {
                    entry = new PlayerCardInventoryEntry
                    {
                        cardId = reward.card.id,
                        ownedCount = 0,
                        duplicateCount = 0,
                        firstAcquiredUtc = timestamp
                    };
                    State.cards.Add(entry);
                }

                entry.ownedCount++;
                entry.lastAcquiredUtc = timestamp;

                if (reward.isDuplicate)
                {
                    entry.duplicateCount++;
                    State.appreciationShards += reward.shardsAwarded;
                }
            }

            Save();
        }

        public void ResetInventory()
        {
            saveService.Reset();
            State = new PackInventoryState();
            Save();
        }
#endif

        public void Save()
        {
            saveService.Save(State);
        }

        public static int ShardsForRarity(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common:
                    return 5;
                case Rarity.Uncommon:
                    return 15;
                case Rarity.Rare:
                    return 50;
                case Rarity.Epic:
                    return 150;
                case Rarity.Legendary:
                    return 500;
                default:
                    return 0;
            }
        }
    }
}
