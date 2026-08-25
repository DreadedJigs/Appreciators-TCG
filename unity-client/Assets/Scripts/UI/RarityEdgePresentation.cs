using System;
using System.Collections.Generic;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    /// <summary>
    /// Resolution-independent card edges.  The lines are rendered by UGUI rather
    /// than baked into character art, keeping the faces clean at every card size.
    /// Mythic/Crown/1-of-1 frames add the only animated holographic treatment.
    /// </summary>
    public sealed class RarityEdgePresentation : MonoBehaviour
    {
        private const string RootName = "RarityEdgeFrame";
        private readonly List<List<Image>> lineBands = new List<List<Image>>();
        private bool holographic;
        private float phase;

        public static void Attach(GameObject cardCanvas, string rarity)
        {
            if (cardCanvas == null)
            {
                return;
            }

            RarityEdgePresentation presentation = cardCanvas.GetComponent<RarityEdgePresentation>() ??
                cardCanvas.AddComponent<RarityEdgePresentation>();
            presentation.Build(rarity);
        }

        private void Build(string rarity)
        {
            Transform existing = transform.Find(RootName);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            lineBands.Clear();
            holographic = IsMythic(rarity);
            phase = Mathf.Abs(GetInstanceID() % 211) * 0.031f;

            GameObject root = new GameObject(RootName, typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            root.transform.SetAsLastSibling();

            if (holographic)
            {
                AddLine(root.transform, new Color(0.04f, 0.03f, 0.18f, 1f), 1f, 3.0f);
                AddLine(root.transform, UIFactory.Accent, 0.96f, 6.0f);
                AddLine(root.transform, UIFactory.NeonCyan, 0.94f, 10.0f);
                AddLine(root.transform, UIFactory.NeonPink, 0.92f, 14.0f);
                AddCornerMarks(root.transform, UIFactory.Accent, 15.0f);
                return;
            }

            if (IsLegendary(rarity))
            {
                AddLine(root.transform, UIFactory.Ink, 1f, 3.0f);
                AddLine(root.transform, UIFactory.Accent, 0.98f, 7.0f);
                AddLine(root.transform, UIFactory.PortalViolet, 0.94f, 11.0f);
                AddCornerMarks(root.transform, UIFactory.Accent, 14.0f);
                return;
            }

            if (IsEpic(rarity))
            {
                AddLine(root.transform, UIFactory.Ink, 1f, 3.0f);
                AddLine(root.transform, UIFactory.Accent, 0.98f, 7.0f);
                AddCornerMarks(root.transform, UIFactory.Accent, 13.0f);
                return;
            }

            if (IsRare(rarity))
            {
                AddLine(root.transform, UIFactory.Ink, 1f, 3.0f);
                AddLine(root.transform, UIFactory.IceBadge, 0.96f, 7.0f);
                AddCornerMarks(root.transform, UIFactory.NeonCyan, 11.0f);
                return;
            }

            AddLine(root.transform, UIFactory.Ink, 1f, 3.0f);
            AddLine(root.transform, UIFactory.NeonCyan, 0.88f, 7.0f);
        }

        private void Update()
        {
            if (!holographic || lineBands.Count < 4)
            {
                return;
            }

            float t = Time.unscaledTime * 0.82f + phase;
            SetBandColor(1, Color.Lerp(UIFactory.Accent, UIFactory.NeonPink, Mathf.PingPong(t, 1f)));
            SetBandColor(2, Color.Lerp(UIFactory.NeonCyan, UIFactory.Accent, Mathf.PingPong(t + 0.33f, 1f)));
            SetBandColor(3, Color.Lerp(UIFactory.NeonPink, UIFactory.PortalViolet, Mathf.PingPong(t + 0.66f, 1f)));
        }

        private void AddLine(Transform parent, Color color, float alpha, float inset)
        {
            // Do not use a sliced Image here: some WebGL UI skins report no valid
            // sprite border, causing Unity to draw the whole card as a solid block.
            // Four thin strips are deterministic and leave the card face untouched.
            // Integer-width geometry holds up much better when the parent card is
            // gently rotated. Fractional 2.25px strips visibly shimmer on some
            // browser GPUs, especially at a hand-card scale.
            const float thickness = 2f;
            float corner = inset + thickness * 2f;
            Color lineColor = new Color(color.r, color.g, color.b, alpha);
            List<Image> band = new List<Image>(4)
            {
                CreateSegment(parent, "RarityLineTop", lineColor, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(corner, -inset - thickness), new Vector2(-corner, -inset)),
                CreateSegment(parent, "RarityLineBottom", lineColor, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(corner, inset), new Vector2(-corner, inset + thickness)),
                CreateSegment(parent, "RarityLineLeft", lineColor, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(inset, corner), new Vector2(inset + thickness, -corner)),
                CreateSegment(parent, "RarityLineRight", lineColor, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-inset - thickness, corner), new Vector2(-inset, -corner))
            };
            lineBands.Add(band);
        }

        private static Image CreateSegment(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject segment = new GameObject(name, typeof(RectTransform), typeof(Image));
            segment.transform.SetParent(parent, false);
            Image image = segment.GetComponent<Image>();
            image.useSpriteMesh = false;
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = segment.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return image;
        }

        private void SetBandColor(int index, Color color)
        {
            if (index < 0 || index >= lineBands.Count)
            {
                return;
            }

            foreach (Image image in lineBands[index])
            {
                if (image != null)
                {
                    image.color = color;
                }
            }
        }

        private static void AddCornerMarks(Transform parent, Color color, float inset)
        {
            Vector2[] anchors =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            foreach (Vector2 anchor in anchors)
            {
                GameObject mark = new GameObject("RarityCornerMark", typeof(RectTransform), typeof(Image));
                mark.transform.SetParent(parent, false);
                Image image = mark.GetComponent<Image>();
                image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
                image.color = color;
                image.raycastTarget = false;
                RectTransform rect = mark.GetComponent<RectTransform>();
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(anchor.x == 0f ? inset : -inset, anchor.y == 0f ? inset : -inset);
                rect.sizeDelta = new Vector2(6f, 6f);
            }
        }

        private static bool IsRare(string rarity) => string.Equals(rarity, GameConstants.Rare, StringComparison.OrdinalIgnoreCase);
        private static bool IsEpic(string rarity) => string.Equals(rarity, GameConstants.Epic, StringComparison.OrdinalIgnoreCase);
        private static bool IsLegendary(string rarity) => string.Equals(rarity, GameConstants.Legendary, StringComparison.OrdinalIgnoreCase);

        private static bool IsMythic(string rarity)
        {
            return string.Equals(rarity, GameConstants.Mythic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rarity, GameConstants.Crown, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rarity, GameConstants.OneOfOne, StringComparison.OrdinalIgnoreCase);
        }
    }
}
