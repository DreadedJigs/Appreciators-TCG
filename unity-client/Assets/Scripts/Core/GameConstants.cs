namespace AppreciatorsTcg.Core
{
    public static class GameConstants
    {
        public const int DeckSize = 30;
        // Every turn starts with a fresh two-card decision. The chosen card is
        // committed and the remaining card is forced into the public discard.
        public const int StartingHandSize = 0;
        public const int DecisionHandSize = 2;
        public const int CardsDrawnPerTurn = 2;
        public const int MaxTurn = 11;
        public const int MaxCardsPerLanePerPlayer = 5;
        public const int MaxNormalCardCopies = 2;
        public const int MaxPremiumCardCopies = 1;
        public const int StartingHealth = 30;
        public const int SpotlightGrowthThreshold = 38;
        public const int GrowthVictoryTarget = 50;

        // Compatibility name for older scenes and invite payloads.
        public const int AppreciationVictoryTarget = GrowthVictoryTarget;

        public const string Original = "ORIGINAL";
        public const string Companion = "COMPANION";
        public const string Item = "ITEM";
        public const string Event = "EVENT";

        public const string Common = "Common";
        public const string Uncommon = "Uncommon";
        public const string Rare = "Rare";
        public const string Epic = "Epic";
        public const string Legendary = "Legendary";
        public const string Crown = "Crown";
        public const string Mythic = "Mythic";
        public const string OneOfOne = "1/1";
    }
}
