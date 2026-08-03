using System;
using System.Collections.Generic;

namespace AppreciatorsTcg.Packs
{
    [Serializable]
    public class PackRewardResult
    {
        public string rewardId;
        public string packId;
        public string packName;
        public Lane attunement;
        public string attunementLabel;
        public int attunementChancePercent;
        public bool attunementSucceeded;
        public int attunementShardsSpent;
        public string openedAtUtc;
        public int packShardsAwarded;
        public int totalDuplicateShards;
        public int totalShardsAwarded;
        public List<PackRewardCardResult> cards = new List<PackRewardCardResult>();
    }

    [Serializable]
    public class PackRewardCardResult
    {
        public int slotIndex;
        public string slotLabel;
        public bool isMysterySlot;
        public bool isDuplicate;
        public int shardsAwarded;
        public CardDefinition card;
    }
}
