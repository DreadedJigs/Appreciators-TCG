using System;

namespace AppreciatorsTcg.Packs
{
    [Serializable]
    public class PlayerCardInventoryEntry
    {
        public string playerId;
        public string cardId;
        public int ownedCount;
        public int quantityOwned;
        public int duplicateCount;
        public string firstAcquiredUtc;
        public string lastAcquiredUtc;
        public string firstAcquiredAt;
        public string lastAcquiredAt;
    }
}
