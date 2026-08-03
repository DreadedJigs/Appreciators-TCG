using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public sealed class BattleCombatAnimationController : MonoBehaviour
    {
        public IEnumerator PlaySequence(
            IReadOnlyList<BattleCombatEvent> events,
            Transform boardRoot,
            Action<BattleCombatEvent> onAttack,
            Action<BattleCombatEvent> onImpact,
            Func<OwnerSide, RectTransform> discardTargetResolver = null)
        {
            if (events == null || events.Count == 0 || boardRoot == null)
            {
                yield break;
            }

            Dictionary<int, OwnerSide> deferredDefeats = new Dictionary<int, OwnerSide>();
            LaneType? announcedLane = null;
            for (int eventIndex = 0; eventIndex < events.Count; eventIndex++)
            {
                BattleCombatEvent combatEvent = events[eventIndex];
                if (!announcedLane.HasValue || announcedLane.Value != combatEvent.Lane)
                {
                    announcedLane = combatEvent.Lane;
                    yield return PlayLaneClashBanner(boardRoot, combatEvent.Lane);
                }

                if (combatEvent.LaneBlocked)
                {
                    onAttack?.Invoke(combatEvent);
                    yield return PlayLaneBlocked(boardRoot, combatEvent.Lane);
                    onImpact?.Invoke(combatEvent);
                    yield return new WaitForSecondsRealtime(0.10f);
                    continue;
                }

                RectTransform attacker = FindCard(boardRoot, combatEvent.SourceInstanceId);
                RectTransform defender = combatEvent.DirectAttack
                    ? FindHud(boardRoot, combatEvent.TargetOwner)
                    : FindCard(boardRoot, combatEvent.TargetInstanceId);
                if (attacker == null || defender == null)
                {
                    Debug.LogWarning($"Combat animation skipped missing card UI for {combatEvent.Summary()}");
                    continue;
                }

                onAttack?.Invoke(combatEvent);
                Vector3 attackStart = attacker.position;
                Quaternion rotationStart = attacker.rotation;
                Vector3 scaleStart = attacker.localScale;
                Vector3 direction = defender.position - attacker.position;
                Vector3 strikePoint = Vector3.Lerp(attacker.position, defender.position, 0.70f);
                float tilt = direction.x >= 0f ? -7f : 7f;

                Canvas attackCanvas = attacker.GetComponent<Canvas>() ?? attacker.gameObject.AddComponent<Canvas>();
                attackCanvas.overrideSorting = true;
                attackCanvas.sortingOrder = 1000;
                yield return AnimateTransform(
                    attacker,
                    attackStart,
                    strikePoint,
                    rotationStart,
                    Quaternion.Euler(0f, 0f, tilt),
                    scaleStart,
                    scaleStart * 1.13f,
                    0.18f);

                onImpact?.Invoke(combatEvent);
                RectTransform impactText = CreateImpactText(defender, combatEvent);
                UpdateVisibleDefense(defender, combatEvent);
                yield return Shake(defender, 0.18f, combatEvent.Damage > 0 ? 7f : 3f);
                yield return FloatAndFadeImpactText(impactText, 0.18f);

                yield return AnimateTransform(
                    attacker,
                    attacker.position,
                    attackStart,
                    attacker.rotation,
                    rotationStart,
                    attacker.localScale,
                    scaleStart,
                    0.20f);
                attackCanvas.overrideSorting = false;

                if (combatEvent.TargetDefeated)
                {
                    bool targetAttacksLater = events
                        .Skip(eventIndex + 1)
                        .Any(item => item.SourceInstanceId == combatEvent.TargetInstanceId);
                    if (targetAttacksLater)
                    {
                        deferredDefeats[combatEvent.TargetInstanceId] = combatEvent.TargetOwner;
                    }
                    else
                    {
                        yield return Defeat(defender, 0.32f, discardTargetResolver?.Invoke(combatEvent.TargetOwner));
                    }
                }

                if (deferredDefeats.TryGetValue(combatEvent.SourceInstanceId, out OwnerSide defeatedOwner))
                {
                    deferredDefeats.Remove(combatEvent.SourceInstanceId);
                    yield return Defeat(attacker, 0.32f, discardTargetResolver?.Invoke(defeatedOwner));
                }

                yield return new WaitForSecondsRealtime(0.06f);
            }
        }

        private static RectTransform FindCard(Transform root, int instanceId)
        {
            string expectedName = $"BattleCard_{instanceId}";
            return root.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == expectedName);
        }

        private static RectTransform FindHud(Transform root, OwnerSide side)
        {
            string expectedName = side == OwnerSide.Player ? "PlayerHud" : "OpponentHud";
            return root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(item => item.name == expectedName)
                ?? root.GetComponent<RectTransform>();
        }

        private static void UpdateVisibleDefense(RectTransform defender, BattleCombatEvent combatEvent)
        {
            if (defender == null || combatEvent.DirectAttack) return;
            Text stats = defender.GetComponentsInChildren<Text>(true).FirstOrDefault(item => item.gameObject.name == "RuntimeStats");
            if (stats != null) stats.text = $"D {combatEvent.DefenseBefore}→{combatEvent.DefenseAfter}";
        }

        private static IEnumerator AnimateTransform(
            RectTransform target,
            Vector3 fromPosition,
            Vector3 toPosition,
            Quaternion fromRotation,
            Quaternion toRotation,
            Vector3 fromScale,
            Vector3 toScale,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                target.position = Vector3.LerpUnclamped(fromPosition, toPosition, t);
                target.rotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, t);
                target.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
                yield return null;
            }

            if (target != null)
            {
                target.position = toPosition;
                target.rotation = toRotation;
                target.localScale = toScale;
            }
        }

        private static IEnumerator Shake(RectTransform target, float duration, float distance)
        {
            Vector3 start = target.localPosition;
            Image image = target.GetComponent<Image>();
            Color originalColor = image == null ? Color.white : image.color;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float fade = 1f - Mathf.Clamp01(elapsed / duration);
                target.localPosition = start + (Vector3)(UnityEngine.Random.insideUnitCircle * distance * fade);
                if (image != null)
                {
                    image.color = Color.Lerp(originalColor, new Color(1f, 0.30f, 0.28f, originalColor.a), fade);
                }

                yield return null;
            }

            if (target != null)
            {
                target.localPosition = start;
                if (image != null)
                {
                    image.color = originalColor;
                }
            }
        }

        private static IEnumerator Defeat(RectTransform target, float duration, RectTransform discardTarget)
        {
            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
            Canvas canvas = target.GetComponent<Canvas>() ?? target.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1100;
            Vector3 startScale = target.localScale;
            Vector3 startPosition = target.position;
            Vector3 endPosition = discardTarget == null
                ? target.position + Vector3.down * 80f
                : discardTarget.TransformPoint(discardTarget.rect.center);
            Quaternion startRotation = target.rotation;
            Quaternion endRotation = Quaternion.Euler(0f, 0f, target.position.x <= endPosition.x ? 15f : -15f);
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 46f;
                target.position = Vector3.Lerp(startPosition, endPosition, eased) + arc;
                target.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
                target.localScale = Vector3.Lerp(startScale, startScale * 0.28f, eased);
                canvasGroup.alpha = Mathf.Lerp(1f, 0.12f, t);
                yield return null;
            }

            if (target != null)
            {
                canvas.overrideSorting = false;
            }
        }

        private static IEnumerator PlayLaneBlocked(Transform boardRoot, AppreciatorsTcg.Core.LaneType lane)
        {
            RectTransform laneRect = boardRoot.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == lane.ToString() || item.name == "GrowthLane");
            if (laneRect == null)
            {
                yield break;
            }

            GameObject flash = new GameObject("LaneBlockedFlash", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            flash.transform.SetParent(laneRect, false);
            RectTransform flashRect = flash.GetComponent<RectTransform>();
            UIFactory.Stretch(flashRect);
            Image image = flash.GetComponent<Image>();
            image.color = new Color(UIFactory.Accent.r, UIFactory.Accent.g, UIFactory.Accent.b, 0.26f);
            image.raycastTarget = false;
            CanvasGroup group = flash.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            Text text = UIFactory.CreateText(flash.transform, "COMMUNITY BLOCKED", 18, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.raycastTarget = false;

            float elapsed = 0f;
            const float duration = 0.70f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = Mathf.Sin(t * Mathf.PI);
                flashRect.localScale = Vector3.one * Mathf.Lerp(0.90f, 1.04f, pulse);
                group.alpha = 1f - t * 0.55f;
                yield return null;
            }

            UnityEngine.Object.Destroy(flash);
        }

        private static IEnumerator PlayLaneClashBanner(Transform boardRoot, LaneType lane)
        {
            RectTransform laneRect = boardRoot.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(item => item.name == lane.ToString() || item.name == "GrowthLane");
            if (laneRect == null)
            {
                yield break;
            }

            GameObject banner = new GameObject("LaneClashBanner", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            banner.transform.SetParent(laneRect, false);
            RectTransform rect = banner.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.10f, 0.36f);
            rect.anchorMax = new Vector2(0.90f, 0.66f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = banner.GetComponent<Image>();
            image.color = new Color(1f, 0.78f, 0.08f, 0.70f);
            image.raycastTarget = false;
            CanvasGroup group = banner.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            Text text = UIFactory.CreateText(banner.transform, "FIELD BATTLE", 18, TextAnchor.MiddleCenter, UIFactory.Ink, FontStyle.Bold);
            UIFactory.Stretch(text.rectTransform);
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = 18;

            float elapsed = 0f;
            const float duration = 0.34f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.localScale = Vector3.one * Mathf.Lerp(0.84f, 1.04f, Mathf.Sin(t * Mathf.PI));
                group.alpha = t < 0.55f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.55f) / 0.45f);
                yield return null;
            }

            UnityEngine.Object.Destroy(banner);
        }

        private static RectTransform CreateImpactText(RectTransform defender, BattleCombatEvent combatEvent)
        {
            string label = combatEvent.Cancelled
                ? "CANCELLED"
                : combatEvent.TargetProtected
                ? "BLOCKED"
                : combatEvent.DirectAttack
                    ? $"{combatEvent.HealthBefore} HP - {combatEvent.Damage} = {combatEvent.HealthAfter}"
                : combatEvent.Damage <= 0
                    ? "WARD"
                    : $"{combatEvent.DefenseBefore} D - {combatEvent.Damage} = {combatEvent.DefenseAfter}";
            Text text = UIFactory.CreateText(
                defender,
                label,
                18,
                TextAnchor.MiddleCenter,
                combatEvent.Damage > 0 ? UIFactory.Red : UIFactory.NeonCyan,
                FontStyle.Bold);
            text.gameObject.name = "CombatImpact";
            text.raycastTarget = false;
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 8f);
            rect.sizeDelta = new Vector2(190f, 42f);
            text.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            return rect;
        }

        private static IEnumerator FloatAndFadeImpactText(RectTransform impactText, float duration)
        {
            if (impactText == null)
            {
                yield break;
            }

            CanvasGroup group = impactText.GetComponent<CanvasGroup>();
            Vector2 start = impactText.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < duration && impactText != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                impactText.anchoredPosition = start + Vector2.up * Mathf.Lerp(0f, 22f, t);
                impactText.localScale = Vector3.one * Mathf.Lerp(1.18f, 0.92f, t);
                if (group != null)
                {
                    group.alpha = 1f - t;
                }

                yield return null;
            }

            if (impactText != null)
            {
                UnityEngine.Object.Destroy(impactText.gameObject);
            }
        }
    }
}
