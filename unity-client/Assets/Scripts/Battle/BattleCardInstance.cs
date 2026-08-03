using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public class BattleCardInstance
    {
        private static int nextInstanceId = 1;

        public BattleCardInstance(CardDefinition definition, OwnerSide owner)
        {
            Definition = definition;
            Owner = owner;
            CurrentPower = definition.GetAttack();
            CurrentAppreciation = definition.GetDefense();
            InstanceId = nextInstanceId++;
        }

        public int InstanceId { get; }
        public CardDefinition Definition { get; }
        public OwnerSide Owner { get; }
        public int CurrentPower { get; set; }
        public int CurrentAppreciation { get; set; }
        public int BaseAttack => Definition.GetAttack();
        public int BaseDefense => Definition.GetDefense();
        public int CurrentAttack => Math.Max(0, CurrentPower);
        public int CurrentDefense => Math.Max(0, CurrentAppreciation);
        public IReadOnlyList<CardStatEffect> ActiveEffects => activeEffects;
        public int GrowthBonus { get; set; }
        public int TurnsInPlay { get; private set; }
        public bool IsExhausted { get; private set; }
        public bool IsProtected { get; set; }
        public int ProtectedUntilTurn { get; set; } = -1;
        public bool IsDisabled { get; private set; }
        public bool HasAttackedThisTurn { get; private set; }
        public LaneType? PlayedLane { get; private set; }
        public int LanePowerBonus { get; private set; }

        public int GrowthValue => Math.Max(1, Definition.GetBaseGrowth() + GrowthBonus);
        public bool IsEligibleDefender => CurrentDefense > 0 && !IsDisabled;
        public bool CanAttack => CurrentAttack > 0 && CurrentDefense > 0 && !IsDisabled && !IsExhausted && !HasAttackedThisTurn;
        public int TemporaryAttackBonus => activeEffects.Where(effect => effect.IsTemporary).Sum(effect => effect.AttackDelta);
        public int PermanentAttackBonus => activeEffects.Where(effect => !effect.IsTemporary).Sum(effect => effect.AttackDelta);
        public int TemporaryDefenseBonus => activeEffects.Where(effect => effect.IsTemporary).Sum(effect => effect.DefenseDelta);
        public int PermanentDefenseBonus => activeEffects.Where(effect => !effect.IsTemporary).Sum(effect => effect.DefenseDelta);

        private readonly List<CardStatEffect> activeEffects = new List<CardStatEffect>();

        public void PlaceInGrowthRow()
        {
            if (!PlayedLane.HasValue)
            {
                PlayedLane = LaneType.Community;
                LanePowerBonus = 0;
            }
        }

        public void Refresh()
        {
            RemoveTemporaryEffects();
            IsExhausted = false;
            IsDisabled = false;
            HasAttackedThisTurn = false;
            TurnsInPlay += 1;
        }

        public void Exhaust()
        {
            IsExhausted = true;
        }

        public void Ready()
        {
            IsExhausted = false;
        }

        public void DisableUntilRefresh()
        {
            IsDisabled = true;
            IsExhausted = true;
        }

        public void MarkAttacked()
        {
            HasAttackedThisTurn = true;
        }

        public int ApplyDefenseDamage(int damage)
        {
            int applied = Math.Max(0, damage);
            CurrentAppreciation = Math.Max(0, CurrentAppreciation - applied);
            return CurrentAppreciation;
        }

        public void RestoreDefense(int amount)
        {
            CurrentAppreciation = Math.Min(BaseDefense + PermanentDefenseBonus + TemporaryDefenseBonus, CurrentAppreciation + Math.Max(0, amount));
        }

        public void ApplyStatEffect(string source, int attackDelta, int defenseDelta, bool temporary, int turn)
        {
            CardStatEffect effect = new CardStatEffect
            {
                Source = string.IsNullOrWhiteSpace(source) ? "Unknown effect" : source,
                AttackDelta = attackDelta,
                DefenseDelta = defenseDelta,
                IsTemporary = temporary,
                AppliedTurn = turn,
                Duration = temporary ? "until Cycle" : "permanent"
            };
            activeEffects.Add(effect);
            CurrentPower = Math.Max(0, CurrentPower + attackDelta);
            CurrentAppreciation = Math.Max(0, CurrentAppreciation + defenseDelta);
        }

        public bool ReverseLatestEffect(int turn)
        {
            CardStatEffect latest = activeEffects.LastOrDefault();
            if (latest == null)
            {
                return false;
            }

            ApplyStatEffect($"Reversal of {latest.Source}", -latest.AttackDelta, -latest.DefenseDelta, true, turn);
            return true;
        }

        public int ActivateForGrowth()
        {
            if (IsExhausted)
            {
                return 0;
            }

            IsExhausted = true;
            return GrowthValue;
        }

        public string EffectSummary()
        {
            return activeEffects.Count == 0
                ? "No active stat effects."
                : string.Join("\n", activeEffects.Select(effect => effect.Summary()));
        }

        private void RemoveTemporaryEffects()
        {
            foreach (CardStatEffect effect in activeEffects.Where(item => item.IsTemporary).ToList())
            {
                CurrentPower = Math.Max(0, CurrentPower - effect.AttackDelta);
                CurrentAppreciation = Math.Max(0, CurrentAppreciation - effect.DefenseDelta);
                activeEffects.Remove(effect);
            }
        }

        public void ApplyLaneStrength(LaneType lane)
        {
            if (PlayedLane.HasValue)
            {
                return;
            }

            PlayedLane = lane;
            LanePowerBonus = Definition.GetLanePowerBonus(lane);
            CurrentPower += LanePowerBonus;
        }

        public string ShortLabel()
        {
            return $"{Definition.name} (Attack {CurrentAttack} / Defense {CurrentDefense} / Growth {GrowthValue})";
        }
    }
}
