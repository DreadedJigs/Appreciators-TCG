using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Packs;

namespace AppreciatorsTcg.Data
{
    [Serializable]
    public class AccountLoginRequest
    {
        public string username;
        public string playerId;
    }

    [Serializable]
    public class AccountProfile
    {
        public string id;
        public string username;
        public string displayName;
        public string createdAt;
        public string updatedAt;
    }

    [Serializable]
    public class AccountLoginResponse
    {
        public bool success;
        public AccountProfile profile;
        public PackServerInventory inventory;
        public string message;
    }

    [Serializable]
    public class PlayerProgress
    {
        public bool tutorialCompleted;
        public string tutorialCompletedAt;
        public PlayerMatchStats stats;
    }

    [Serializable]
    public class PlayerMatchStats
    {
        public int matchesPlayed;
        public int wins;
        public int losses;
        public int bossBattlesPlayed;
        public int bossWins;
    }

    [Serializable]
    public class PackOpenRequest
    {
        public string requestId;
        public string playerId;
        public string packId;
        public string attunement;
    }

    [Serializable]
    public class PackPurchaseRequest
    {
        public string requestId;
        public string playerId;
        public string packId;
    }

    [Serializable]
    public class MatchWinRewardRequest
    {
        public string playerId;
        public string matchId;
        public string result;
        public string mode;
    }

    [Serializable]
    public class BossContributionRequest
    {
        public string requestId;
        public string playerId;
        public string poolId;
        public int amount;
    }

    [Serializable]
    public class PackGrantRequest
    {
        public string playerId;
        public string packId;
        public int count;
    }

    [Serializable]
    public class PackPlayerRequest
    {
        public string playerId;
    }

    [Serializable]
    public class PackSimulationRequest
    {
        public string packId;
        public string attunement;
        public int count;
    }

    [Serializable]
    public class SignedPackRewardResponse
    {
        public bool success;
        public string requestId;
        public string packId;
        public string attunement;
        public int attunementChancePercent;
        public bool attunementSucceeded;
        public int attunementShardsSpent;
        public int packShardsAwarded;
        public PackRewardCardResult[] rewards = Array.Empty<PackRewardCardResult>();
        public int totalShardsAwarded;
        public int netShardChange;
        public int remainingPackCount;
        public int totalShardBalance;
        public string openedAt;
        public string errorCode;
        public string errorMessage;
        public string version;
        public string algorithm;
        public string keyId;
        public string payloadBase64;
        public string signature;
        public bool idempotentReplay;
        public PackRewardResult reward;
        public PackServerInventory inventory;

        public bool TryValidate(string expectedPlayerId, string expectedRequestId, string expectedPackId, string expectedAttunement, out string validationError)
        {
            if (!success)
            {
                validationError = $"Pack response was not successful: {errorCode} {errorMessage}".Trim();
                return false;
            }

            if (!string.Equals(requestId, expectedRequestId, StringComparison.Ordinal))
            {
                validationError = "Pack response requestId does not match the pending idempotent request.";
                return false;
            }

            if (version != "pack-reward-v1" || algorithm != "HMAC-SHA256")
            {
                validationError = "Unsupported reward signature envelope.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(payloadBase64) || string.IsNullOrWhiteSpace(signature))
            {
                validationError = "Reward signature or signed payload is missing.";
                return false;
            }

            if (!TryReadSignedPayload(payloadBase64, out PackSignedPayload signedPayload, out validationError))
            {
                return false;
            }

            if (!string.Equals(signedPayload.version, version, StringComparison.Ordinal) ||
                !string.Equals(signedPayload.playerId, expectedPlayerId, StringComparison.Ordinal) ||
                !string.Equals(signedPayload.requestId, expectedRequestId, StringComparison.Ordinal) ||
                signedPayload.reward == null ||
                !string.Equals(signedPayload.reward.rewardId, reward?.rewardId, StringComparison.Ordinal) ||
                !string.Equals(signedPayload.reward.packId, expectedPackId, StringComparison.Ordinal))
            {
                validationError = "Signed payload identity does not match the visible pack response.";
                return false;
            }

            if (reward == null || string.IsNullOrWhiteSpace(reward.rewardId))
            {
                validationError = "Reward identity is missing.";
                return false;
            }

            if (!string.Equals(reward.packId, expectedPackId, StringComparison.Ordinal))
            {
                validationError = $"Reward pack '{reward.packId}' does not match requested pack '{expectedPackId}'.";
                return false;
            }

            if (!string.Equals(reward.attunementLabel, expectedAttunement, StringComparison.OrdinalIgnoreCase))
            {
                validationError = $"Reward attunement '{reward.attunementLabel}' does not match '{expectedAttunement}'.";
                return false;
            }

            if (reward.cards == null || reward.cards.Count != 5)
            {
                validationError = "A signed pack reward must contain exactly five card slots.";
                return false;
            }

            if (rewards == null || rewards.Length != 5)
            {
                validationError = "Top-level pack response must contain exactly five finalized rewards.";
                return false;
            }

            HashSet<int> slots = new HashSet<int>();
            int mysteryCount = 0;
            for (int index = 0; index < reward.cards.Count; index++)
            {
                PackRewardCardResult item = reward.cards[index];
                if (item?.card == null)
                {
                    validationError = $"Reward slot {index + 1} has no card data.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(item.card.id) || string.IsNullOrWhiteSpace(item.card.name))
                {
                    validationError = $"Reward slot {index + 1} has an invalid card id or name.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(item.card.rarityLabel) || string.IsNullOrWhiteSpace(item.card.laneLabel))
                {
                    validationError = $"Reward card '{item.card.id}' is missing rarity or lane data.";
                    return false;
                }

                if (!Enum.TryParse(item.card.rarityLabel, true, out Rarity _))
                {
                    validationError = $"Reward card '{item.card.id}' has unknown rarity '{item.card.rarityLabel}'.";
                    return false;
                }

                if (!Enum.TryParse(item.card.laneLabel, true, out Lane _))
                {
                    validationError = $"Reward card '{item.card.id}' has unknown lane '{item.card.laneLabel}'.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(item.slotLabel))
                {
                    validationError = $"Reward slot {item.slotIndex} is missing its display label.";
                    return false;
                }

                if (item.slotIndex < 1 || item.slotIndex > 5 || !slots.Add(item.slotIndex))
                {
                    validationError = $"Reward slot index {item.slotIndex} is invalid or duplicated.";
                    return false;
                }

                if (item.shardsAwarded < 0)
                {
                    validationError = $"Reward slot {item.slotIndex} contains a negative shard award.";
                    return false;
                }

                if ((item.isDuplicate && item.shardsAwarded <= 0) || (!item.isDuplicate && item.shardsAwarded != 0))
                {
                    validationError = $"Reward slot {item.slotIndex} has inconsistent duplicate shard data.";
                    return false;
                }

                if (item.isMysterySlot)
                {
                    if (item.slotIndex != 5)
                    {
                        validationError = "The mystery reward must be in slot 5.";
                        return false;
                    }

                    mysteryCount++;
                }

                item.card.Normalize();

                PackRewardCardResult visibleItem = rewards.FirstOrDefault(candidate => candidate != null && candidate.slotIndex == item.slotIndex);
                if (visibleItem?.card == null || visibleItem.card.id != item.card.id ||
                    visibleItem.isDuplicate != item.isDuplicate || visibleItem.shardsAwarded != item.shardsAwarded)
                {
                    validationError = $"Top-level reward slot {item.slotIndex} does not match the signed reward.";
                    return false;
                }
            }

            if (mysteryCount != 1)
            {
                validationError = "A signed pack reward must contain exactly one mystery slot.";
                return false;
            }

            if (inventory == null || inventory.packs == null || inventory.cards == null)
            {
                validationError = "Authoritative inventory snapshot is missing from the response.";
                return false;
            }

            if (!string.Equals(inventory.playerId, expectedPlayerId, StringComparison.Ordinal))
            {
                validationError = "Authoritative inventory belongs to a different player.";
                return false;
            }

            if (inventory.appreciationShards < 0 || inventory.ownedCardCount < 0)
            {
                validationError = "Authoritative inventory contains negative totals.";
                return false;
            }

            HashSet<string> inventoryCardIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PlayerCardInventoryEntry entry in inventory.cards)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.cardId) || entry.ownedCount <= 0 || entry.duplicateCount < 0 || !inventoryCardIds.Add(entry.cardId))
                {
                    validationError = "Authoritative inventory contains an invalid or duplicated card entry.";
                    return false;
                }
            }

            if (inventory.ownedCardCount != inventoryCardIds.Count)
            {
                validationError = "Authoritative owned-card total does not match its card entries.";
                return false;
            }

            HashSet<string> inventoryPackIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PackServerPackEntry entry in inventory.packs)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.packId) || entry.count < 0 || !inventoryPackIds.Add(entry.packId))
                {
                    validationError = "Authoritative inventory contains an invalid or duplicated pack entry.";
                    return false;
                }
            }

            int duplicateShardTotal = 0;
            foreach (PackRewardCardResult item in reward.cards)
            {
                duplicateShardTotal += item.shardsAwarded;
            }

            if (duplicateShardTotal != reward.totalDuplicateShards)
            {
                validationError = "Reward duplicate shard total does not match its card entries.";
                return false;
            }

            if (reward.packShardsAwarded < 0 || reward.attunementShardsSpent < 0 ||
                reward.totalShardsAwarded != reward.totalDuplicateShards + reward.packShardsAwarded)
            {
                validationError = "Signed pack, duplicate, or attunement shard values are inconsistent.";
                return false;
            }

            bool neutralAttunement = string.Equals(expectedAttunement, "Neutral", StringComparison.OrdinalIgnoreCase);
            PackRewardCardResult mysteryReward = reward.cards.FirstOrDefault(item => item != null && item.isMysterySlot);
            if (neutralAttunement)
            {
                if (reward.attunementShardsSpent != 0 || reward.attunementChancePercent != 0 || reward.attunementSucceeded)
                {
                    validationError = "Natural pack opening unexpectedly contains a paid attunement outcome.";
                    return false;
                }
            }
            else
            {
                if (reward.attunementShardsSpent <= 0 || reward.attunementChancePercent <= 0 || reward.attunementChancePercent >= 100)
                {
                    validationError = "Paid attunement must have a positive cost and a non-guaranteed chance.";
                    return false;
                }

                bool mysteryMatchesLane = string.Equals(mysteryReward?.card?.laneLabel, expectedAttunement, StringComparison.OrdinalIgnoreCase);
                if (mysteryMatchesLane != reward.attunementSucceeded)
                {
                    validationError = "Mystery card lane does not match the signed attunement outcome.";
                    return false;
                }
            }

            if (attunementChancePercent != reward.attunementChancePercent ||
                attunementSucceeded != reward.attunementSucceeded ||
                attunementShardsSpent != reward.attunementShardsSpent ||
                packShardsAwarded != reward.packShardsAwarded ||
                totalShardsAwarded != reward.totalShardsAwarded ||
                netShardChange != reward.totalShardsAwarded - reward.attunementShardsSpent ||
                totalShardBalance != inventory.appreciationShards)
            {
                validationError = "Top-level shard totals do not match the signed reward and inventory snapshot.";
                return false;
            }

            PackServerPackEntry openedPack = inventory.packs.FirstOrDefault(entry => entry != null && entry.packId == expectedPackId);
            int authoritativeRemaining = openedPack?.count ?? 0;
            if (remainingPackCount != authoritativeRemaining)
            {
                validationError = "Remaining pack count does not match the authoritative inventory snapshot.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        private static bool TryReadSignedPayload(string base64Url, out PackSignedPayload payload, out string validationError)
        {
            payload = null;
            try
            {
                string base64 = base64Url.Replace('-', '+').Replace('_', '/');
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                payload = UnityEngine.JsonUtility.FromJson<PackSignedPayload>(json);
                if (payload == null)
                {
                    validationError = "Signed reward payload decoded to null.";
                    return false;
                }

                validationError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                validationError = $"Signed reward payload could not be decoded: {exception.Message}";
                return false;
            }
        }
    }

    [Serializable]
    public class PackSignedPayload
    {
        public string version;
        public string playerId;
        public string requestId;
        public PackRewardResult reward;
    }

    [Serializable]
    public class PackOpenResponse : SignedPackRewardResponse
    {
    }

    [Serializable]
    public class PackInventoryResponse
    {
        public PackServerInventory inventory;
    }

    [Serializable]
    public class PackGrantResponse
    {
        public bool alphaOnly;
        public string grantedPackId;
        public int grantedCount;
        public PackServerInventory inventory;
    }

    [Serializable]
    public class PackResetResponse
    {
        public bool alphaOnly;
        public PackServerInventory inventory;
    }

    [Serializable]
    public class PackServerInventory
    {
        public string playerId;
        public int appreciationShards;
        public int starterPacksGranted;
        public int matchWinsRewarded;
        public int ownedCardCount;
        public PlayerProgress progress;
        public PlayerCurrencyBalance currency;
        public PackServerPackEntry[] packs = Array.Empty<PackServerPackEntry>();
        public PlayerCardInventoryEntry[] cards = Array.Empty<PlayerCardInventoryEntry>();
        public string updatedAt;
    }

    [Serializable]
    public class PackPurchaseResponse
    {
        public bool success;
        public string requestId;
        public string packId;
        public string packName;
        public int shardCost;
        public int remainingShards;
        public int quantityOwned;
        public bool idempotentReplay;
        public PackServerInventory inventory;
    }

    [Serializable]
    public class MatchWinRewardResponse
    {
        public bool success;
        public string matchId;
        public int shardsAwarded;
        public int shardsChanged;
        public int rankedLossPenalty;
        public string result;
        public string mode;
        public int totalShardBalance;
        public bool idempotentReplay;
        public PackServerInventory inventory;
    }

    [Serializable]
    public class TutorialRewardResponse
    {
        public bool success;
        public string rewardId;
        public int shardsAwarded;
        public int shardsChanged;
        public int totalShardBalance;
        public bool idempotentReplay;
        public PackServerInventory inventory;
    }

    [Serializable]
    public class BossPoolStatus
    {
        public string poolId;
        public int targetShards;
        public int totalShards;
        public int remainingShards;
        public bool unlocked;
        public int contributors;
        public string updatedAt;
    }

    [Serializable]
    public class BossPoolResponse
    {
        public bool success;
        public BossPoolStatus pool;
    }

    [Serializable]
    public class BossContributionResponse
    {
        public bool success;
        public string requestId;
        public int amountContributed;
        public int totalShardBalance;
        public bool unlocked;
        public bool idempotentReplay;
        public PackServerInventory inventory;
        public BossPoolStatus pool;
    }

    [Serializable]
    public class PackServerPackEntry
    {
        public string playerId;
        public string packId;
        public int count;
        public int quantityOwned;
        public string updatedAt;
    }

    [Serializable]
    public class PackSimulationResponse
    {
        public string packId;
        public string attunement;
        public int packCount;
        public int cardsOpened;
        public PackRarityDistribution distribution;
        public PackRarityDistribution rarityDistribution;
        public PackLaneDistribution laneDistribution;
        public int duplicateCount;
        public int totalShardsAwarded;
        public float averageShardsPerPack;
    }

    [Serializable]
    public class PackRarityDistribution
    {
        public int Common;
        public int Uncommon;
        public int Rare;
        public int Epic;
        public int Legendary;
    }

    [Serializable]
    public class PackLaneDistribution
    {
        public int Art;
        public int Community;
        public int Blockchain;
        public int Neutral;
    }

    [Serializable]
    public class PlayerCurrencyBalance
    {
        public string playerId;
        public int appreciationShards;
        public string updatedAt;
    }

    [Serializable]
    public class PackOpenRequestLedgerEntry
    {
        public string playerId;
        public string requestId;
        public string packId;
        public string attunement;
        public string finalizedResponseJson;
        public string openedAt;
    }

    [Serializable]
    public class PackOddsResponse
    {
        public bool success;
        public string packId;
        public string packName;
        public string description;
        public bool attunementEnabled;
        public string[] validAttunements = Array.Empty<string>();
        public string attunementExplanation;
        public int attunementShardCost;
        public int attunementChancePercent;
        public int attunementAffectsSlot;
        public int shardCost;
        public bool purchasable;
        public string storeTierLabel;
        public string minimumMysteryRarity;
        public PackShardOddsEntry[] packShardOdds = Array.Empty<PackShardOddsEntry>();
        public bool starterRareOrBetterGuarantee;
        public PackOddsSlot[] slots = Array.Empty<PackOddsSlot>();
        public string complianceNotice;
    }

    [Serializable]
    public class PackOddsSlot
    {
        public int slotIndex;
        public string slotType;
        public string label;
        public bool isLaneAttuned;
        public bool isAttunementEligible;
        public bool isMystery;
        public PackOddsEntry[] rarityOdds = Array.Empty<PackOddsEntry>();
    }

    [Serializable]
    public class PackOddsEntry
    {
        public string rarityLabel;
        public float percent;
    }

    [Serializable]
    public class PackShardOddsEntry
    {
        public int shards;
        public float percent;
    }
}
