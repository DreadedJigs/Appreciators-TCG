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
        public LaneScoreResult[] laneScores;
        public int playerLaneWins;
        public int opponentLaneWins;
        public string winner;
    }

    public static class MatchResultData
    {
        public static MatchResult LastResult;
    }
}
