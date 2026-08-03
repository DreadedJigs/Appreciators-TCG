using System.Collections;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public sealed class AppreciationLiquidMeter : MonoBehaviour
    {
        private Image liquid;
        private RectTransform wave;
        private Text valueText;
        private int maximum;
        private int currentValue = -1;
        private Coroutine animationRoutine;

        public void Configure(int maxValue, Color liquidColor, Rect nativePlaymatCrop)
        {
            maximum = Mathf.Max(1, maxValue);
            Image legacyVessel = gameObject.GetComponent<Image>();
            if (legacyVessel != null)
            {
                legacyVessel.enabled = false;
            }

            // Repaint the exact pixels from the printed Resource button instead
            // of laying a colored rectangle over it. As this duplicate fills,
            // the original star, burst, lettering, and rounded corners all move
            // with the Appreciation level and the button appears to fill itself.
            GameObject liquidObject = new GameObject("NativeAppreciationFill", typeof(RectTransform), typeof(Image));
            liquidObject.transform.SetParent(transform, false);
            liquid = liquidObject.GetComponent<Image>();
            liquid.sprite = UIFactory.LoadPlaymatSprite(nativePlaymatCrop);
            liquid.color = new Color(liquidColor.r, liquidColor.g, liquidColor.b, 0.52f);
            liquid.type = Image.Type.Filled;
            liquid.fillMethod = Image.FillMethod.Vertical;
            liquid.fillOrigin = 0;
            liquid.fillAmount = 0f;
            liquid.preserveAspect = false;
            liquid.raycastTarget = false;
            UIFactory.Stretch(liquidObject.GetComponent<RectTransform>());

            GameObject waveObject = new GameObject("LiquidSurface", typeof(RectTransform), typeof(Image));
            waveObject.transform.SetParent(transform, false);
            wave = waveObject.GetComponent<RectTransform>();
            Image waveImage = waveObject.GetComponent<Image>();
            waveImage.color = new Color(1f, 1f, 1f, 0.58f);
            waveImage.raycastTarget = false;

            // The score is printed directly into the native button footprint. It
            // has no backing panel, border, or detached HUD container.
            valueText = UIFactory.CreateText(transform, $"0/{maximum}", 27, TextAnchor.MiddleCenter, UIFactory.Cream, FontStyle.Bold);
            UIFactory.SetAnchors(valueText.rectTransform, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.64f), Vector2.zero, Vector2.zero);
            valueText.resizeTextForBestFit = true;
            valueText.resizeTextMinSize = 17;
            valueText.resizeTextMaxSize = 27;
            valueText.raycastTarget = false;
            Outline valueOutline = valueText.gameObject.AddComponent<Outline>();
            valueOutline.effectColor = new Color(0.02f, 0.02f, 0.16f, 0.96f);
            valueOutline.effectDistance = new Vector2(2f, -2f);
        }

        public void SetValue(int value, bool animate)
        {
            int safe = Mathf.Clamp(value, 0, maximum);
            float target = safe / (float)maximum;
            valueText.text = $"{safe}/{maximum}";
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            if (currentValue < 0 || !animate || ThemeService.ReducedMotion)
            {
                liquid.fillAmount = target;
                PositionWave(target, 0f);
            }
            else
            {
                animationRoutine = StartCoroutine(AnimateFill(liquid.fillAmount, target));
            }
            currentValue = safe;
        }

        private IEnumerator AnimateFill(float start, float target)
        {
            float elapsed = 0f;
            const float duration = 0.92f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                float amount = Mathf.Lerp(start, target, eased);
                liquid.fillAmount = amount;
                PositionWave(amount, Mathf.Sin(t * Mathf.PI * 6f) * (1f - t));
                yield return null;
            }
            liquid.fillAmount = target;
            PositionWave(target, 0f);
            animationRoutine = null;
        }

        private void PositionWave(float amount, float wobble)
        {
            float y = Mathf.Clamp01(amount);
            wave.gameObject.SetActive(y > 0.002f && y < 0.998f);
            wave.anchorMin = new Vector2(0.025f, y);
            wave.anchorMax = new Vector2(0.975f, y);
            wave.offsetMin = new Vector2(wobble * 3f, -2f);
            wave.offsetMax = new Vector2(wobble * 3f, 2f);
            wave.localRotation = Quaternion.Euler(0f, 0f, wobble * 4f);
        }
    }
}
