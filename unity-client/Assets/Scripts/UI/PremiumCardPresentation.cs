using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    /// <summary>
    /// Adds a restrained physical-holo finish to every card without rebuilding or
    /// altering its collection artwork. All movement is continuous and overlay-only.
    /// </summary>
    public sealed class PremiumCardPresentation : MonoBehaviour
    {
        private const string FoilResourcePath = "Art/Official/CardMaterials/appreciators_holo_foil_v1";
        private static Sprite foilSprite;

        private RectTransform cardRect;
        private RectTransform foilRect;
        private RectTransform sheenRect;
        private Image foilImage;
        private Image sheenImage;
        private Image cyanEdge;
        private Image magentaEdge;
        private float intensity;
        private float cycleOffset;
        private Vector2 lastCardSize;

        public static PremiumCardPresentation Attach(GameObject cardCanvas, string rarity)
        {
            if (cardCanvas == null)
            {
                return null;
            }

            PremiumCardPresentation presentation = cardCanvas.GetComponent<PremiumCardPresentation>() ??
                cardCanvas.AddComponent<PremiumCardPresentation>();
            presentation.Build(rarity);
            return presentation;
        }

        private void Build(string rarity)
        {
            cardRect = transform as RectTransform;
            intensity = IntensityFor(rarity);
            cycleOffset = Mathf.Abs(GetInstanceID() % 997) / 137f;

            if (GetComponent<RectMask2D>() == null)
            {
                gameObject.AddComponent<RectMask2D>();
            }

            if (foilSprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(FoilResourcePath);
                if (texture != null)
                {
                    foilSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
                else
                {
                    Debug.LogError($"Missing premium card foil at Resources/{FoilResourcePath}.png");
                }
            }

            GameObject foil = new GameObject("HolographicFoil", typeof(RectTransform), typeof(Image));
            foil.transform.SetParent(transform, false);
            foilRect = foil.GetComponent<RectTransform>();
            foilRect.anchorMin = new Vector2(-0.035f, -0.025f);
            foilRect.anchorMax = new Vector2(1.035f, 1.025f);
            foilRect.offsetMin = Vector2.zero;
            foilRect.offsetMax = Vector2.zero;
            foilImage = foil.GetComponent<Image>();
            foilImage.sprite = foilSprite;
            foilImage.preserveAspect = false;
            foilImage.color = new Color(1f, 1f, 1f, intensity);
            foilImage.raycastTarget = false;

            GameObject sheen = new GameObject("CardSpecularSweep", typeof(RectTransform), typeof(Image));
            sheen.transform.SetParent(transform, false);
            sheenRect = sheen.GetComponent<RectTransform>();
            sheenRect.anchorMin = new Vector2(0f, 0.5f);
            sheenRect.anchorMax = new Vector2(0f, 0.5f);
            sheenRect.pivot = new Vector2(0.5f, 0.5f);
            sheenRect.sizeDelta = Vector2.zero;
            sheenRect.localRotation = Quaternion.Euler(0f, 0f, -17f);
            sheenImage = sheen.GetComponent<Image>();
            sheenImage.color = new Color(0.82f, 0.97f, 1f, 0f);
            sheenImage.raycastTarget = false;

            cyanEdge = CreateEdge("CyanFoilEdge", new Vector2(0.012f, 0.015f), new Vector2(0.024f, 0.985f), new Color(0.08f, 0.88f, 1f, intensity * 1.35f));
            magentaEdge = CreateEdge("MagentaFoilEdge", new Vector2(0.976f, 0.015f), new Vector2(0.988f, 0.985f), new Color(1f, 0.16f, 0.86f, intensity * 1.20f));
        }

        private Image CreateEdge(string objectName, Vector2 min, Vector2 max, Color color)
        {
            GameObject edge = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            edge.transform.SetParent(transform, false);
            RectTransform rect = edge.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = edge.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private void Update()
        {
            if (cardRect == null || foilImage == null)
            {
                return;
            }

            float time = Time.unscaledTime + cycleOffset;
            Vector2 cardSize = cardRect.rect.size;
            if (cardSize != lastCardSize)
            {
                // A fixed 38 x 720 sweep overwhelmed miniature hand/deck cards and
                // could read as a flashing block. Scale the highlight to the actual
                // rendered card so every presentation size shares the same finish.
                sheenRect.sizeDelta = new Vector2(
                    Mathf.Clamp(cardSize.x * 0.13f, 6f, 38f),
                    Mathf.Max(24f, cardSize.y * 1.45f));
                lastCardSize = cardSize;
            }

            // Hand, deck, and pack-fan cards are intentionally small. Moving foil
            // beneath their mask causes browser GPUs to shimmer along the upper and
            // lower edge as the card is resampled. Keep the finish present but
            // motionless at that scale; the full inspection view still animates.
            bool compactCard = cardSize.x < 260f || cardSize.y < 390f;
            if (compactCard)
            {
                foilRect.anchoredPosition = Vector2.zero;
                foilImage.color = new Color(1f, 1f, 1f, intensity);
                sheenImage.color = Color.clear;
                cyanEdge.color = new Color(0.08f, 0.88f, 1f, intensity * 1.15f);
                magentaEdge.color = new Color(1f, 0.16f, 0.86f, intensity);
                return;
            }

            float materialPulse = 0.84f + Mathf.Sin(time * 0.42f) * 0.12f;
            foilImage.color = new Color(1f, 1f, 1f, intensity * materialPulse);
            foilRect.anchoredPosition = new Vector2(Mathf.Sin(time * 0.24f) * 2.5f, Mathf.Cos(time * 0.19f) * 2f);

            float sweep = Mathf.Repeat(time * 0.115f, 1f);
            float smoothSweep = sweep * sweep * (3f - 2f * sweep);
            float width = Mathf.Max(1f, cardSize.x);
            sheenRect.anchoredPosition = new Vector2(Mathf.Lerp(-width * 0.32f, width * 1.32f, smoothSweep), 0f);
            float sweepStrength = Mathf.Sin(sweep * Mathf.PI);
            sheenImage.color = new Color(0.82f, 0.97f, 1f, sweepStrength * intensity * 0.82f);

            float edgePulse = 0.78f + Mathf.Sin(time * 0.55f) * 0.18f;
            cyanEdge.color = new Color(0.08f, 0.88f, 1f, intensity * 1.35f * edgePulse);
            magentaEdge.color = new Color(1f, 0.16f, 0.86f, intensity * 1.20f * (1.56f - edgePulse));
        }

        private static float IntensityFor(string rarity)
        {
            string value = (rarity ?? string.Empty).ToLowerInvariant();
            if (value.Contains("crown") || value.Contains("one of one")) return 0.14f;
            if (value.Contains("legendary")) return 0.12f;
            if (value.Contains("mythic")) return 0.10f;
            if (value.Contains("rare") || value.Contains("epic")) return 0.082f;
            if (value.Contains("uncommon")) return 0.066f;
            return 0.052f;
        }
    }
}
