using System.Collections;
using AppreciatorsTcg.Audio;
using AppreciatorsTcg.Battle;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public sealed class PhaseAnnouncementController : MonoBehaviour
    {
        private RectTransform parent;
        private GameObject activeBanner;

        public void Configure(RectTransform host) => parent = host;

        public static string GetPhaseLabel(BattleTurnPhase phase)
        {
            if (phase == BattleTurnPhase.Draw) return "DRAW";
            if (phase == BattleTurnPhase.Commit) return "COMMIT";
            if (phase == BattleTurnPhase.Battle) return "BATTLE";
            if (phase == BattleTurnPhase.Appreciate) return "APPRECIATE";
            if (phase == BattleTurnPhase.Complete) return "MATCH COMPLETE";
            return phase.ToString().ToUpperInvariant();
        }

        public static string GetPhaseCaption(BattleTurnPhase phase)
        {
            if (phase == BattleTurnPhase.Draw) return "DRAW TWO CARDS";
            if (phase == BattleTurnPhase.Commit) return "CHOOSE 1 • BUILD OR DISCARD";
            if (phase == BattleTurnPhase.Battle) return "FIGHT OR SCORE";
            if (phase == BattleTurnPhase.Appreciate) return "READY CARDS SCORE";
            return string.Empty;
        }

        public IEnumerator PlayPhase(BattleTurnPhase phase)
        {
            if (parent == null) yield break;
            Clear();
            string label = GetPhaseLabel(phase);
            string caption = GetPhaseCaption(phase);
            UiAudioService.PlayPhaseSweep(label);
            GameObject banner = UIFactory.CreatePanel(parent, "PhaseAnnouncement", ThemeService.IsDark
                ? new Color(0.035f, 0.02f, 0.16f, 0.96f)
                : new Color(0.98f, 0.97f, 0.86f, 0.97f));
            activeBanner = banner;
            RectTransform rect = banner.GetComponent<RectTransform>();
            UIFactory.SetAnchors(rect, new Vector2(0.20f, 0.435f), new Vector2(0.80f, 0.565f), Vector2.zero, Vector2.zero);
            CanvasGroup group = banner.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Text text = UIFactory.CreateText(banner.transform, label, 34, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.SetAnchors(text.rectTransform, new Vector2(0.04f, string.IsNullOrEmpty(caption) ? 0.08f : 0.38f), new Vector2(0.96f, string.IsNullOrEmpty(caption) ? 0.92f : 0.96f), Vector2.zero, Vector2.zero);
            if (!string.IsNullOrEmpty(caption))
            {
                Text captionText = UIFactory.CreateText(banner.transform, caption, 17, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
                UIFactory.SetAnchors(captionText.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.40f), Vector2.zero, Vector2.zero);
            }
            banner.transform.SetAsLastSibling();

            float travel = ThemeService.ReducedMotion ? 0.10f : 0.30f;
            float hold = ThemeService.ReducedMotion ? 0.40f : 2.00f;
            float exit = ThemeService.ReducedMotion ? 0.10f : 0.46f;
            float distance = Mathf.Max(900f, parent.rect.width * 0.92f);
            float elapsed = 0f;
            group.alpha = 1f;
            rect.anchoredPosition = new Vector2(-distance, 0f);
            while (elapsed < travel && banner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / travel));
                rect.anchoredPosition = new Vector2(Mathf.Lerp(-distance, 0f, t), 0f);
                yield return null;
            }

            if (banner != null)
            {
                rect.anchoredPosition = Vector2.zero;
                yield return new WaitForSecondsRealtime(hold);
            }

            elapsed = 0f;
            while (elapsed < exit && banner != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / exit));
                rect.anchoredPosition = new Vector2(Mathf.Lerp(0f, distance, t), 0f);
                group.alpha = 1f - t * 0.28f;
                yield return null;
            }
            if (banner != null) Destroy(banner);
            if (activeBanner == banner) activeBanner = null;
        }

        public void Clear()
        {
            if (activeBanner != null)
            {
                Destroy(activeBanner);
                activeBanner = null;
            }
        }

        private void OnDestroy() => Clear();
    }
}
