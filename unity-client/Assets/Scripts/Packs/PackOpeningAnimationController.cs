using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using AppreciatorsTcg.UI;
using AppreciatorsTcg.Core;

namespace AppreciatorsTcg.Packs
{
    public class PackOpeningAnimationController : MonoBehaviour
    {
        private Coroutine packIdleRoutine;
        private RectTransform idlePack;
        private Vector2 idleBasePosition;
        private Quaternion idleBaseRotation;
        private Vector3 idleBaseScale = Vector3.one;
        private float packHoldIntensity;
        private Coroutine resultFloatRoutine;
        private readonly List<ResultCardFloatState> resultFloatCards = new List<ResultCardFloatState>();
        private RectTransform inspectedResultCard;

        private sealed class ResultCardFloatState
        {
            public RectTransform rect;
            public Vector2 position;
            public Quaternion rotation;
            public Vector3 scale;
            public float phase;
        }

        [Serializable]
        public class RarityRevealStyle
        {
            public Rarity rarity;
            public float revealDelay;
            public float glowStrength;
            public float screenShakeStrength;
        }

        // These timings remain the authoritative bridge between PackOpeningFlow and presentation.
        [SerializeField]
        private RarityRevealStyle[] rarityStyles =
        {
            new RarityRevealStyle { rarity = Rarity.Common, revealDelay = 0.16f, glowStrength = 0.1f },
            new RarityRevealStyle { rarity = Rarity.Uncommon, revealDelay = 0.18f, glowStrength = 0.25f },
            new RarityRevealStyle { rarity = Rarity.Rare, revealDelay = 0.24f, glowStrength = 0.5f, screenShakeStrength = 0.1f },
            new RarityRevealStyle { rarity = Rarity.Epic, revealDelay = 0.32f, glowStrength = 0.8f, screenShakeStrength = 0.2f },
            new RarityRevealStyle { rarity = Rarity.Legendary, revealDelay = 0.48f, glowStrength = 1f, screenShakeStrength = 0.35f }
        };

        public IEnumerator PlayPackEnterAnimation()
        {
            yield return PlayPackEnterAnimation(null);
        }

        public IEnumerator PlayPackEnterAnimation(RectTransform packVisual)
        {
            Debug.Log("Pack animation hook: pack enters the Appreciation Ritual.");
            if (packVisual == null)
            {
                yield return new WaitForSecondsRealtime(0.20f);
                yield break;
            }

            Vector2 targetPosition = packVisual.anchoredPosition;
            packVisual.anchoredPosition = targetPosition + new Vector2(0f, -82f);
            packVisual.localScale = Vector3.one * 0.70f;
            packVisual.localRotation = Quaternion.Euler(9f, -14f, 2f);
            yield return AnimateTransform(packVisual, targetPosition + new Vector2(0f, 8f), Vector3.one * 1.035f, Quaternion.Euler(-2f, 3f, -0.5f), 0.42f, true);
            yield return AnimateTransform(packVisual, targetPosition, Vector3.one, Quaternion.identity, 0.16f, false);
        }

        public void StartPackIdleAnimation(RectTransform packVisual)
        {
            if (packVisual == null)
            {
                return;
            }

            StopPackIdleAnimation(true);
            packVisual.localRotation = Quaternion.identity;
            idlePack = packVisual;
            idleBasePosition = packVisual.anchoredPosition;
            idleBaseRotation = packVisual.localRotation;
            idleBaseScale = packVisual.localScale;
            packHoldIntensity = 0f;
            packIdleRoutine = StartCoroutine(PackIdleLoop());
        }

        public void SetPackHoldIntensity(float progress)
        {
            packHoldIntensity = Mathf.Clamp01(progress);
        }

        public void StopPackIdleAnimation(bool resetTransform)
        {
            if (packIdleRoutine != null)
            {
                StopCoroutine(packIdleRoutine);
                packIdleRoutine = null;
            }

            if (resetTransform && idlePack != null)
            {
                idlePack.anchoredPosition = idleBasePosition;
                idlePack.localRotation = idleBaseRotation;
                idlePack.localScale = idleBaseScale;
            }

            idlePack = null;
            packHoldIntensity = 0f;
        }

        public void ResetPackOpenVisual(RectTransform packArt)
        {
            if (packArt == null) return;
            Transform glow = packArt.Find("PackInteriorGlow");
            if (glow != null) Destroy(glow.gameObject);
            Transform seam = packArt.Find("TornTopSeam");
            if (seam != null) Destroy(seam.gameObject);
        }

        public void StartResultCardFloat(IReadOnlyList<RectTransform> cards)
        {
            StopResultCardFloat();
            if (cards == null || cards.Count == 0)
            {
                return;
            }

            for (int i = 0; i < cards.Count; i++)
            {
                RectTransform card = cards[i];
                if (card == null)
                {
                    continue;
                }

                resultFloatCards.Add(new ResultCardFloatState
                {
                    rect = card,
                    position = card.anchoredPosition,
                    rotation = card.localRotation,
                    scale = card.localScale,
                    phase = i * 0.82f
                });
            }

            if (resultFloatCards.Count > 0)
            {
                resultFloatRoutine = StartCoroutine(ResultCardFloatLoop());
            }
        }

        public void SetInspectedResultCard(RectTransform card)
        {
            inspectedResultCard = card;
            if (inspectedResultCard != null)
            {
                inspectedResultCard.SetAsLastSibling();
            }
        }

        public void StopResultCardFloat()
        {
            if (resultFloatRoutine != null)
            {
                StopCoroutine(resultFloatRoutine);
                resultFloatRoutine = null;
            }

            inspectedResultCard = null;
            resultFloatCards.Clear();
        }

        public IEnumerator PlayPackBurstOpenAnimation(RectTransform packVisual, CanvasGroup flash)
        {
            Debug.Log("Pack animation hook: Appreciation Ritual pack bursts open.");
            StopPackIdleAnimation(true);
            if (flash != null)
            {
                flash.alpha = 0f;
            }

            if (packVisual == null)
            {
                if (flash != null)
                {
                    yield return FadeCanvasGroup(flash, 0f, 1f, 0.10f);
                    yield return FadeCanvasGroup(flash, 1f, 0f, 0.24f);
                }
                yield break;
            }

            Vector2 basePosition = packVisual.anchoredPosition;
            float elapsed = 0f;
            const float chargeDuration = 0.52f;
            while (elapsed < chargeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / chargeDuration);
                float shake = Mathf.Sin(t * Mathf.PI * 14f) * Mathf.Lerp(2f, 13f, t);
                packVisual.anchoredPosition = basePosition + new Vector2(shake, Mathf.Sin(t * Mathf.PI) * 22f);
                packVisual.localRotation = Quaternion.Euler(0f, 0f, shake * 0.38f);
                packVisual.localScale = Vector3.one * Mathf.Lerp(1f, 1.16f, t * t);
                yield return null;
            }

            if (flash != null)
            {
                yield return FadeCanvasGroup(flash, 0f, 1f, 0.11f);
            }

            yield return AnimateTransform(
                packVisual,
                basePosition + new Vector2(0f, 36f),
                Vector3.one * 1.42f,
                Quaternion.Euler(0f, 0f, -6f),
                0.16f,
                true);

            if (flash != null)
            {
                yield return FadeCanvasGroup(flash, 1f, 0f, 0.30f);
            }

            packVisual.anchoredPosition = basePosition;
            packVisual.localRotation = Quaternion.identity;
            packVisual.localScale = Vector3.one;
        }

        public IEnumerator PlayRightToLeftTearAnimation(RectTransform packArt, CanvasGroup flash, Rarity rarity)
        {
            StopPackIdleAnimation(true);
            if (packArt == null)
            {
                yield return new WaitForSecondsRealtime(ThemeService.ReducedMotion ? 0.12f : 0.72f);
                yield break;
            }

            GameObject glow = UIFactory.CreatePanel(packArt, "PackInteriorGlow", RarityGlow(rarity));
            RectTransform glowRect = glow.GetComponent<RectTransform>();
            UIFactory.SetAnchors(glowRect, new Vector2(1f, 0.84f), new Vector2(1f, 0.985f), Vector2.zero, Vector2.zero);
            glow.GetComponent<Image>().raycastTarget = false;

            GameObject tear = UIFactory.CreatePanel(packArt, "TornTopSeam", new Color(0.98f, 0.91f, 0.66f, 0.98f));
            RectTransform tearRect = tear.GetComponent<RectTransform>();
            UIFactory.SetAnchors(tearRect, new Vector2(0f, 0.91f), new Vector2(1f, 0.985f), Vector2.zero, Vector2.zero);
            tear.GetComponent<Image>().raycastTarget = false;
            tear.transform.SetAsLastSibling();

            float duration = ThemeService.ReducedMotion ? 0.18f : 1.05f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                glowRect.anchorMin = new Vector2(1f - eased, 0.84f);
                glowRect.offsetMin = Vector2.zero;
                glowRect.offsetMax = Vector2.zero;
                tearRect.localRotation = Quaternion.Euler(0f, Mathf.Lerp(0f, 12f, eased), Mathf.Lerp(0f, -3f, eased));
                tearRect.anchoredPosition = new Vector2(Mathf.Lerp(0f, -10f, eased), Mathf.Sin(eased * Mathf.PI) * 6f);
                packArt.localRotation = Quaternion.Euler(Mathf.Sin(eased * Mathf.PI) * -2.5f, Mathf.Lerp(4f, -3f, eased), 0f);
                if (flash != null) flash.alpha = Mathf.Sin(eased * Mathf.PI) * 0.18f;
                yield return null;
            }

            float fallDuration = ThemeService.ReducedMotion ? 0.08f : 0.42f;
            elapsed = 0f;
            Vector2 tearStart = tearRect.anchoredPosition;
            while (elapsed < fallDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                tearRect.anchoredPosition = tearStart + new Vector2(-28f * t, -92f * t * t);
                tearRect.localRotation = Quaternion.Euler(0f, 18f * t, -16f * t);
                tear.GetComponent<Image>().color = new Color(0.98f, 0.91f, 0.66f, 1f - t);
                yield return null;
            }

            Destroy(tear);
            packArt.localRotation = Quaternion.identity;
            if (flash != null) flash.alpha = 0f;
            Image glowImage = glow.GetComponent<Image>();
            glowImage.color = RarityGlow(rarity);
        }

        public IEnumerator PlayAcceleratingPackSpin(RectTransform packVisual, CanvasGroup flash)
        {
            StopPackIdleAnimation(true);
            if (packVisual == null)
            {
                yield return new WaitForSecondsRealtime(2f);
                yield break;
            }

            Vector2 startPosition = packVisual.anchoredPosition;
            Vector3 startScale = packVisual.localScale;
            float duration = ThemeService.ReducedMotion ? 0.65f : 2f;
            float forwardHold = ThemeService.ReducedMotion ? 0.08f : 0.20f;
            float elapsed = 0f;
            packVisual.localRotation = Quaternion.identity;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float spinT = Mathf.Clamp01((elapsed - forwardHold) / Mathf.Max(0.01f, duration - forwardHold));
                float acceleration = spinT * spinT * spinT;
                // Spin around the pack's upright axis so it turns like a standing card,
                // rather than rotating flat like a wheel.
                packVisual.localRotation = Quaternion.Euler(0f, -1440f * acceleration, 0f);
                packVisual.localScale = startScale * Mathf.Lerp(1f, 1.12f, t * t);
                packVisual.anchoredPosition = startPosition + new Vector2(0f, Mathf.Sin(t * Mathf.PI * (2f + 8f * t)) * (3f + 6f * t));
                yield return null;
            }

            packVisual.localScale = Vector3.zero;
            if (flash != null)
            {
                yield return PlayPackConfettiExplosion(flash);
            }

            packVisual.anchoredPosition = startPosition;
            packVisual.localRotation = Quaternion.identity;
            packVisual.localScale = startScale;
        }

        private IEnumerator PlayPackConfettiExplosion(CanvasGroup burstGroup)
        {
            if (burstGroup == null) yield break;

            RectTransform stage = burstGroup.GetComponent<RectTransform>();
            burstGroup.alpha = 1f;
            int count = ThemeService.ReducedMotion ? 18 : 58;
            float duration = ThemeService.ReducedMotion ? 0.24f : 0.72f;
            Color[] palette =
            {
                UIFactory.Accent,
                UIFactory.NeonCyan,
                UIFactory.NeonPink,
                UIFactory.PortalViolet,
                UIFactory.Cream,
                new Color(1f, 0.34f, 0.24f, 1f)
            };
            List<RectTransform> pieces = new List<RectTransform>(count);
            List<Vector2> velocities = new List<Vector2>(count);
            List<float> rotations = new List<float>(count);

            for (int index = 0; index < count; index++)
            {
                GameObject piece = new GameObject($"PackConfetti_{index}", typeof(RectTransform), typeof(Image));
                piece.transform.SetParent(stage, false);
                RectTransform rect = piece.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                float width = 11f + (index % 5) * 3f;
                float height = index % 3 == 0 ? width * 2.4f : width * 0.72f;
                rect.sizeDelta = new Vector2(width, height);
                float angle = index * 2.399963f;
                float speed = 210f + (index % 11) * 23f;
                velocities.Add(new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed + 92f));
                rotations.Add((index % 2 == 0 ? 1f : -1f) * (240f + (index % 7) * 55f));
                Image image = piece.GetComponent<Image>();
                image.color = palette[index % palette.Length];
                image.raycastTarget = false;
                pieces.Add(rect);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - Mathf.SmoothStep(0.56f, 1f, t);
                for (int index = 0; index < pieces.Count; index++)
                {
                    RectTransform rect = pieces[index];
                    Vector2 velocity = velocities[index];
                    Vector2 position = velocity * elapsed + Vector2.down * (310f * elapsed * elapsed);
                    rect.anchoredPosition = position;
                    rect.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * Mathf.PI * 4f + index) * 48f, rotations[index] * elapsed);
                    Image image = rect.GetComponent<Image>();
                    Color color = image.color;
                    color.a = fade;
                    image.color = color;
                }
                yield return null;
            }

            foreach (RectTransform piece in pieces)
            {
                if (piece != null) Destroy(piece.gameObject);
            }
            burstGroup.alpha = 0f;
        }

        public IEnumerator PlayAppreciateCelebration(RectTransform stage, Rarity rarity)
        {
            if (stage == null) yield break;

            GameObject banner = UIFactory.CreatePanel(stage, "AppreciateBanner", new Color(0.055f, 0.025f, 0.19f, 0.97f));
            UIFactory.AddNeonFrame(banner, RarityGlow(rarity), 0.92f);
            RectTransform bannerRect = banner.GetComponent<RectTransform>();
            UIFactory.SetAnchors(bannerRect, new Vector2(0.18f, 0.76f), new Vector2(0.82f, 0.91f), Vector2.zero, Vector2.zero);
            Text bannerText = UIFactory.CreateText(banner.transform, "APPRECIATE", 38, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.Stretch(bannerText.rectTransform, 8f);
            bannerText.resizeTextForBestFit = true;
            bannerText.resizeTextMinSize = 24;
            bannerText.resizeTextMaxSize = 42;
            bannerText.raycastTarget = false;
            banner.transform.SetAsLastSibling();

            Vector2 target = bannerRect.anchoredPosition;
            bannerRect.anchoredPosition = target + new Vector2(0f, -48f);
            bannerRect.localScale = new Vector3(0.74f, 0.18f, 1f);
            yield return AnimateTransform(bannerRect, target + new Vector2(0f, 5f), Vector3.one * 1.05f, Quaternion.identity, ThemeService.ReducedMotion ? 0.10f : 0.34f, true);
            yield return AnimateTransform(bannerRect, target, Vector3.one, Quaternion.identity, ThemeService.ReducedMotion ? 0.06f : 0.14f, false);

            int particleCount = ThemeService.ReducedMotion ? 8 : 28;
            List<RectTransform> particles = new List<RectTransform>();
            for (int i = 0; i < particleCount; i++)
            {
                Text particle = UIFactory.CreateText(stage, i % 3 == 0 ? "★" : i % 3 == 1 ? "✦" : "◆", 18, TextAnchor.MiddleCenter,
                    i % 2 == 0 ? UIFactory.Accent : UIFactory.NeonCyan, FontStyle.Bold);
                RectTransform rect = particle.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2((i + 0.5f) / particleCount, 1.03f + (i % 4) * 0.04f);
                rect.sizeDelta = new Vector2(24f, 24f);
                rect.anchoredPosition = Vector2.zero;
                particle.raycastTarget = false;
                particles.Add(rect);
            }

            float confettiDuration = ThemeService.ReducedMotion ? 0.35f : 3f;
            float elapsed = 0f;
            while (elapsed < confettiDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / confettiDuration);
                for (int i = 0; i < particles.Count; i++)
                {
                    RectTransform rect = particles[i];
                    if (rect == null) continue;
                    float drift = Mathf.Sin(t * Mathf.PI * 4f + i) * (8f + i % 5 * 3f);
                    rect.anchoredPosition = new Vector2(drift, -stage.rect.height * (0.18f + 0.82f * t) - (i % 4) * 18f);
                    rect.localRotation = Quaternion.Euler(0f, 0f, t * 240f * (i % 2 == 0 ? 1f : -1f));
                }
                yield return null;
            }

            foreach (RectTransform particle in particles) if (particle != null) Destroy(particle.gameObject);
            yield return new WaitForSecondsRealtime(ThemeService.ReducedMotion ? 0.05f : 0.18f);
            Destroy(banner);
        }

        private static Color RarityGlow(Rarity rarity)
        {
            if (rarity >= Rarity.Legendary) return new Color(1f, 0.72f, 0.18f, 0.82f);
            if (rarity >= Rarity.Epic) return new Color(0.72f, 0.28f, 1f, 0.78f);
            if (rarity >= Rarity.Rare) return new Color(0.12f, 0.78f, 1f, 0.74f);
            return new Color(0.30f, 0.94f, 0.72f, 0.62f);
        }

        public IEnumerator PlaySealBreakAnimation(Lane lane)
        {
            yield return PlaySealBreakAnimation(lane, null);
        }

        public IEnumerator PlaySealBreakAnimation(Lane lane, RectTransform sealVisual)
        {
            Debug.Log($"Pack animation hook: {lane} Seal breaks.");
            if (sealVisual == null)
            {
                yield return new WaitForSecondsRealtime(0.24f);
                yield break;
            }

            Vector2 startPosition = sealVisual.anchoredPosition;
            Quaternion startRotation = sealVisual.localRotation;
            yield return AnimateTransform(
                sealVisual,
                startPosition,
                Vector3.one * 1.16f,
                Quaternion.Euler(0f, 0f, lane == Lane.Community ? -7f : 7f),
                0.12f,
                false);
            yield return AnimateTransform(sealVisual, startPosition, Vector3.one, startRotation, 0.16f, false);
        }

        public IEnumerator PlayCardRevealAnimation(CardDefinition card, int slotIndex)
        {
            yield return PlayCardRevealAnimation(card, slotIndex, null);
        }

        public IEnumerator PlayCardRevealAnimation(CardDefinition card, int slotIndex, RectTransform cardVisual)
        {
            RarityRevealStyle style = ResolveStyle(card?.rarity ?? Rarity.Common);
            Debug.Log($"Pack animation hook: reveal slot {slotIndex} - {card?.name ?? "Unknown Card"}; glow {style.glowStrength:0.00}, shake {style.screenShakeStrength:0.00}.");
            if (cardVisual == null)
            {
                yield return new WaitForSecondsRealtime(style.revealDelay);
                yield break;
            }

            StartCoroutine(PlayRarityBurst(cardVisual.parent as RectTransform, card?.rarity ?? Rarity.Common, style.glowStrength));

            cardVisual.anchoredPosition = new Vector2(0f, -172f);
            cardVisual.localScale = Vector3.one * 0.70f;
            cardVisual.localRotation = Quaternion.Euler(-7f, slotIndex % 2 == 0 ? 82f : -82f, slotIndex % 2 == 0 ? -5f : 5f);
            yield return AnimateTransform(cardVisual, new Vector2(0f, 24f), Vector3.one * 1.055f, Quaternion.Euler(1f, -3f, 0f), Mathf.Max(0.48f, style.revealDelay), true);
            yield return AnimateTransform(cardVisual, new Vector2(0f, 16f), Vector3.one, Quaternion.identity, 0.14f, false);
        }

        public IEnumerator PlayMysteryRevealAnimation(CardDefinition card)
        {
            yield return PlayMysteryRevealAnimation(card, null);
        }

        public IEnumerator PlayMysteryRevealAnimation(CardDefinition card, RectTransform cardVisual)
        {
            RarityRevealStyle style = ResolveStyle(card?.rarity ?? Rarity.Common);
            Debug.Log($"Pack animation hook: final mystery reveal - {card?.name ?? "Unknown Card"}; premium glow {style.glowStrength:0.00}, shake {style.screenShakeStrength:0.00}.");
            if (cardVisual == null)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.32f, style.revealDelay));
                yield break;
            }


            StartCoroutine(PlayRarityBurst(cardVisual.parent as RectTransform, card?.rarity ?? Rarity.Common, Mathf.Max(0.72f, style.glowStrength)));

            cardVisual.anchoredPosition = new Vector2(0f, -188f);
            cardVisual.localScale = Vector3.one * 0.66f;
            cardVisual.localRotation = Quaternion.Euler(-9f, -88f, -7f);
            float revealDuration = Mathf.Max(0.42f, style.revealDelay);
            yield return AnimateTransform(cardVisual, new Vector2(0f, 20f), Vector3.one * 1.07f, Quaternion.Euler(0f, 0f, 2f), revealDuration, true);
            yield return AnimateTransform(cardVisual, new Vector2(0f, 16f), Vector3.one, Quaternion.identity, 0.16f, false);
        }

        public void ShowCardImmediately(RectTransform cardVisual)
        {
            if (cardVisual == null)
            {
                return;
            }

            cardVisual.anchoredPosition = new Vector2(0f, 16f);
            cardVisual.localScale = Vector3.one;
            cardVisual.localRotation = Quaternion.identity;
        }

        public IEnumerator PlayRarityBannerAnimation(RectTransform banner, CanvasGroup group, Rarity rarity, bool fast)
        {
            Debug.Log($"Pack animation hook: {rarity} rarity banner enters above the revealed card.");
            if (banner == null || group == null)
            {
                yield break;
            }

            banner.gameObject.SetActive(true);
            Vector2 basePosition = banner.anchoredPosition;
            banner.anchoredPosition = basePosition + new Vector2(0f, 22f);
            banner.localScale = new Vector3(0.62f, 0.78f, 1f);
            group.alpha = 0f;

            float enterDuration = fast ? 0.08f : 0.18f;
            float elapsed = 0f;
            while (elapsed < enterDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / enterDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                banner.anchoredPosition = Vector2.Lerp(basePosition + new Vector2(0f, 22f), basePosition, eased);
                banner.localScale = Vector3.Lerp(new Vector3(0.62f, 0.78f, 1f), Vector3.one * (rarity >= Rarity.Rare ? 1.06f : 1f), eased);
                group.alpha = eased;
                yield return null;
            }

            banner.anchoredPosition = basePosition;
            banner.localScale = Vector3.one;
            group.alpha = 1f;
            yield return new WaitForSecondsRealtime(fast ? 0.14f : rarity >= Rarity.Rare ? 0.78f : 0.52f);

            float exitDuration = fast ? 0.08f : 0.16f;
            elapsed = 0f;
            while (elapsed < exitDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / exitDuration);
                banner.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.12f, 0.82f, 1f), t);
                group.alpha = 1f - t;
                yield return null;
            }

            group.alpha = 0f;
            banner.localScale = Vector3.one;
            banner.anchoredPosition = basePosition;
            banner.gameObject.SetActive(false);
        }

        public IEnumerator PlayCardArchiveAnimation(RectTransform cardVisual, int slotIndex, int totalCards, bool fast)
        {
            if (cardVisual == null)
            {
                yield break;
            }

            int safeTotal = Mathf.Max(1, totalCards);
            float spacing = safeTotal <= 1 ? 0f : Mathf.Min(132f, 620f / (safeTotal - 1));
            float startX = -spacing * (safeTotal - 1) * 0.5f;
            Vector2 target = new Vector2(startX + spacing * slotIndex, -205f);
            float angle = (slotIndex - (safeTotal - 1) * 0.5f) * -3.5f;
            yield return AnimateTransform(
                cardVisual,
                target,
                Vector3.one * 0.40f,
                Quaternion.Euler(0f, 0f, angle),
                fast ? 0.08f : 0.24f,
                true);
        }

        public IEnumerator PlayDuplicateConvertAnimation(CardDefinition card, int shards)
        {
            Debug.Log($"Pack animation hook: duplicate {card?.name ?? "Unknown Card"} converts into {shards} Appreciation Shards.");
            yield return new WaitForSeconds(0.14f);
        }

        public IEnumerator PlaySummaryAnimation()
        {
            Debug.Log("Pack animation hook: Appreciation Ritual summary enters.");
            yield return new WaitForSeconds(0.18f);
        }

        public IEnumerator PlayFinalCardFanAnimation(IReadOnlyList<RectTransform> cards, IReadOnlyList<Rarity> rarities)
        {
            if (cards == null || cards.Count == 0) yield break;

            List<int> order = Enumerable.Range(0, cards.Count).ToList();
            int heroIndex = 0;
            if (rarities != null && rarities.Count == cards.Count)
            {
                heroIndex = order.OrderByDescending(index => rarities[index]).First();
            }
            order.Remove(heroIndex);
            order.Insert(order.Count / 2, heroIndex);

            float center = (order.Count - 1) * 0.5f;
            float spacing = order.Count <= 1 ? 0f : Mathf.Min(122f, 620f / (order.Count - 1));
            for (int fanSlot = 0; fanSlot < order.Count; fanSlot++)
            {
                RectTransform card = cards[order[fanSlot]];
                if (card == null) continue;
                float delta = fanSlot - center;
                bool hero = order[fanSlot] == heroIndex;
                Vector2 target = new Vector2(delta * spacing, hero ? 8f : -18f - Mathf.Abs(delta) * 9f);
                float scale = hero ? 0.66f : Mathf.Lerp(0.54f, 0.46f, Mathf.Abs(delta) / Mathf.Max(1f, center));
                // A 3D yaw makes thin card borders resample every frame in the
                // browser. The final fan remains dimensional through overlap and
                // depth, but uses a restrained 2D rotation for crisp edges.
                Quaternion rotation = Quaternion.Euler(0f, 0f, -delta * 2.25f);
                card.SetAsLastSibling();
                StartCoroutine(AnimateTransform(card, target, Vector3.one * scale, rotation, ThemeService.ReducedMotion ? 0.12f : 0.46f, true));
            }

            if (heroIndex >= 0 && heroIndex < cards.Count && cards[heroIndex] != null)
            {
                cards[heroIndex].SetAsLastSibling();
            }

            yield return new WaitForSecondsRealtime(ThemeService.ReducedMotion ? 0.14f : 0.52f);
        }

        private IEnumerator PlayRarityBurst(RectTransform parent, Rarity rarity, float strength)
        {
            if (parent == null) yield break;
            GameObject root = new GameObject("RarityBurst", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(520f, 520f);
            rootRect.anchoredPosition = new Vector2(0f, 18f);
            root.transform.SetAsFirstSibling();
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Color glow = RarityGlow(rarity);
            int rayCount = ThemeService.ReducedMotion ? 8 : 18;
            for (int i = 0; i < rayCount; i++)
            {
                GameObject ray = UIFactory.CreatePanel(root.transform, $"Ray_{i}", glow);
                RectTransform rayRect = ray.GetComponent<RectTransform>();
                rayRect.anchorMin = rayRect.anchorMax = new Vector2(0.5f, 0.5f);
                rayRect.pivot = new Vector2(0f, 0.5f);
                rayRect.sizeDelta = new Vector2(i % 2 == 0 ? 235f : 175f, i % 3 == 0 ? 9f : 5f);
                rayRect.anchoredPosition = Vector2.zero;
                rayRect.localRotation = Quaternion.Euler(0f, 0f, i * (360f / rayCount));
                ray.GetComponent<Image>().raycastTarget = false;
            }

            float duration = ThemeService.ReducedMotion ? 0.24f : 0.82f;
            float elapsed = 0f;
            rootRect.localScale = Vector3.one * 0.18f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float expand = 1f - Mathf.Pow(1f - t, 3f);
                rootRect.localScale = Vector3.one * Mathf.Lerp(0.18f, 1.18f + strength * 0.16f, expand);
                rootRect.localRotation = Quaternion.Euler(0f, 0f, t * 13f);
                group.alpha = Mathf.Sin(t * Mathf.PI) * Mathf.Lerp(0.52f, 0.92f, strength);
                yield return null;
            }
            Destroy(root);
        }

        private RarityRevealStyle ResolveStyle(Rarity rarity)
        {
            return rarityStyles?.FirstOrDefault(style => style != null && style.rarity == rarity)
                ?? new RarityRevealStyle { rarity = rarity, revealDelay = 0.18f, glowStrength = 0.1f };
        }

        private static IEnumerator AnimateTransform(
            RectTransform target,
            Vector2 toPosition,
            Vector3 toScale,
            Quaternion toRotation,
            float duration,
            bool easeOut)
        {
            if (target == null)
            {
                yield break;
            }

            Vector2 fromPosition = target.anchoredPosition;
            Vector3 fromScale = target.localScale;
            Quaternion fromRotation = target.localRotation;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                if (easeOut)
                {
                    t = 1f - Mathf.Pow(1f - t, 3f);
                }
                else
                {
                    t = t * t * (3f - 2f * t);
                }

                target.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
                target.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
                target.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, t);
                yield return null;
            }

            target.anchoredPosition = toPosition;
            target.localScale = toScale;
            target.localRotation = toRotation;
        }

        private IEnumerator PackIdleLoop()
        {
            float elapsed = 0f;
            while (idlePack != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float intensity = packHoldIntensity * packHoldIntensity;
                float verticalSpeed = Mathf.Lerp(1.10f, 1.35f, intensity);
                float horizontalSpeed = Mathf.Lerp(0.68f, 0.90f, intensity);
                float wave = Mathf.Sin(elapsed * verticalSpeed);
                float slowWave = Mathf.Sin(elapsed * horizontalSpeed);
                float horizontalAmplitude = Mathf.Lerp(3f, 5f, intensity);
                float verticalAmplitude = Mathf.Lerp(7f, 10f, intensity);
                idlePack.anchoredPosition = idleBasePosition + new Vector2(
                    slowWave * horizontalAmplitude,
                    wave * verticalAmplitude);
                idlePack.localRotation = Quaternion.identity;
                float pulse = Mathf.Sin(elapsed * Mathf.Lerp(1.05f, 1.55f, intensity));
                idlePack.localScale = idleBaseScale * (1f + pulse * Mathf.Lerp(0.008f, 0.018f, intensity) + intensity * 0.015f);
                yield return null;
            }

            packIdleRoutine = null;
        }

        private IEnumerator ResultCardFloatLoop()
        {
            float elapsed = 0f;
            while (resultFloatCards.Count > 0)
            {
                elapsed += Time.unscaledDeltaTime;
                float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 8f);
                foreach (ResultCardFloatState state in resultFloatCards)
                {
                    if (state?.rect == null)
                    {
                        continue;
                    }

                    bool inspected = state.rect == inspectedResultCard;
                    float wave = Mathf.Sin(elapsed * 1.45f + state.phase);
                    Vector2 targetPosition = inspected
                        ? new Vector2(0f, -12f + wave * 8f)
                        : state.position + new Vector2(0f, wave * 7f);
                    Vector3 targetScale = inspected
                        ? Vector3.one * 0.78f
                        : state.scale * (1f + wave * 0.025f);
                    Quaternion targetRotation = inspected
                        ? Quaternion.identity
                        : state.rotation * Quaternion.Euler(0f, 0f, wave * 1.4f);

                    state.rect.anchoredPosition = Vector2.Lerp(state.rect.anchoredPosition, targetPosition, blend);
                    state.rect.localScale = Vector3.Lerp(state.rect.localScale, targetScale, blend);
                    state.rect.localRotation = Quaternion.Slerp(state.rect.localRotation, targetRotation, blend);
                }

                yield return null;
            }

            resultFloatRoutine = null;
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);
            group.alpha = from;
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                group.alpha = Mathf.Lerp(from, to, t * t * (3f - 2f * t));
                yield return null;
            }

            group.alpha = to;
        }

        private void OnDisable()
        {
            StopPackIdleAnimation(false);
            StopResultCardFloat();
        }
    }
}
