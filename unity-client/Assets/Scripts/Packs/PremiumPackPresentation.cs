using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.Packs
{
    /// <summary>
    /// Gives the sealed pack a calm product-display treatment. Motion is continuous
    /// and transform-only so changing pack art never flashes or rebuilds the UI.
    /// </summary>
    public sealed class PremiumPackPresentation : MonoBehaviour
    {
        private RectTransform packRect;
        private RectTransform sheenRect;
        private Image sheenImage;
        private Vector3 baseScale;
        private float cycleOffset;

        public void Configure(RectTransform foilSheen, Image foilSheenImage)
        {
            packRect = transform as RectTransform;
            sheenRect = foilSheen;
            sheenImage = foilSheenImage;
            baseScale = packRect == null ? Vector3.one : packRect.localScale;
            cycleOffset = Random.Range(0f, 1.75f);
        }

        private void Awake()
        {
            packRect = transform as RectTransform;
            baseScale = packRect == null ? Vector3.one : packRect.localScale;
        }

        private void OnDisable()
        {
            if (packRect != null)
            {
                packRect.localScale = baseScale;
                packRect.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (packRect == null)
            {
                return;
            }

            float time = Time.unscaledTime + cycleOffset;
            float breath = 1f + Mathf.Sin(time * 0.72f) * 0.006f;
            packRect.localScale = baseScale * breath;
            packRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 0.48f) * 0.22f);

            if (sheenRect == null || sheenImage == null)
            {
                return;
            }

            float sweep = Mathf.Repeat(time * 0.16f, 1f);
            float width = Mathf.Max(320f, packRect.rect.width);
            sheenRect.anchoredPosition = new Vector2(Mathf.Lerp(-width * 0.42f, width * 1.42f, sweep), 0f);

            float centerStrength = Mathf.Clamp01(1f - Mathf.Abs(sweep - 0.5f) * 3.4f);
            sheenImage.color = new Color(0.70f, 0.96f, 1f, centerStrength * 0.12f);
        }
    }
}
