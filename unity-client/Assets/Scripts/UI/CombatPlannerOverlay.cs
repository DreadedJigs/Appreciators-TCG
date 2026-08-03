using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public sealed class CombatPlannerOverlay
    {
        private readonly RectTransform parent;
        private readonly BattleGame game;
        private readonly Action<List<BattleAttackOrder>> onConfirmed;
        private readonly List<BattleAttackOrder> orders = new List<BattleAttackOrder>();
        private GameObject root;
        private BattleCardInstance selectedAttacker;
        private bool awaitingFinalConfirmation;

        private CombatPlannerOverlay(RectTransform parent, BattleGame game, Action<List<BattleAttackOrder>> onConfirmed)
        {
            this.parent = parent;
            this.game = game;
            this.onConfirmed = onConfirmed;
        }

        public static CombatPlannerOverlay Open(RectTransform parent, BattleGame game, Action<List<BattleAttackOrder>> onConfirmed)
        {
            CombatPlannerOverlay planner = new CombatPlannerOverlay(parent, game, onConfirmed);
            planner.Rebuild();
            return planner;
        }

        public void Close()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
        }

        private void Rebuild()
        {
            bool compact = ResponsiveCanvasScaler.IsCompactLayout;
            bool phone = ResponsiveCanvasScaler.IsPhoneLayout;
            if (root != null) UnityEngine.Object.Destroy(root);
            root = UIFactory.CreatePanel(parent, "CombatPlanner", ThemeService.IsDark ? new Color(0.025f, 0.015f, 0.12f, 0.985f) : new Color(0.97f, 0.96f, 0.88f, 0.985f));
            UIFactory.SetAnchors(root.GetComponent<RectTransform>(),
                phone ? new Vector2(0.015f, 0.025f) : compact ? new Vector2(0.07f, 0.08f) : new Vector2(0.16f, 0.16f),
                phone ? new Vector2(0.985f, 0.975f) : compact ? new Vector2(0.93f, 0.92f) : new Vector2(0.84f, 0.84f),
                Vector2.zero,
                Vector2.zero);
            GameObject stack = UIFactory.CreateVerticalStack(root.transform, "PlannerStack", Color.clear, phone ? 4 : 8, phone ? 7 : 14);
            UIFactory.Stretch(stack.GetComponent<RectTransform>());
            UIFactory.CreateText(stack.transform, "COMBAT — CHOOSE ATTACKERS, TARGETS, AND ORDER", phone ? 19 : 22, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            string instruction = selectedAttacker == null
                ? "1. Select an available attacker. Press and hold any card to inspect it first."
                : $"2. Choose a highlighted legal target for {selectedAttacker.Definition.name}. Press and hold to inspect.";
            UIFactory.CreateText(stack.transform, instruction, phone ? 15 : 16, TextAnchor.MiddleCenter, UIFactory.TextColor);

            string path = orders.Count == 0 ? "ATTACK PATHS: none — passing Combat is allowed" :
                "ATTACK PATHS: " + string.Join("   •   ", orders.Select((order, index) =>
                {
                    BattleCardInstance source = game.MainLane.PlayerCards.FirstOrDefault(card => card.InstanceId == order.SourceInstanceId);
                    BattleCardInstance target = game.MainLane.OpponentCards.FirstOrDefault(card => card.InstanceId == order.TargetInstanceId);
                    return $"{index + 1}. {source?.Definition.name ?? "unit"} → {(order.TargetsPlayer ? "PLAYER" : target?.Definition.name ?? "target")}";
                }));
            UIFactory.CreateText(stack.transform, path, phone ? 13 : 14, TextAnchor.MiddleCenter, UIFactory.NeonCyan, FontStyle.Bold);

            List<BattleCardInstance> defenders = game.MainLane.OpponentCards.Where(card => card.IsEligibleDefender).ToList();
            GameObject targetRow = UIFactory.CreateHorizontalStack(stack.transform, "OpponentTargets", Color.clear, 8, 0);
            if (defenders.Count == 0)
            {
                string directLabel = selectedAttacker == null
                    ? "OPPONENT • DIRECT ATTACK AVAILABLE"
                    : $"DIRECT ATTACK\n{selectedAttacker.CurrentAttack} HP DAMAGE";
                Button direct = UIFactory.CreateButton(targetRow.transform, directLabel, () => Assign(0), UIFactory.Red);
                direct.interactable = selectedAttacker != null;
                direct.gameObject.AddComponent<LayoutElement>().preferredWidth = 230;
            }
            else
            {
                foreach (BattleCardInstance target in defenders)
                {
                    GameObject card = UIFactory.CreateMiniCardPanel(targetRow.transform, target.Definition,
                        $"A {target.CurrentAttack}  D {target.CurrentDefense}", true, phone ? 82 : 92, phone ? 118 : 132, phone ? 50 : 58);
                    CardInspectionTrigger inspection = card.AddComponent<CardInspectionTrigger>();
                    inspection.Card = target.Definition;
                    Button button = card.AddComponent<Button>();
                    button.targetGraphic = card.GetComponent<Image>();
                    button.interactable = selectedAttacker != null;
                    int id = target.InstanceId;
                    button.onClick.AddListener(() => Assign(id));
                }
            }

            Text versus = UIFactory.CreateText(stack.transform, "VS", 16, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            LayoutElement versusLayout = versus.gameObject.AddComponent<LayoutElement>();
            versusLayout.minHeight = 20;
            versusLayout.preferredHeight = 22;

            GameObject attackerRow = UIFactory.CreateHorizontalStack(stack.transform, "PlayerAttackers", Color.clear, 8, 0);
            foreach (BattleCardInstance attacker in game.MainLane.PlayerCards.Where(card => card.CanAttack && !game.Player.CannotAttackThisTurn))
            {
                bool assigned = orders.Any(order => order.SourceInstanceId == attacker.InstanceId);
                bool canRepeat = game.Player.ExtraAttacksThisCombat > orders.Count - orders.Select(order => order.SourceInstanceId).Distinct().Count();
                GameObject card = UIFactory.CreateMiniCardPanel(attackerRow.transform, attacker.Definition,
                    $"A {attacker.CurrentAttack}  D {attacker.CurrentDefense}", selectedAttacker == attacker, phone ? 82 : 92, phone ? 118 : 132, phone ? 50 : 58);
                CardInspectionTrigger inspection = card.AddComponent<CardInspectionTrigger>();
                inspection.Card = attacker.Definition;
                Button button = card.AddComponent<Button>();
                button.targetGraphic = card.GetComponent<Image>();
                button.interactable = !assigned || canRepeat;
                BattleCardInstance captured = attacker;
                button.onClick.AddListener(() => { selectedAttacker = captured; awaitingFinalConfirmation = false; Rebuild(); });
            }

            GameObject actions = UIFactory.CreateHorizontalStack(stack.transform, "CombatActions", Color.clear, 8, 0);
            Button auto = UIFactory.CreateButton(actions.transform, game.CanUseAutoAttack ? "AUTO-ATTACK" : "AUTO-ATTACK UNAVAILABLE", AutoAttack, UIFactory.Blue);
            auto.interactable = game.CanUseAutoAttack;
            UIFactory.CreateButton(actions.transform, "RESET / RESELECT", Reset, UIFactory.PanelAlt);
            string confirmLabel = awaitingFinalConfirmation ? $"FINAL CONFIRM — {orders.Count} ATTACK{(orders.Count == 1 ? string.Empty : "S")}" : "REVIEW ATTACK ORDER";
            UIFactory.CreateButton(actions.transform, confirmLabel, Confirm, UIFactory.Green);
            if (!game.CanUseAutoAttack)
                UIFactory.CreateText(stack.transform, game.AutoAttackRestriction, 13, TextAnchor.MiddleCenter, UIFactory.Red, FontStyle.Bold);
            root.transform.SetAsLastSibling();
        }

        private void Assign(int targetId)
        {
            if (selectedAttacker == null) return;
            orders.Add(new BattleAttackOrder { AttackerSide = OwnerSide.Player, SourceInstanceId = selectedAttacker.InstanceId, TargetInstanceId = targetId });
            selectedAttacker = null;
            awaitingFinalConfirmation = false;
            Rebuild();
        }

        private void AutoAttack()
        {
            if (!game.CanUseAutoAttack) return;
            orders.Clear();
            orders.AddRange(game.BuildAutoAttackPlan(OwnerSide.Player));
            selectedAttacker = null;
            awaitingFinalConfirmation = false;
            Rebuild();
        }

        private void Reset()
        {
            orders.Clear();
            selectedAttacker = null;
            awaitingFinalConfirmation = false;
            Rebuild();
        }

        private void Confirm()
        {
            if (!game.ValidateAttackPlan(OwnerSide.Player, orders, out _)) return;
            if (!awaitingFinalConfirmation)
            {
                awaitingFinalConfirmation = true;
                Rebuild();
                return;
            }
            List<BattleAttackOrder> confirmed = orders.Select(order => order.Clone()).ToList();
            Close();
            onConfirmed?.Invoke(confirmed);
        }
    }
}
