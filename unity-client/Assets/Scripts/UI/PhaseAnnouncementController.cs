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

        public IEnumerator PlayPhase(BattleTurnPhase phase)
        {
            if (parent == null) yield break;
            Clear();
            string label = phase == BattleTurnPhase.GatherGrowth ? "GATHER GROWTH" :
                phase == BattleTurnPhase.Cycle ? "CYCLE" :
                phase == BattleTurnPhase.BuildOrDiscard ? "BUILD OR DISCARD" :
                phase == BattleTurnPhase.Discard ? "DISCARD PHASE" :
                phase == BattleTurnPhase.EndTurn ? "END TURN" : $"{phase.ToString().ToUpperInvariant()} PHASE";
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
            UIFactory.Stretch(text.rectTransform);
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
