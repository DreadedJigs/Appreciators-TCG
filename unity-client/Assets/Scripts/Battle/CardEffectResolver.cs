using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Battle
{
    public static class CardEffectResolver
    {
        public static void ApplyOnBuild(BattleGame game, BattlePlayerState owner, LaneState row, BattleCardInstance card)
        {
            List<BattleCardInstance> friendly = row.GetCards(card.Owner);
            List<BattleCardInstance> opposing = row.GetCards(game.OppositeSide(card.Owner));
            switch (card.Definition.effectId)
            {
                case "tiger_shark_head":
                    if (card.CurrentAttack > opposing.Select(item => item.CurrentAttack).DefaultIfEmpty(0).Max()) owner.QueueGrowth(3);
                    break;
                case "unicorn_head":
                    int unicornIndex = friendly.IndexOf(card);
                    foreach (BattleCardInstance neighbor in friendly.Where((item, index) => item != card && Math.Abs(index - unicornIndex) == 1))
                        neighbor.ApplyStatEffect("Unicorn Head", 1, 1, false, game.Turn);
                    break;
                case "alpha_kaiju_head":
                    foreach (BattleCardInstance ally in friendly) ally.ApplyStatEffect("Alpha Kaiju", 1, 1, false, game.Turn);
                    break;
                case "no_head_body":
                case "blue_skin":
                    card.ApplyStatEffect(card.Definition.name, 1, 1, false, game.Turn);
                    break;
                case "decapitated_body":
                case "ghost_flame_background":
                    card.IsProtected = true;
                    card.ProtectedUntilTurn = game.Turn + 1;
                    break;
                case "pink_lemonade_background":
                    owner.QueueGrowth(2);
                    break;
                case "tropical_background":
                case "captain_fish_food":
                    RevealRetainedCard(owner, game.GetPlayerState(game.OppositeSide(card.Owner)));
                    break;
                case "overcast_background":
                    game.GetPlayerState(game.OppositeSide(card.Owner)).PendingGrowthPenalty += 1;
                    break;
                case "second_hand_smoke_seafoam":
                    foreach (BattleCardInstance ally in friendly) ally.ApplyStatEffect("Seafoam Support", 0, 1, false, game.Turn);
                    break;
                case "purple_skin":
                    owner.QueueGrowth(1);
                    game.GetPlayerState(game.OppositeSide(card.Owner)).PendingGrowthPenalty += 1;
                    break;
                case "chaos":
                    ApplyChaosRoll(game, owner, card);
                    break;
                case "the_original":
                    foreach (BattleCardInstance original in friendly.Where(item => item.Definition.IsType(GameConstants.Original)))
                        original.ApplyStatEffect("The Original", 1, 1, false, game.Turn);
                    break;
            }
        }

        public static bool CanResolveDiscard(BattleGame game, BattlePlayerState owner, OwnerSide side, CardDefinition card, out string message)
        {
            // Commit is deliberately frictionless: a player must always be able
            // to choose either of their two cards as the round's one action.
            // Legacy card data can still carry an old board-cost field, but it
            // is presentation metadata now rather than a condition that blocks
            // an otherwise legal Discard choice.
            message = string.Empty;
            return true;
        }

        public static void PayDiscardBoardCost(BattleGame game, BattlePlayerState owner, OwnerSide side, CardDefinition card)
        {
            string type = card.discardBoardCostType ?? "none";
            int amount = Math.Max(1, card.discardBoardCostAmount);
            List<BattleCardInstance> allies = game.MainLane.GetCards(side);
            switch (type)
            {
                case "exhaust_ally":
                    BattleCardInstance readyAlly = allies.FirstOrDefault(unit => !unit.IsExhausted);
                    (readyAlly ?? allies.FirstOrDefault())?.Exhaust();
                    break;
                case "exhaust_two_allies":
                    List<BattleCardInstance> readyAllies = allies.Where(item => !item.IsExhausted).Take(2).ToList();
                    foreach (BattleCardInstance unit in readyAllies) unit.Exhaust();
                    int missing = 2 - readyAllies.Count;
                    foreach (BattleCardInstance unit in allies.Where(item => !readyAllies.Contains(item)).Take(missing)) unit.Exhaust();
                    break;
                case "damage_ally":
                    BattleCardInstance damaged = allies.OrderByDescending(unit => unit.CurrentDefense).First();
                    damaged.ApplyDefenseDamage(amount);
                    if (damaged.CurrentDefense <= 0) game.DefeatCard(game.MainLane, null, damaged);
                    break;
                case "return_ally": ReturnToHand(game, allies.FirstOrDefault()); break;
                case "sacrifice_ally": game.DefeatCard(game.MainLane, null, allies.FirstOrDefault()); break;
                case "sacrifice_two_allies": foreach (BattleCardInstance unit in allies.Take(2).ToList()) game.DefeatCard(game.MainLane, null, unit); break;
                case "lose_appreciation": owner.Appreciation = Math.Max(0, owner.Appreciation - amount); break;
                case "discard_hidden":
                    int selectedIndex = owner.Hand.IndexOf(card);
                    CardDefinition hidden = owner.Hand.Select((item, index) => new { item, index })
                        .Where(entry => entry.index != selectedIndex)
                        .OrderBy(entry => owner.IsRevealed(entry.item) ? 1 : 0)
                        .Select(entry => entry.item)
                        .FirstOrDefault();
                    if (hidden != null)
                    {
                        owner.Hand.Remove(hidden);
                        owner.ForgetRevealed(hidden);
                        owner.DiscardPile.Add(hidden);
                    }
                    break;
                case "skip_commit": owner.SkipNextCommitPhase = true; break;
                case "zero_growth_skip_commit": owner.UnbankedGrowth = 0; owner.SkipNextCommitPhase = true; break;
                case "exhaust_board": foreach (BattleCardInstance unit in allies) unit.Exhaust(); break;
                case "no_attack_next_turn": owner.CannotAttackNextTurn = true; break;
            }
        }

        public static int ResolveDiscard(BattleGame game, BattlePlayerState owner, OwnerSide side, CardDefinition card, out string detail)
        {
            BattlePlayerState opponent = game.GetPlayerState(game.OppositeSide(side));
            List<BattleCardInstance> friendly = game.MainLane.GetCards(side);
            List<BattleCardInstance> opposing = game.MainLane.GetCards(game.OppositeSide(side));
            BattleCardInstance ally = friendly.OrderByDescending(unit => unit.CurrentAttack + unit.CurrentDefense).FirstOrDefault();
            BattleCardInstance enemy = opposing.OrderByDescending(unit => unit.GrowthValue * 2 + unit.CurrentAttack).FirstOrDefault();
            string effect = card.discardEffectId ?? string.Empty;
            int growth = 0;

            switch (effect)
            {
                case "restore_defense":
                    ally?.RestoreDefense(2);
                    detail = ally == null ? "No allied unit was available." : $"{ally.Definition.name} restored 2 Defense.";
                    break;
                case "reduce_defense":
                    enemy?.ApplyStatEffect(card.name, 0, -2, true, game.Turn);
                    if (enemy != null && enemy.CurrentDefense <= 0) game.DefeatCard(game.MainLane, null, enemy);
                    detail = enemy == null ? "No opposing unit was available." : $"{enemy.Definition.name} lost 2 Defense until Growth resolves.";
                    break;
                case "steal_growth":
                    bool highImpact = card.GetDiscardCategory().IndexOf("Costly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        card.GetDiscardCategory().IndexOf("Dangerous", StringComparison.OrdinalIgnoreCase) >= 0;
                    int stolen = Math.Min(highImpact ? 5 : 2, opponent.UnbankedGrowth);
                    opponent.UnbankedGrowth -= stolen;
                    owner.UnbankedGrowth += stolen;
                    detail = $"Stole {stolen} unbanked Growth before Growth resolves.";
                    break;
                case "reveal_hidden":
                    CardDefinition revealed = RevealRetainedCard(owner, opponent);
                    detail = revealed == null ? "No hidden card remained." : $"{revealed.name} is now public and remains face-up.";
                    break;
                case "ready_unit":
                    ally?.Ready();
                    detail = ally == null ? "No allied unit was available." : $"{ally.Definition.name} is ready.";
                    break;
                case "extra_attack": owner.ExtraAttacksThisCombat += 1; detail = "One allied attacker may attack an additional time this Combat."; break;
                case "remove_enemy":
                    if (enemy != null) game.DefeatCard(game.MainLane, null, enemy);
                    detail = enemy == null ? "No opposing unit was available." : $"{enemy.Definition.name} was removed from the board.";
                    break;
                case "cancel_attack": owner.CancelNextIncomingAttack = true; if (ally != null) { ally.IsProtected = true; ally.ProtectedUntilTurn = game.Turn; } detail = "The next incoming attack is cancelled; the strongest allied unit is protected this turn."; break;
                case "redirect_attack": owner.RedirectNextIncomingAttack = true; detail = "The next attack against your board will be redirected to another eligible unit."; break;
                case "disable_enemy": enemy?.DisableUntilRefresh(); detail = enemy == null ? "No opposing unit was available." : $"{enemy.Definition.name} is disabled until Growth resolves."; break;
                case "break_combo": opponent.BreakComboThisTally = true; detail = "The opponent's combination Growth is removed from this Growth phase."; break;
                case "prevent_tally": opponent.PreventNextTally = true; detail = "The opponent's current unbanked Growth will not become Appreciation this Growth phase."; break;
                case "reverse_modifiers":
                    foreach (BattleCardInstance unit in opposing) unit.ReverseLatestEffect(game.Turn);
                    detail = "The latest tracked buff or nerf on each opposing unit was reversed until Growth resolves.";
                    break;
                case "return_enemy":
                    ReturnToHand(game, enemy);
                    detail = enemy == null ? "No opposing unit was available." : $"{enemy.Definition.name} returned to its owner's public hand.";
                    break;
                case "force_discard":
                    CardDefinition discarded = opponent.Hand.FirstOrDefault(item => !opponent.IsRevealed(item)) ?? opponent.Hand.FirstOrDefault();
                    if (discarded != null) { opponent.Hand.Remove(discarded); opponent.ForgetRevealed(discarded); opponent.DiscardPile.Add(discarded); }
                    detail = discarded == null ? "The opponent had no card to discard." : $"The opponent discarded {discarded.name}.";
                    break;
                case "ultimate_extra_attacks": owner.ExtraAttacksThisCombat += 2; detail = "Up to two additional attacks may be assigned this Combat."; break;
                case "ultimate_disable":
                    foreach (BattleCardInstance unit in opposing.Take(2)) unit.DisableUntilRefresh();
                    detail = "Up to two opposing units are disabled until Growth resolves.";
                    break;
                case "ultimate_original":
                    foreach (BattleCardInstance unit in opposing) unit.ReverseLatestEffect(game.Turn);
                    int copied = Math.Min(10, opponent.UnbankedGrowth);
                    owner.UnbankedGrowth += copied;
                    detail = $"Reversed opposing modifiers and copied {copied} unbanked Growth.";
                    break;
                default:
                    detail = "No additional board effect resolved.";
                    break;
            }

            // Negative discard deltas belonged to the retired early-discard
            // penalty. Discard is now a valid Commit action, so it never takes
            // Appreciation or queued Growth away from its owner.
            int appreciationBefore = owner.Appreciation;
            int appreciationChange = Math.Max(0, card.discardAppreciationChange);
            int growthChange = Math.Max(0, card.discardGrowthChange);
            owner.Appreciation = Math.Max(0, owner.Appreciation + appreciationChange);
            if (growthChange > 0)
            {
                growth = growthChange;
                owner.QueueGrowth(growth);
            }

            List<string> changes = new List<string>();
            if (owner.Appreciation != appreciationBefore)
            {
                changes.Add($"Appreciation {appreciationBefore} -> {owner.Appreciation}");
            }
            if (growthChange != 0)
            {
                changes.Add($"Growth +{growthChange}");
            }
            if (changes.Count > 0)
            {
                detail = $"{detail} {string.Join("; ", changes)}.";
            }
            return growth;
        }

        public static void ApplyRefresh(BattleGame game, BattlePlayerState owner, OwnerSide side)
        {
            foreach (BattleCardInstance card in game.MainLane.GetCards(side))
            {
                if (card.ProtectedUntilTurn >= 0 && card.ProtectedUntilTurn <= game.Turn)
                {
                    card.IsProtected = false;
                    card.ProtectedUntilTurn = -1;
                }
                switch (card.Definition.effectId)
                {
                    case "green_skin": card.ApplyStatEffect("Green Skin Refresh", 1, 0, false, game.Turn); break;
                    case "second_hand_smoke_dawn": card.ApplyStatEffect("Smoke Dawn Refresh", 0, 1, false, game.Turn); break;
                    case "chaos": ApplyChaosRoll(game, owner, card); break;
                }
            }
        }

        public static void ApplyStartOfTurn(BattleGame game, BattlePlayerState owner, OwnerSide side) => ApplyRefresh(game, owner, side);

        public static int CalculateGatherBonus(BattleGame game, OwnerSide side)
        {
            BattlePlayerState owner = game.GetPlayerState(side);
            BattlePlayerState opponent = game.GetPlayerState(game.OppositeSide(side));
            List<BattleCardInstance> friendly = game.MainLane.GetCards(side);
            int opposingPower = game.MainLane.GetCards(game.OppositeSide(side)).Select(item => item.CurrentAttack).DefaultIfEmpty(0).Max();
            int bonus = 0;
            foreach (BattleCardInstance card in friendly)
            {
                if (card.Definition.effectId == "great_white_head" && card.CurrentAttack > opposingPower) bonus += 2;
                if (card.Definition.effectId == "blockchain_background" && friendly.Count(item => item.Definition.HasLaneAffinity("Blockchain")) >= 2) bonus += 2;
                if (card.Definition.effectId == "yellow_skin" && owner.Appreciation < opponent.Appreciation) bonus += 1;
            }
            return bonus;
        }

        public static void ApplyOnPlay(BattleGame game, BattlePlayerState owner, LaneState lane, BattleCardInstance card, int ownerPowerBefore, int opponentPowerBefore) => ApplyOnBuild(game, owner, lane, card);
        public static void ApplyAfterCardPlayed(BattleGame game, OwnerSide playedSide, BattleCardInstance playedCard) { }

        private static CardDefinition RevealRetainedCard(BattlePlayerState viewer, BattlePlayerState opponent)
        {
            CardDefinition revealed = opponent.Hand.FirstOrDefault(item => !opponent.IsRevealed(item)) ?? opponent.Hand.FirstOrDefault();
            if (revealed != null)
            {
                opponent.Reveal(revealed);
                viewer.LastLearnedCardName = revealed.name;
            }
            return revealed;
        }

        private static void ReturnToHand(BattleGame game, BattleCardInstance unit)
        {
            if (unit == null) return;
            game.MainLane.GetCards(unit.Owner).Remove(unit);
            BattlePlayerState owner = game.GetPlayerState(unit.Owner);
            owner.Hand.Add(unit.Definition);
            owner.Reveal(unit.Definition);
        }

        private static void ApplyChaosRoll(BattleGame game, BattlePlayerState owner, BattleCardInstance chaos)
        {
            switch (game.NextAbilityRoll(4))
            {
                case 0: chaos.ApplyStatEffect("CHAOS", 1, 0, false, game.Turn); break;
                case 1: chaos.ApplyStatEffect("CHAOS", 0, 1, false, game.Turn); break;
                case 2: owner.QueueGrowth(2); break;
                default: owner.PendingGrowthBonus += 1; break;
            }
        }
    }
}
