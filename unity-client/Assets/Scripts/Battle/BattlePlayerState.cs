using System;
using System.Collections.Generic;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public class BattlePlayerState
    {
        public BattlePlayerState(string displayName, List<CardDefinition> deck, int seed)
        {
            DisplayName = displayName;
            DrawPile = new List<CardDefinition>(deck);
            Hand = new List<CardDefinition>();
            Leader = BattleLeaderDefinition.SelectForDeck(deck);
            Shuffle(DrawPile, new Random(seed));
        }

        public string DisplayName { get; }
        public List<CardDefinition> DrawPile { get; }
        public List<CardDefinition> Hand { get; }
        public List<CardDefinition> DiscardPile { get; } = new List<CardDefinition>();
        public List<CardDefinition> RevealedCards { get; } = new List<CardDefinition>();
        public HashSet<string> ReturnedAfterDefeatIds { get; } = new HashSet<string>();
        public BattleLeaderDefinition Leader { get; }
        public bool LeaderAbilityUsed { get; set; }
        public int Health { get; set; } = GameConstants.StartingHealth;
        public int Appreciation { get; set; }
        public int UnbankedGrowth { get; set; }
        public int GrowthScore
        {
            get => Appreciation;
            set => Appreciation = Math.Max(0, value);
        }
        public bool IsInSpotlight => Appreciation >= GameConstants.SpotlightGrowthThreshold;

        // Save/invite compatibility for the retired score name.
        public bool HasCommittedCardThisTurn { get; set; }
        public bool HasForcedDiscardedThisTurn { get; set; }
        public int PendingAbilityGrowth { get; set; }
        public int PendingGrowthBonus { get; set; }
        public int PendingGrowthPenalty { get; set; }
        public int PendingBoardGrowth { get; set; }
        public int PendingCombinationGrowth { get; set; }
        public int PendingTriggerGrowth { get; set; }
        public int TallyMultiplierPercent { get; set; } = 100;
        public string LastLearnedCardName { get; set; }
        public BattleTallyResult LastTally { get; set; }
        public bool SkipNextCommitPhase { get; set; }
        public bool CommitSkippedThisTurn { get; set; }
        public bool CannotAttackNextTurn { get; set; }
        public bool CannotAttackThisTurn { get; set; }
        public bool CancelNextIncomingAttack { get; set; }
        public bool RedirectNextIncomingAttack { get; set; }
        public bool PreventNextTally { get; set; }
        public bool BreakComboThisTally { get; set; }
        public int ExtraAttacksThisCombat { get; set; }

        // Compatibility alias for the existing invite-match payload.
        public int Energy
        {
            get => GrowthScore;
            set => GrowthScore = Math.Max(0, value);
        }
        public int ArtShards { get; private set; }
        public int BlockchainShards { get; private set; }
        public int CommunityShield { get; private set; }
        public int CommunityRally { get; private set; }

        public void GrantLaneShards()
        {
            ArtShards += 1;
            BlockchainShards += 1;
        }

        public void GrantShard(LaneType lane, int amount)
        {
            int safeAmount = Math.Max(0, amount);
            if (lane == LaneType.Art)
            {
                ArtShards += safeAmount;
            }
            else if (lane == LaneType.Blockchain)
            {
                BlockchainShards += safeAmount;
            }
        }

        public bool TryInvestArtShard()
        {
            if (ArtShards <= 0 || CommunityShield >= 3)
            {
                return false;
            }

            ArtShards -= 1;
            CommunityShield += 1;
            return true;
        }

        public bool TryInvestBlockchainShard()
        {
            if (BlockchainShards <= 0 || CommunityRally >= 3)
            {
                return false;
            }

            BlockchainShards -= 1;
            CommunityRally += 1;
            return true;
        }

        public void ClearCommunityInvestments()
        {
            CommunityShield = 0;
            CommunityRally = 0;
        }

        public void DrawCards(int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                DrawCard();
            }
        }

        public bool DrawCard()
        {
            if (DrawPile.Count == 0)
            {
                return false;
            }

            Hand.Add(DrawPile[0]);
            DrawPile.RemoveAt(0);
            return true;
        }

        public void DrawToDecisionHand()
        {
            while (Hand.Count < GameConstants.DecisionHandSize && DrawCard())
            {
            }
        }

        public void QueueGrowth(int amount)
        {
            PendingAbilityGrowth += Math.Max(0, amount);
        }

        public void Reveal(CardDefinition card)
        {
            if (card != null && !RevealedCards.Contains(card))
            {
                RevealedCards.Add(card);
            }
        }

        public bool IsRevealed(CardDefinition card)
        {
            return card != null && RevealedCards.Contains(card);
        }

        public void ForgetRevealed(CardDefinition card)
        {
            RevealedCards.Remove(card);
        }

        public void ResetForNewTurn()
        {
            HasCommittedCardThisTurn = false;
            HasForcedDiscardedThisTurn = false;
            PendingAbilityGrowth = 0;
            PendingGrowthBonus = 0;
            PendingGrowthPenalty = 0;
            PendingBoardGrowth = 0;
            PendingCombinationGrowth = 0;
            PendingTriggerGrowth = 0;
            TallyMultiplierPercent = 100;
            LastLearnedCardName = null;
            ClearCommunityInvestments();
            UnbankedGrowth = 0;
            CommitSkippedThisTurn = SkipNextCommitPhase;
            SkipNextCommitPhase = false;
            CannotAttackThisTurn = CannotAttackNextTurn;
            CannotAttackNextTurn = false;
            CancelNextIncomingAttack = false;
            RedirectNextIncomingAttack = false;
            PreventNextTally = false;
            BreakComboThisTally = false;
            ExtraAttacksThisCombat = 0;
        }

        private static void Shuffle<T>(IList<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
            }
        }
    }
}
