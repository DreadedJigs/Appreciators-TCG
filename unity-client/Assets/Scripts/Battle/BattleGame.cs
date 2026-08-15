using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.AI;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;

namespace AppreciatorsTcg.Battle
{
    public enum BattleTurnPhase
    {
        Draw,
        Learn,
        BuildOrDiscard,
        EndTurn,
        Discard,
        ForcedDiscard = Discard,
        Combat,
        Battle = Combat,
        GatherGrowth,
        Cycle,
        Complete
    }

    public sealed class BattleTallyResult
    {
        public OwnerSide Side { get; set; }
        public int StartingGrowth { get; set; }
        public int BoardGrowth { get; set; }
        public int CombinationGrowth { get; set; }
        public int AbilityGrowth { get; set; }
        public int TriggerGrowth { get; set; }
        public int Penalty { get; set; }
        public int ModifierGrowth { get; set; }
        public int TotalGrowth { get; set; }
        public int EndingGrowth { get; set; }
        public bool EnteredSpotlight { get; set; }
        public bool ReachedVictoryTarget { get; set; }
        public int StartingAppreciation { get => StartingGrowth; set => StartingGrowth = value; }
        public int EndingAppreciation { get => EndingGrowth; set => EndingGrowth = value; }

        public string Summary(string name)
        {
            string modifier = ModifierGrowth != 0 ? $" + {ModifierGrowth} modifier" : string.Empty;
            string penalty = Penalty > 0 ? $" - {Penalty} penalty" : string.Empty;
            string spotlight = EnteredSpotlight ? " - Spotlight reached!" : string.Empty;
            return $"{name}: {StartingAppreciation} Appreciation + {TotalGrowth} Growth = {EndingAppreciation} Appreciation{modifier}{penalty}{spotlight}";
        }
    }

    public class BattleGame
    {
        private readonly Random random;
        private readonly List<BattleCombatEvent> lastCombatEvents = new List<BattleCombatEvent>();
        private readonly Dictionary<LaneType, BattleLaneEvent> laneEvents = new Dictionary<LaneType, BattleLaneEvent>();
        private readonly List<BattleTurnPhase> phaseHistory = new List<BattleTurnPhase>();
        private readonly List<BattleReplayEvent> replayEvents = new List<BattleReplayEvent>();
        private bool fieldBattleResolvedThisTurn;

        public BattleGame(string playerName, List<CardDefinition> playerDeck)
            : this(playerName, playerDeck, BuildAiDeck(), Environment.TickCount, false)
        {
        }

        public BattleGame(string playerName, List<CardDefinition> playerDeck, bool competitiveMode)
            : this(playerName, playerDeck, BuildAiDeck(), Environment.TickCount, competitiveMode)
        {
        }

        public BattleGame(string playerName, List<CardDefinition> playerDeck, List<CardDefinition> opponentDeck, int seed)
            : this(playerName, playerDeck, opponentDeck, seed, false)
        {
        }

        public BattleGame(string playerName, List<CardDefinition> playerDeck, List<CardDefinition> opponentDeck, int seed, bool competitiveMode)
        {
            random = new Random(seed);
            Player = new BattlePlayerState(playerName, playerDeck, seed);
            Opponent = new BattlePlayerState("Prototype AI", opponentDeck, seed + 13);
            MainLane = new LaneState(LaneType.Community);
            Lanes = new List<LaneState> { MainLane };
            IsCompetitiveMode = competitiveMode;
        }

        public event Action<BattleTurnPhase> PhaseChanged;
        public BattlePlayerState Player { get; }
        public BattlePlayerState Opponent { get; }
        public LaneState MainLane { get; }
        public List<LaneState> Lanes { get; }
        public int Turn { get; private set; } = 1;
        public BattleTurnPhase Phase { get; private set; } = BattleTurnPhase.Draw;
        public bool IsComplete { get; private set; }
        public bool IsCompetitiveMode { get; }
        public bool CanUseAutoAttack => !IsCompetitiveMode;
        public string AutoAttackRestriction => CanUseAutoAttack ? string.Empty : "Auto-Attack is unavailable in competitive matches. Choose every attacker and target manually.";
        public string LastMessage { get; private set; } = "Learn the table, then choose one card to Build or Discard.";
        public IReadOnlyList<BattleCombatEvent> LastCombatEvents => lastCombatEvents;
        public IReadOnlyDictionary<LaneType, BattleLaneEvent> LaneEvents => laneEvents;
        public IReadOnlyList<BattleTurnPhase> PhaseHistory => phaseHistory;
        public IReadOnlyList<BattleReplayEvent> ReplayEvents => replayEvents;
        public bool IsCommunityBlockedThisTurn => false;
        public BattleTallyResult LastPlayerTally => Player.LastTally;
        public BattleTallyResult LastOpponentTally => Opponent.LastTally;
        public CardDefinition LastPlayerForcedDiscard { get; private set; }
        public CardDefinition LastOpponentForcedDiscard { get; private set; }
        public string LastPlayerForcedDiscardMessage { get; private set; }
        public string LastOpponentForcedDiscardMessage { get; private set; }

        public int NextAbilityRoll(int maxExclusive) => random.Next(Math.Max(1, maxExclusive));

        public void Start()
        {
            Player.DrawCards(GameConstants.StartingHandSize);
            Opponent.DrawCards(GameConstants.StartingHandSize);
            StartTurn();
        }

        public LaneState GetLane(LaneType lane) => MainLane;
        public int GetEffectiveCost(BattlePlayerState owner, CardDefinition card) => 0;
        public int GetEffectiveCost(BattlePlayerState owner, CardDefinition card, LaneType? laneType) => 0;

        public bool CanAffordCardInAnyOpenLane(OwnerSide side, CardDefinition card)
        {
            BattlePlayerState owner = GetPlayerState(side);
            return !owner.HasCommittedCardThisTurn && !owner.CommitSkippedThisTurn && MainLane.HasSpace(side);
        }

        public int PreviewBoardGrowth(OwnerSide side) => BattleRules.CalculateBoardGrowth(MainLane, side) + BattleRules.CalculateCombinationGrowth(MainLane.GetCards(side));
        public int PreviewDiscardGrowth(CardDefinition card) => card == null ? 0 : card.GetDiscardGrowthValue();
        public bool TryPlayPlayerCard(int handIndex, LaneType laneType, out string message) => TryBuildCard(OwnerSide.Player, handIndex, out message);
        public bool TryPlayOpponentCard(int handIndex, LaneType laneType, out string message) => TryBuildCard(OwnerSide.Opponent, handIndex, out message);
        public bool TryPlayCardForSide(OwnerSide side, int handIndex, LaneType laneType, out string message) => TryBuildCard(side, handIndex, out message);

        public bool TryBuildCard(OwnerSide side, int handIndex, out string message)
        {
            BattlePlayerState owner = GetPlayerState(side);
            if (!CanCommitCard(owner, handIndex, out message))
            {
                LastMessage = message;
                return false;
            }
            if (owner.CommitSkippedThisTurn)
            {
                message = "A card effect skips your Build or Discard phase this turn.";
                LastMessage = message;
                return false;
            }
            if (!MainLane.HasSpace(side))
            {
                message = "Your board is full. You must Discard one of your two cards.";
                LastMessage = message;
                return false;
            }

            SetPhase(BattleTurnPhase.BuildOrDiscard);
            CardDefinition definition = owner.Hand[handIndex];
            owner.Hand.RemoveAt(handIndex);
            owner.ForgetRevealed(definition);
            BattleCardInstance card = new BattleCardInstance(definition, side);
            card.PlaceInGrowthRow();
            MainLane.GetCards(side).Add(card);
            CardEffectResolver.ApplyOnBuild(this, owner, MainLane, card);
            owner.HasCommittedCardThisTurn = true;
            replayEvents.Add(new BattleReplayEvent
            {
                Turn = Turn,
                Phase = Phase,
                Side = side,
                EventType = "card-decision",
                CardId = definition.id,
                Decision = "Build",
                Revealed = true,
                Summary = message
            });
            message = $"{owner.DisplayName} built {definition.name}. It can attack, defend, and generate {card.GrowthValue} Growth.";
            LastMessage = message;
            return true;
        }

        public bool TryDiscardCard(OwnerSide side, int handIndex, out string message)
        {
            BattlePlayerState owner = GetPlayerState(side);
            if (!CanCommitCard(owner, handIndex, out message))
            {
                LastMessage = message;
                return false;
            }

            CardDefinition definition = owner.Hand[handIndex];
            if (!CardEffectResolver.CanResolveDiscard(this, owner, side, definition, out message))
            {
                LastMessage = message;
                return false;
            }

            SetPhase(BattleTurnPhase.BuildOrDiscard);
            int appreciationBefore = owner.Appreciation;
            int growthBefore = owner.PendingAbilityGrowth;
            CardEffectResolver.PayDiscardBoardCost(this, owner, side, definition);
            owner.Hand.Remove(definition);
            owner.ForgetRevealed(definition);
            owner.DiscardPile.Add(definition);
            CardEffectResolver.ResolveDiscard(this, owner, side, definition, out string detail);
            owner.HasCommittedCardThisTurn = true;
            message = $"{owner.DisplayName} revealed and discarded {definition.name}. {detail}";
            LastMessage = message;
            replayEvents.Add(new BattleReplayEvent
            {
                Turn = Turn,
                Phase = Phase,
                Side = side,
                EventType = "card-decision",
                CardId = definition.id,
                Decision = "Discard",
                AppreciationChange = owner.Appreciation - appreciationBefore,
                GrowthChange = owner.PendingAbilityGrowth - growthBefore,
                Revealed = true,
                Summary = message
            });
            return true;
        }

        public int DiscardRemainingHandWithoutEffects(OwnerSide side)
        {
            BattlePlayerState owner = GetPlayerState(side);
            int discarded = 0;
            while (owner.Hand.Count > 0)
            {
                CardDefinition definition = owner.Hand[0];
                owner.Hand.RemoveAt(0);
                owner.ForgetRevealed(definition);
                owner.DiscardPile.Add(definition);
                discarded += 1;
            }
            owner.HasForcedDiscardedThisTurn = true;
            return discarded;
        }

        public bool ApplyRemoteCard(string cardId, LaneType laneType, string remoteName, out string message)
        {
            int handIndex = FindOrAddRemoteCard(cardId);
            if (handIndex < 0)
            {
                message = "Remote card could not be found.";
                LastMessage = message;
                return false;
            }
            bool built = TryBuildCard(OwnerSide.Opponent, handIndex, out message);
            if (built && !string.IsNullOrWhiteSpace(remoteName))
            {
                message = message.Replace(Opponent.DisplayName, remoteName);
                LastMessage = message;
            }
            return built;
        }

        public bool ApplyRemoteDiscard(string cardId, string remoteName, out string message)
        {
            int handIndex = FindOrAddRemoteCard(cardId);
            if (handIndex < 0)
            {
                message = "Remote discard could not be validated.";
                LastMessage = message;
                return false;
            }
            bool acted = TryDiscardCard(OwnerSide.Opponent, handIndex, out message);
            if (acted && !string.IsNullOrWhiteSpace(remoteName))
            {
                message = message.Replace(Opponent.DisplayName, remoteName);
                LastMessage = message;
            }
            return acted;
        }

        public void EndPlayerTurnAndRunAi()
        {
            if (IsComplete) return;
            RunAiTurn();
            ResolveGrowthAndAdvanceTurn();
        }

        public void EndPlayerTurnOnly() => ResolveGrowthAndAdvanceTurn();

        public void RunAiTurn()
        {
            if (!IsComplete && !Opponent.HasCommittedCardThisTurn)
            {
                SimpleAiPlayer.PlayTurn(this, random);
            }
        }

        public List<BattleAttackOrder> BuildAutoAttackPlan(OwnerSide side)
        {
            if (!CanUseAutoAttack && side == OwnerSide.Player)
            {
                return new List<BattleAttackOrder>();
            }

            BattlePlayerState owner = GetPlayerState(side);
            List<BattleCardInstance> attackers = MainLane.GetCards(side)
                .Where(card => card.CanAttack && !owner.CannotAttackThisTurn)
                .OrderByDescending(card => card.CurrentAttack)
                .ToList();
            List<BattleCardInstance> defenders = MainLane.GetCards(OppositeSide(side))
                .Where(card => card.IsEligibleDefender)
                .ToList();
            List<BattleAttackOrder> plan = new List<BattleAttackOrder>();

            foreach (BattleCardInstance attacker in attackers)
            {
                BattleCardInstance target = defenders
                    .OrderBy(card => card.CurrentDefense > attacker.CurrentAttack ? 1 : 0)
                    .ThenByDescending(card => card.GrowthValue + card.CurrentAttack)
                    .ThenBy(card => card.CurrentDefense)
                    .FirstOrDefault();
                plan.Add(new BattleAttackOrder
                {
                    AttackerSide = side,
                    SourceInstanceId = attacker.InstanceId,
                    TargetInstanceId = target?.InstanceId ?? 0
                });
            }

            for (int i = 0; i < owner.ExtraAttacksThisCombat && attackers.Count > 0; i++)
            {
                BattleAttackOrder repeat = plan.FirstOrDefault()?.Clone();
                if (repeat != null) plan.Add(repeat);
            }
            replayEvents.Add(new BattleReplayEvent
            {
                Turn = Turn,
                Phase = BattleTurnPhase.Combat,
                Side = side,
                EventType = "combat-plan",
                UsedAutoAttack = true,
                TargetIds = plan.Select(order => order.TargetsPlayer ? "player" : order.TargetInstanceId.ToString()).ToArray(),
                Summary = $"Auto-Attack planned {plan.Count} attack path(s)."
            });
            return plan;
        }

        public bool ValidateAttackPlan(OwnerSide side, IEnumerable<BattleAttackOrder> orders, out string message)
        {
            List<BattleAttackOrder> plan = orders?.ToList() ?? new List<BattleAttackOrder>();
            BattlePlayerState owner = GetPlayerState(side);
            if (owner.CannotAttackThisTurn && plan.Count > 0)
            {
                message = "A Discard Effect prevents this player from attacking this turn.";
                return false;
            }

            int allowedRepeats = owner.ExtraAttacksThisCombat;
            foreach (IGrouping<int, BattleAttackOrder> group in plan.GroupBy(order => order.SourceInstanceId))
            {
                int repeats = Math.Max(0, group.Count() - 1);
                allowedRepeats -= repeats;
            }
            if (allowedRepeats < 0)
            {
                message = "An attacker can only be assigned once unless a tracked effect grants an additional attack.";
                return false;
            }

            foreach (BattleAttackOrder order in plan)
            {
                BattleCardInstance attacker = FindCard(side, order.SourceInstanceId);
                if (attacker == null || !attacker.CanAttack)
                {
                    message = "The selected attacker is exhausted, disabled, destroyed, or no longer eligible.";
                    return false;
                }
                List<BattleCardInstance> defenders = MainLane.GetCards(OppositeSide(side)).Where(card => card.IsEligibleDefender).ToList();
                if (order.TargetsPlayer)
                {
                    if (defenders.Count > 0)
                    {
                        message = "Direct attacks are legal only when the opponent has no eligible defenders.";
                        return false;
                    }
                }
                else if (defenders.All(card => card.InstanceId != order.TargetInstanceId))
                {
                    message = "Choose a highlighted eligible defender.";
                    return false;
                }
            }

            message = plan.Count == 0 ? "No attacks selected. Confirm to pass Combat." : $"{plan.Count} attack{(plan.Count == 1 ? string.Empty : "s")} ready to confirm.";
            return true;
        }

        public bool ResolveCombatPlans(IEnumerable<BattleAttackOrder> playerOrders, IEnumerable<BattleAttackOrder> opponentOrders, out string message)
        {
            if (IsComplete)
            {
                message = "The match is complete.";
                return false;
            }
            if ((!Player.HasCommittedCardThisTurn && !Player.CommitSkippedThisTurn) ||
                (!Opponent.HasCommittedCardThisTurn && !Opponent.CommitSkippedThisTurn))
            {
                message = "Each player must Build or Discard before Combat unless an effect skipped that phase.";
                LastMessage = message;
                return false;
            }
            BeginEndTurnPhase();
            ResolveForcedDiscardPhase();
            List<BattleAttackOrder> playerPlan = playerOrders?.ToList() ?? new List<BattleAttackOrder>();
            List<BattleAttackOrder> opponentPlan = opponentOrders?.ToList() ?? new List<BattleAttackOrder>();
            if (!ValidateAttackPlan(OwnerSide.Player, playerPlan, out message) || !ValidateAttackPlan(OwnerSide.Opponent, opponentPlan, out message))
            {
                LastMessage = message;
                return false;
            }

            SetPhase(BattleTurnPhase.Combat);
            lastCombatEvents.Clear();
            foreach (BattleAttackOrder order in playerPlan.Concat(opponentPlan))
            {
                int eventStart = lastCombatEvents.Count;
                ResolveAttack(order);
                foreach (BattleCombatEvent combatEvent in lastCombatEvents.Skip(eventStart))
                {
                    replayEvents.Add(new BattleReplayEvent
                    {
                        Turn = Turn,
                        Phase = BattleTurnPhase.Combat,
                        Side = combatEvent.Attacker,
                        EventType = "combat-resolution",
                        CardId = combatEvent.SourceCardName,
                        TargetIds = new[] { combatEvent.TargetCardName },
                        DefenseBefore = combatEvent.DefenseBefore,
                        DefenseAfter = combatEvent.DefenseAfter,
                        DirectAttack = combatEvent.DirectAttack,
                        Summary = combatEvent.Summary()
                    });
                }
                if (Player.Health <= 0 || Opponent.Health <= 0) break;
            }
            fieldBattleResolvedThisTurn = true;

            if (Player.Health <= 0 || Opponent.Health <= 0)
            {
                CompleteMatch(Player.Health > Opponent.Health ? OwnerSide.Player : OwnerSide.Opponent);
                message = LastMessage;
                return true;
            }
            message = lastCombatEvents.Count == 0 ? "Both players passed Combat." : string.Join(" ", lastCombatEvents.Select(item => item.Summary()));
            LastMessage = message;
            return true;
        }

        public void ResolveFieldBattle()
        {
            if (fieldBattleResolvedThisTurn || IsComplete) return;
            BeginEndTurnPhase();
            ResolveForcedDiscardPhase();
            List<BattleAttackOrder> playerPlan = BuildAutoAttackPlan(OwnerSide.Player);
            List<BattleAttackOrder> opponentPlan = BuildAutoAttackPlan(OwnerSide.Opponent);
            ResolveCombatPlans(playerPlan, opponentPlan, out _);
        }

        public void ResolveCombatAndAdvanceTurn() => ResolveGrowthAndAdvanceTurn();

        public void ResolveGrowthAndAdvanceTurn()
        {
            BeginEndTurnPhase();
            ResolveForcedDiscardPhase();
            ResolveFieldBattle();
            ResolveGrowthTallyAndAdvanceTurn();
        }

        // Growth is the player-facing close of the turn. It gathers board value,
        // banks Appreciation, refreshes permanents, and begins the next Draw.
        // The old Cycle method remains as an integration alias only.
        public void ResolveGrowthTallyAndAdvanceTurn()
        {
            if (IsComplete) return;
            if (!fieldBattleResolvedThisTurn) ResolveFieldBattle();
            if (!fieldBattleResolvedThisTurn || IsComplete) return;

            GatherGrowth(OwnerSide.Player);
            GatherGrowth(OwnerSide.Opponent);
            TallyAppreciation(OwnerSide.Player);
            if (Player.Appreciation >= GameConstants.AppreciationVictoryTarget)
            {
                CompleteMatch(OwnerSide.Player);
                return;
            }
            TallyAppreciation(OwnerSide.Opponent);
            if (Opponent.Appreciation >= GameConstants.AppreciationVictoryTarget)
            {
                CompleteMatch(OwnerSide.Opponent);
                return;
            }
            if (Turn >= GameConstants.MaxTurn)
            {
                CompleteMatch(null);
                return;
            }

            ResolveRefresh();
            Turn += 1;
            StartTurn();
        }

        public void ResolveCycleAndAdvanceTurn()
        {
            ResolveGrowthTallyAndAdvanceTurn();
        }

        // Kept for compatibility with saved tests and integrations created before
        // Growth became the single player-facing tally and refresh phase.
        public void ResolveTallyAndAdvanceTurn() => ResolveGrowthTallyAndAdvanceTurn();

        public void BeginEndTurnPhase()
        {
            if (IsComplete || Phase == BattleTurnPhase.EndTurn || Phase == BattleTurnPhase.Discard ||
                Phase == BattleTurnPhase.Combat || fieldBattleResolvedThisTurn)
            {
                return;
            }

            if ((!Player.HasCommittedCardThisTurn && !Player.CommitSkippedThisTurn) ||
                (!Opponent.HasCommittedCardThisTurn && !Opponent.CommitSkippedThisTurn))
            {
                return;
            }

            SetPhase(BattleTurnPhase.EndTurn);
            LastMessage = $"Turn {Turn} ended. Resolve the remaining-card Discard phase before Combat.";
        }

        public void ResolveForcedDiscardPhase()
        {
            if (IsComplete)
            {
                return;
            }

            if (Player.HasForcedDiscardedThisTurn && Opponent.HasForcedDiscardedThisTurn)
            {
                return;
            }

            BeginEndTurnPhase();
            SetPhase(BattleTurnPhase.Discard);
            LastPlayerForcedDiscard = ForceDiscardRemainingCard(OwnerSide.Player, out string playerMessage);
            LastOpponentForcedDiscard = ForceDiscardRemainingCard(OwnerSide.Opponent, out string opponentMessage);

            // A Discard effect can return a permanent to either hand. The official
            // end-of-turn Discard phase is a hard boundary: both players enter
            // Combat with zero cards, even when an effect created an extra card.
            List<string> cleanupMessages = new List<string>();
            int safety = 0;
            while ((Player.Hand.Count > 0 || Opponent.Hand.Count > 0) && safety++ < 32)
            {
                if (Player.Hand.Count > 0)
                {
                    DiscardRandomHandCard(OwnerSide.Player, out _, out string cleanup);
                    cleanupMessages.Add(cleanup);
                }
                if (Opponent.Hand.Count > 0)
                {
                    DiscardRandomHandCard(OwnerSide.Opponent, out _, out string cleanup);
                    cleanupMessages.Add(cleanup);
                }
            }
            LastPlayerForcedDiscardMessage = playerMessage;
            LastOpponentForcedDiscardMessage = opponentMessage;
            LastMessage = string.Join(" ", new[] { playerMessage, opponentMessage }
                .Concat(cleanupMessages)
                .Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        public CardDefinition ForceDiscardRemainingCard(OwnerSide side, out string message)
        {
            BattlePlayerState owner = GetPlayerState(side);
            if (owner.HasForcedDiscardedThisTurn)
            {
                message = $"{owner.DisplayName} already completed the Discard phase.";
                return null;
            }

            owner.HasForcedDiscardedThisTurn = true;
            if (owner.Hand.Count == 0)
            {
                message = $"{owner.DisplayName} has no second card to discard.";
                return null;
            }

            DiscardRandomHandCard(side, out CardDefinition firstDiscarded, out string firstMessage);
            List<string> messages = new List<string> { firstMessage };
            while (owner.Hand.Count > 0)
            {
                DiscardRandomHandCard(side, out _, out string overflowMessage);
                messages.Add(overflowMessage);
            }

            message = string.Join(" ", messages.Where(item => !string.IsNullOrWhiteSpace(item)));
            return firstDiscarded;
        }

        private void DiscardRandomHandCard(OwnerSide side, out CardDefinition definition, out string message)
        {
            BattlePlayerState owner = GetPlayerState(side);
            if (owner.Hand.Count == 0)
            {
                definition = null;
                message = string.Empty;
                return;
            }

            int handIndex = random.Next(owner.Hand.Count);
            definition = owner.Hand[handIndex];
            int appreciationBefore = owner.Appreciation;
            int growthBefore = owner.PendingAbilityGrowth;
            bool canResolve = CardEffectResolver.CanResolveDiscard(this, owner, side, definition, out string blockedReason);
            if (canResolve)
            {
                CardEffectResolver.PayDiscardBoardCost(this, owner, side, definition);
            }

            owner.Hand.RemoveAt(handIndex);
            owner.ForgetRevealed(definition);
            owner.DiscardPile.Add(definition);
            string detail;
            if (canResolve)
            {
                CardEffectResolver.ResolveDiscard(this, owner, side, definition, out detail);
            }
            else
            {
                detail = $"Its effect could not resolve: {blockedReason}";
            }

            message = $"{owner.DisplayName}'s remaining card was randomly revealed in the Discard phase: {definition.name}. {detail}";
            replayEvents.Add(new BattleReplayEvent
            {
                Turn = Turn,
                Phase = BattleTurnPhase.Discard,
                Side = side,
                EventType = "forced-discard",
                CardId = definition.id,
                Decision = "Turn-End Discard",
                AppreciationChange = owner.Appreciation - appreciationBefore,
                GrowthChange = owner.PendingAbilityGrowth - growthBefore,
                Revealed = true,
                Summary = message
            });
        }

        public void ResolveTurnCombat() => ResolveGrowthAndAdvanceTurn();

        public int GatherGrowth(OwnerSide side)
        {
            SetPhase(BattleTurnPhase.GatherGrowth);
            BattlePlayerState owner = GetPlayerState(side);
            List<BattleCardInstance> cards = MainLane.GetCards(side);
            int boardGrowth = cards.Sum(card => card.ActivateForGrowth());
            int combination = owner.BreakComboThisTally ? 0 : BattleRules.CalculateCombinationGrowth(cards);
            int trigger = CardEffectResolver.CalculateGatherBonus(this, side) + owner.PendingGrowthBonus;
            int subtotal = Math.Max(0, boardGrowth + combination + trigger + owner.PendingAbilityGrowth - owner.PendingGrowthPenalty);
            owner.UnbankedGrowth = subtotal;
            owner.PendingBoardGrowth = boardGrowth;
            owner.PendingCombinationGrowth = combination;
            owner.PendingTriggerGrowth = trigger;
            LastMessage = $"{owner.DisplayName} gathered {subtotal} temporary Growth.";
            return subtotal;
        }

        public BattleTallyResult TallyAppreciation(OwnerSide side)
        {
            if (Phase != BattleTurnPhase.GatherGrowth) SetPhase(BattleTurnPhase.GatherGrowth);
            BattlePlayerState owner = GetPlayerState(side);
            int starting = owner.Appreciation;
            int subtotal = owner.PreventNextTally ? 0 : owner.UnbankedGrowth;
            int total = Math.Max(0, (subtotal * Math.Max(0, owner.TallyMultiplierPercent) + 99) / 100);
            int modifier = total - subtotal;
            owner.Appreciation += total;
            owner.UnbankedGrowth = 0;

            BattleTallyResult tally = new BattleTallyResult
            {
                Side = side,
                StartingAppreciation = starting,
                BoardGrowth = owner.PendingBoardGrowth,
                CombinationGrowth = owner.PendingCombinationGrowth,
                AbilityGrowth = owner.PendingAbilityGrowth,
                TriggerGrowth = owner.PendingTriggerGrowth,
                Penalty = owner.PendingGrowthPenalty,
                ModifierGrowth = modifier,
                TotalGrowth = total,
                EndingAppreciation = owner.Appreciation,
                EnteredSpotlight = starting < GameConstants.SpotlightGrowthThreshold && owner.IsInSpotlight,
                ReachedVictoryTarget = owner.Appreciation >= GameConstants.AppreciationVictoryTarget
            };
            owner.LastTally = tally;
            LastMessage = tally.Summary(owner.DisplayName);
            return tally;
        }

        public BattleTallyResult GatherAndTally(OwnerSide side)
        {
            GatherGrowth(side);
            return TallyAppreciation(side);
        }

        public void ResolveRefresh()
        {
            if (Phase != BattleTurnPhase.GatherGrowth) SetPhase(BattleTurnPhase.GatherGrowth);
            foreach (BattleCardInstance card in MainLane.PlayerCards.Concat(MainLane.OpponentCards).ToList())
            {
                card.Refresh();
                if (card.CurrentDefense <= 0) DefeatCard(MainLane, null, card);
            }
            CardEffectResolver.ApplyRefresh(this, Player, OwnerSide.Player);
            CardEffectResolver.ApplyRefresh(this, Opponent, OwnerSide.Opponent);
            LastMessage = "Growth resolved: Appreciation was banked, exhausted cards readied, and expiring effects were removed. Damage remains until explicitly restored.";
        }

        public List<LaneType> GetOpenLanes(OwnerSide side) => MainLane.HasSpace(side) ? new List<LaneType> { LaneType.Community } : new List<LaneType>();
        public int GetLanePower(LaneType lane, OwnerSide side, bool finalScore = false) => BattleRules.CalculateLanePower(MainLane, side, finalScore);
        public BattleLaneEvent GetLaneEvent(LaneType lane) => null;
        public int CommunityBuffBonus(LaneState lane) => 0;

        public bool TryInvestCommunityShield(OwnerSide side, out string message)
        {
            message = "Resource investments are retired. Choose Build or Discard during the commit phase.";
            LastMessage = message;
            return false;
        }

        public bool TryInvestCommunityRally(OwnerSide side, out string message)
        {
            message = "Resource investments are retired. Growth is generated by your shared row.";
            LastMessage = message;
            return false;
        }

        // Kept for older scene and replay payloads; the visible role title has been retired.
        public bool TryUseLeaderAbility(OwnerSide side, out string message)
        {
            BattlePlayerState owner = GetPlayerState(side);
            BattleLeaderDefinition guide = owner.Leader;
            if (guide == null)
            {
                message = "No focus ability is assigned.";
                LastMessage = message;
                return false;
            }
            if (owner.LeaderAbilityUsed)
            {
                message = $"{guide.Name}'s focus ability has already been used.";
                LastMessage = message;
                return false;
            }
            owner.TallyMultiplierPercent = 125;
            List<BattleCardInstance> focused = MainLane.GetCards(side)
                .Where(card => card.Definition.HasLaneAffinity(guide.FocusLane.ToString()))
                .ToList();
            if (guide.FocusLane == LaneType.Art)
            {
                BattleCardInstance target = focused.OrderByDescending(card => card.GrowthValue).FirstOrDefault();
                if (target != null) target.GrowthBonus += 1;
            }
            else if (guide.FocusLane == LaneType.Blockchain)
            {
                owner.QueueGrowth(2);
            }
            else
            {
                foreach (BattleCardInstance card in focused) card.GrowthBonus += 1;
            }
            owner.LeaderAbilityUsed = true;
            message = $"{owner.DisplayName} used {guide.AbilityName}. This Growth phase gains a 25% modifier.";
            LastMessage = message;
            return true;
        }

        public IEnumerable<LaneState> AllLanes() { yield return MainLane; }
        public BattlePlayerState GetPlayerState(OwnerSide side) => side == OwnerSide.Player ? Player : Opponent;
        public OwnerSide OppositeSide(OwnerSide side) => side == OwnerSide.Player ? OwnerSide.Opponent : OwnerSide.Player;

        public void DealAppreciationDamage(LaneState lane, BattleCardInstance source, BattleCardInstance target, int damage)
        {
            if (target == null || target.IsProtected) return;
            target.ApplyDefenseDamage(damage);
            if (target.CurrentDefense <= 0) DefeatCard(lane, source, target);
        }

        public void DefeatCard(LaneState lane, BattleCardInstance source, BattleCardInstance defeated)
        {
            if (defeated == null || defeated.IsProtected) return;
            lane.GetCards(defeated.Owner).Remove(defeated);
            GetPlayerState(defeated.Owner).DiscardPile.Add(defeated.Definition);
        }

        public bool TrySummonToken(OwnerSide side, LaneState lane, CardDefinition token)
        {
            if (!MainLane.HasSpace(side)) return false;
            BattleCardInstance instance = new BattleCardInstance(token, side);
            instance.PlaceInGrowthRow();
            MainLane.GetCards(side).Add(instance);
            return true;
        }

        private void ResolveAttack(BattleAttackOrder order)
        {
            BattleCardInstance attacker = FindCard(order.AttackerSide, order.SourceInstanceId);
            if (attacker == null || attacker.CurrentDefense <= 0 || attacker.IsDisabled) return;
            BattlePlayerState defenderState = GetPlayerState(OppositeSide(order.AttackerSide));
            int damage = attacker.CurrentAttack;
            attacker.MarkAttacked();

            if (defenderState.CancelNextIncomingAttack)
            {
                defenderState.CancelNextIncomingAttack = false;
                lastCombatEvents.Add(new BattleCombatEvent
                {
                    Lane = LaneType.Community, Attacker = order.AttackerSide, TargetOwner = OppositeSide(order.AttackerSide),
                    SourceInstanceId = attacker.InstanceId, SourceCardName = attacker.Definition.name,
                    TargetCardName = "opposing player", DirectAttack = order.TargetsPlayer, Cancelled = true
                });
                return;
            }

            if (order.TargetsPlayer)
            {
                int before = defenderState.Health;
                defenderState.Health = Math.Max(0, defenderState.Health - damage);
                lastCombatEvents.Add(new BattleCombatEvent
                {
                    Lane = LaneType.Community, Attacker = order.AttackerSide, TargetOwner = OppositeSide(order.AttackerSide),
                    SourceInstanceId = attacker.InstanceId, SourceCardName = attacker.Definition.name,
                    TargetCardName = defenderState.DisplayName, BaseDamage = damage, Damage = damage,
                    DirectAttack = true, HealthBefore = before, HealthAfter = defenderState.Health
                });
                return;
            }

            BattleCardInstance target = FindCard(OppositeSide(order.AttackerSide), order.TargetInstanceId);
            if (target == null) return;
            bool redirected = false;
            if (defenderState.RedirectNextIncomingAttack)
            {
                BattleCardInstance alternate = MainLane.GetCards(target.Owner).FirstOrDefault(card => card.IsEligibleDefender && card != target);
                if (alternate != null)
                {
                    target = alternate;
                    redirected = true;
                }
                defenderState.RedirectNextIncomingAttack = false;
            }

            int defenseBefore = target.CurrentDefense;
            int retaliation = target.CurrentAttack;
            int applied = target.IsProtected ? 0 : damage;
            target.ApplyDefenseDamage(applied);
            int defenseAfter = target.CurrentDefense;
            lastCombatEvents.Add(new BattleCombatEvent
            {
                Lane = LaneType.Community, Attacker = attacker.Owner, TargetOwner = target.Owner,
                SourceInstanceId = attacker.InstanceId, TargetInstanceId = target.InstanceId,
                SourceCardName = attacker.Definition.name, TargetCardName = target.Definition.name,
                BaseDamage = damage, Damage = applied, TargetProtected = target.IsProtected,
                TargetDefeated = defenseAfter <= 0, DefenseBefore = defenseBefore, DefenseAfter = defenseAfter,
                Redirected = redirected
            });
            int attackerDefenseBefore = attacker.CurrentDefense;
            int retaliationApplied = attacker.IsProtected ? 0 : retaliation;
            if (retaliation > 0)
            {
                attacker.ApplyDefenseDamage(retaliationApplied);
                lastCombatEvents.Add(new BattleCombatEvent
                {
                    Lane = LaneType.Community, Attacker = target.Owner, TargetOwner = attacker.Owner,
                    SourceInstanceId = target.InstanceId, TargetInstanceId = attacker.InstanceId,
                    SourceCardName = target.Definition.name, TargetCardName = attacker.Definition.name,
                    BaseDamage = retaliation, Damage = retaliationApplied, TargetProtected = attacker.IsProtected,
                    TargetDefeated = attacker.CurrentDefense <= 0, DefenseBefore = attackerDefenseBefore,
                    DefenseAfter = attacker.CurrentDefense
                });
            }
            if (target.CurrentDefense <= 0) DefeatCard(MainLane, attacker, target);
            if (attacker.CurrentDefense <= 0) DefeatCard(MainLane, target, attacker);
        }

        private BattleCardInstance FindCard(OwnerSide side, int instanceId) => MainLane.GetCards(side).FirstOrDefault(card => card.InstanceId == instanceId);

        private bool CanCommitCard(BattlePlayerState owner, int handIndex, out string message)
        {
            if (IsComplete) { message = "The match is complete."; return false; }
            if (owner.HasCommittedCardThisTurn) { message = "You already played the required card this turn."; return false; }
            if (handIndex < 0 || handIndex >= owner.Hand.Count) { message = "No card selected."; return false; }
            message = string.Empty;
            return true;
        }

        private int FindOrAddRemoteCard(string cardId)
        {
            int handIndex = Opponent.Hand.FindIndex(card => card.id == cardId);
            if (handIndex >= 0) return handIndex;
            CardDefinition definition = CardCatalog.GetCard(cardId);
            if (definition == null) return -1;
            Opponent.Hand.Add(definition);
            return Opponent.Hand.Count - 1;
        }

        private void StartTurn()
        {
            fieldBattleResolvedThisTurn = false;
            lastCombatEvents.Clear();
            Player.ResetForNewTurn();
            Opponent.ResetForNewTurn();
            SetPhase(BattleTurnPhase.Draw);
            Player.DrawCards(GameConstants.CardsDrawnPerTurn);
            Opponent.DrawCards(GameConstants.CardsDrawnPerTurn);
            if (Player.Hand.Count == 0)
            {
                Player.CommitSkippedThisTurn = true;
            }
            if (Opponent.Hand.Count == 0)
            {
                Opponent.CommitSkippedThisTurn = true;
            }
            SetPhase(BattleTurnPhase.Learn);
            LastMessage = Player.CommitSkippedThisTurn
                ? $"Turn {Turn}: a Discard Effect skips your Build or Discard phase. Learn the table, then continue to Combat."
                : $"Turn {Turn}: two cards were drawn automatically. Choose one to Build or Discard; the remaining card will be randomly discarded after End Turn and before Combat.";
        }

        private void SetPhase(BattleTurnPhase phase)
        {
            Phase = phase;
            phaseHistory.Add(phase);
            replayEvents.Add(new BattleReplayEvent
            {
                Turn = Turn,
                Phase = phase,
                Side = OwnerSide.Player,
                EventType = "phase-transition",
                Summary = phase == BattleTurnPhase.BuildOrDiscard ? "BUILD OR DISCARD" :
                    phase == BattleTurnPhase.Discard ? "DISCARD" : phase.ToString()
            });
            PhaseChanged?.Invoke(phase);
        }

        private void CompleteMatch(OwnerSide? forcedWinner)
        {
            IsComplete = true;
            SetPhase(BattleTurnPhase.Complete);
            int comparison = forcedWinner.HasValue
                ? (forcedWinner.Value == OwnerSide.Player ? 1 : -1)
                : Player.Appreciation.CompareTo(Opponent.Appreciation);
            if (comparison == 0) comparison = Player.Health.CompareTo(Opponent.Health);
            string matchWinner = comparison > 0 ? "Victory" : comparison < 0 ? "Defeat" : "Draw";
            string rowWinner = comparison > 0 ? "Player" : comparison < 0 ? "Opponent" : "Tie";
            MatchResultData.LastResult = new MatchResult
            {
                matchId = $"local_{Guid.NewGuid():N}",
                laneScores = new[] { new LaneScoreResult { lane = LaneType.Community, playerPower = Player.Appreciation, opponentPower = Opponent.Appreciation, winner = rowWinner } },
                playerLaneWins = comparison > 0 ? 1 : 0,
                opponentLaneWins = comparison < 0 ? 1 : 0,
                playerGrowth = Player.Appreciation,
                opponentGrowth = Opponent.Appreciation,
                playerAppreciation = Player.Appreciation,
                opponentAppreciation = Opponent.Appreciation,
                playerHp = Player.Health,
                opponentHp = Opponent.Health,
                turnsPlayed = Turn,
                winner = matchWinner
            };
            LastMessage = $"{matchWinner}: {Player.Appreciation} to {Opponent.Appreciation} Appreciation; HP {Player.Health} to {Opponent.Health}; turn {Turn}.";
        }

        private static List<CardDefinition> BuildAiDeck() => CardCatalog.GetCards(CardCatalog.StarterDeckIds());
    }
}
