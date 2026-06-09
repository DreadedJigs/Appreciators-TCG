using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using AppreciatorsTcg.AI;

namespace AppreciatorsTcg.Battle
{
    public class BattleGame
    {
        private readonly Random random;

        public BattleGame(string playerName, List<CardDefinition> playerDeck)
        {
            int seed = Environment.TickCount;
            random = new Random(seed);
            Player = new BattlePlayerState(playerName, playerDeck, seed);
            Opponent = new BattlePlayerState("Prototype AI", BuildAiDeck(), seed + 13);
            Lanes = new List<LaneState>
            {
                new LaneState(LaneType.Art),
                new LaneState(LaneType.Community),
                new LaneState(LaneType.Blockchain)
            };
        }

        public BattlePlayerState Player { get; }
        public BattlePlayerState Opponent { get; }
        public List<LaneState> Lanes { get; }
        public int Turn { get; private set; } = 1;
        public bool IsComplete { get; private set; }
        public string LastMessage { get; private set; } = "Choose a card, then play it into a lane.";

        public void Start()
        {
            Player.DrawCards(GameConstants.StartingHandSize);
            Opponent.DrawCards(GameConstants.StartingHandSize);
            StartTurn();
        }

        public LaneState GetLane(LaneType lane)
        {
            return Lanes.First(item => item.Lane == lane);
        }

        public int GetEffectiveCost(BattlePlayerState owner, CardDefinition card)
        {
            int reduction = 0;
            if (card.IsType(GameConstants.Trait))
            {
                if (!owner.PlayedTraitThisTurn && HasAnyBackground(owner == Player ? OwnerSide.Player : OwnerSide.Opponent, "blockchain_portal"))
                {
                    reduction += 1;
                }

                reduction += owner.NextTraitCostReduction;
            }

            return Math.Max(0, card.cost - reduction);
        }

        public bool TryPlayPlayerCard(int handIndex, LaneType laneType, out string message)
        {
            bool played = TryPlayCard(Player, OwnerSide.Player, Opponent, handIndex, laneType, out message);
            LastMessage = message;
            return played;
        }

        public bool TryPlayOpponentCard(int handIndex, LaneType laneType, out string message)
        {
            bool played = TryPlayCard(Opponent, OwnerSide.Opponent, Player, handIndex, laneType, out message);
            LastMessage = message;
            return played;
        }

        public void EndPlayerTurnAndRunAi()
        {
            if (IsComplete)
            {
                return;
            }

            SimpleAiPlayer.PlayTurn(this, random);
            if (Turn >= GameConstants.MaxTurn)
            {
                CompleteMatch();
                return;
            }

            Turn += 1;
            StartTurn();
        }

        public List<LaneType> GetOpenLanes(OwnerSide side)
        {
            return Lanes.Where(lane => lane.HasSpace(side)).Select(lane => lane.Lane).ToList();
        }

        public int GetLanePower(LaneType lane, OwnerSide side, bool finalScore = false)
        {
            return BattleRules.CalculateLanePower(GetLane(lane), side, finalScore);
        }

        private void StartTurn()
        {
            Player.Energy = Turn;
            Opponent.Energy = Turn;
            Player.PlayedTraitThisTurn = false;
            Opponent.PlayedTraitThisTurn = false;

            Player.DrawCards(GameConstants.CardsDrawnPerTurn);
            Opponent.DrawCards(GameConstants.CardsDrawnPerTurn);
            LastMessage = $"Turn {Turn}: {Player.DisplayName} has {Player.Energy} energy.";
        }

        private bool TryPlayCard(BattlePlayerState owner, OwnerSide side, BattlePlayerState opponent, int handIndex, LaneType laneType, out string message)
        {
            if (handIndex < 0 || handIndex >= owner.Hand.Count)
            {
                message = "No card selected.";
                return false;
            }

            LaneState lane = GetLane(laneType);
            if (!lane.HasSpace(side))
            {
                message = $"{laneType} lane is full.";
                return false;
            }

            CardDefinition cardDefinition = owner.Hand[handIndex];
            int cost = GetEffectiveCost(owner, cardDefinition);
            if (owner.Energy < cost)
            {
                message = $"Not enough energy for {cardDefinition.name}.";
                return false;
            }

            int ownerPowerBefore = BattleRules.CalculateLanePower(lane, side, false);
            int opponentPowerBefore = BattleRules.CalculateLanePower(lane, side == OwnerSide.Player ? OwnerSide.Opponent : OwnerSide.Player, false);

            owner.Hand.RemoveAt(handIndex);
            owner.Energy -= cost;

            BattleCardInstance instance = new BattleCardInstance(cardDefinition, side);
            lane.GetCards(side).Add(instance);

            if (cardDefinition.IsType(GameConstants.Trait))
            {
                owner.PlayedTraitThisTurn = true;
                owner.NextTraitCostReduction = 0;
            }

            CardEffectResolver.ApplyOnPlay(this, owner, lane, instance, ownerPowerBefore, opponentPowerBefore);
            message = $"{owner.DisplayName} played {cardDefinition.name} in {laneType}.";
            return true;
        }

        private bool HasAnyBackground(OwnerSide side, string effectId)
        {
            return Lanes.Any(lane => lane.GetCards(side).Any(card => card.Definition.effectId == effectId));
        }

        private void CompleteMatch()
        {
            IsComplete = true;
            List<LaneScoreResult> scores = new List<LaneScoreResult>();
            int playerWins = 0;
            int opponentWins = 0;

            foreach (LaneState lane in Lanes)
            {
                int playerPower = BattleRules.CalculateLanePower(lane, OwnerSide.Player, true);
                int opponentPower = BattleRules.CalculateLanePower(lane, OwnerSide.Opponent, true);
                string winner = "Tie";

                if (playerPower > opponentPower)
                {
                    playerWins += 1;
                    winner = "Player";
                }
                else if (opponentPower > playerPower)
                {
                    opponentWins += 1;
                    winner = "Opponent";
                }

                scores.Add(new LaneScoreResult
                {
                    lane = lane.Lane,
                    playerPower = playerPower,
                    opponentPower = opponentPower,
                    winner = winner
                });
            }

            string matchWinner = "Draw";
            if (playerWins >= 2)
            {
                matchWinner = "Victory";
            }
            else if (opponentWins >= 2)
            {
                matchWinner = "Defeat";
            }

            MatchResultData.LastResult = new MatchResult
            {
                laneScores = scores.ToArray(),
                playerLaneWins = playerWins,
                opponentLaneWins = opponentWins,
                winner = matchWinner
            };
        }

        private static List<CardDefinition> BuildAiDeck()
        {
            List<string> starter = CardCatalog.StarterDeckIds();
            List<string> extras = new List<string>
            {
                "chain_guardian",
                "gallery_scout",
                "rare_eyes",
                "mint_day",
                "rare_original",
                "rally_beast"
            };

            List<string> aiIds = starter.Take(8).Concat(extras.Take(4)).ToList();
            return CardCatalog.GetCards(aiIds);
        }
    }
}
