using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.AI
{
    public static class SimpleAiPlayer
    {
        public static void PlayTurn(BattleGame game, Random random)
        {
            int plays = 0;
            while (plays < 8)
            {
                List<int> playableIndexes = FindPlayableCards(game);
                if (playableIndexes.Count == 0)
                {
                    break;
                }

                int handIndex = PickPlayableCard(game, playableIndexes);
                LaneType lane = PickLane(game, game.Opponent.Hand[handIndex], random);
                if (!game.TryPlayOpponentCard(handIndex, lane, out _))
                {
                    break;
                }

                plays += 1;
            }
        }

        private static List<int> FindPlayableCards(BattleGame game)
        {
            List<LaneType> openLanes = game.GetOpenLanes(OwnerSide.Opponent);
            if (openLanes.Count == 0)
            {
                return new List<int>();
            }

            List<int> playable = new List<int>();
            for (int i = 0; i < game.Opponent.Hand.Count; i++)
            {
                if (game.GetEffectiveCost(game.Opponent, game.Opponent.Hand[i]) <= game.Opponent.Energy)
                {
                    playable.Add(i);
                }
            }

            return playable;
        }

        private static int PickPlayableCard(BattleGame game, List<int> playableIndexes)
        {
            return playableIndexes
                .OrderByDescending(index => game.GetEffectiveCost(game.Opponent, game.Opponent.Hand[index]))
                .ThenByDescending(index => game.Opponent.Hand[index].power)
                .First();
        }

        private static LaneType PickLane(BattleGame game, CardDefinition card, Random random)
        {
            List<LaneType> weighted = new List<LaneType>();
            foreach (LaneType lane in game.GetOpenLanes(OwnerSide.Opponent))
            {
                int opponentPower = game.GetLanePower(lane, OwnerSide.Opponent);
                int playerPower = game.GetLanePower(lane, OwnerSide.Player);
                int weight = opponentPower < playerPower ? 4 : 1;

                if (opponentPower == playerPower)
                {
                    weight += 1;
                }

                if (card.HasLaneAffinity(lane.ToString()))
                {
                    weight += 2;
                }

                for (int i = 0; i < weight; i++)
                {
                    weighted.Add(lane);
                }
            }

            return weighted[random.Next(weighted.Count)];
        }
    }
}
