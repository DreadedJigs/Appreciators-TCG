using System;

namespace AppreciatorsTcg.Core
{
    [Serializable]
    public class LaneScoreResult
    {
        public LaneType lane;
        public int playerPower;
        public int opponentPower;
        public string winner;
    }

    [Serializable]
    public class MatchResult
    {
        public string matchId;
        public LaneScoreResult[] laneScores;
        public int playerLaneWins;
        public int opponentLaneWins;
        public int playerGrowth;
        public int opponentGrowth;
        // Compatibility fields retained for old result payloads.
        public int playerAppreciation;
        public int opponentAppreciation;
        public int playerHp;
        public int opponentHp;
        public int turnsPlayed;
        public string winner;
        public string mode;
    }

    public static class MatchResultData
    {
        public static MatchResult LastResult;
    }
}
