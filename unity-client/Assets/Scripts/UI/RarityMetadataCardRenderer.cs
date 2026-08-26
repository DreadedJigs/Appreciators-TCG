using System;
using System.Collections.Generic;
using System.Linq;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using AppreciatorsTcg.Data;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    /// <summary>
    /// Builds a metadata face only when a card has no authored production face.
    /// BakedCardFace objects are complete card compositions and must remain intact;
    /// rebuilding them at runtime makes the artwork and labels look pasted over.
    /// </summary>
    public sealed class RarityMetadataCardRenderer : MonoBehaviour
    {
        private const float ScanInterval = 0.25f;
        private static readonly HashSet<int> Processed = new HashSet<int>();
        private static Dictionary<string, CardDefinition> cardsByName;
        private float nextScanAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRuntimeRenderer()
        {
            if (UnityEngine.Object.FindFirstObjectByType<RarityMetadataCardRenderer>() != null)
            {
                return;
            }

            GameObject host = new GameObject("RarityMetadataCardRenderer");
            DontDestroyOnLoad(host);
            host.AddComponent<RarityMetadataCardRenderer>();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextScanAt)
            {
                return;
            }

            nextScanAt = Time.unscaledTime + ScanInterval;
            foreach (Image image in Resources.FindObjectsOfTypeAll<Image>())
            {
                bool fallbackFace = image != null && image.gameObject.name == "OfficialCardCanvas";
                if (!fallbackFace || Processed.Contains(image.GetInstanceID()))
                {
                    continue;
                }

                CardDefinition card = FindCardFor(image.transform);
                if (card == null)
                {
                    continue;
                }

                Processed.Add(image.GetInstanceID());
                Build(image, card);
            }
        }

        private static CardDefinition FindCardFor(Transform transform)
        {
            if (cardsByName == null)
            {
                cardsByName = CardCatalog.AllCards
                    .Where(card => card != null && !string.IsNullOrWhiteSpace(card.name))
                    .GroupBy(card => card.name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            }

            for (Transform candidate = transform; candidate != null; candidate = candidate.parent)
            {
                if (cardsByName.TryGetValue(candidate.name, out CardDefinition card))
                {
                    return card;
                }
            }

            return null;
        }

        private static void Build(Image canvasImage, CardDefinition card)
        {
            foreach (Transform child in canvasImage.transform)
            {
                Destroy(child.gameObject);
            }

            canvasImage.name = "LiveMetadataCardFace";
            canvasImage.raycastTarget = false;
            if (!UIAssetPack.ApplyResource(canvasImage, FramePath(card.rarity), false))
            {
                canvasImage.color = UIFactory.Ink;
            }

            RectTransform canvas = canvasImage.rectTransform;
            Color accent = AccentFor(card.rarity);

            GameObject artWindow = NewSurface(canvas, "MetadataArtWindow", new Color(0.015f, 0.025f, 0.08f, 0.96f), new Vector2(0.030f, 0.305f), new Vector2(0.935f, 0.855f), true);
            Sprite art = CardArtResolver.LoadSprite(card);
            if (art != null)
            {
                AddFittedImage(artWindow.transform, "MetadataArt", art);
            }

            GameObject id = NewSurface(canvas, "CardIdPlate", UIFactory.Ink, new Vector2(0.030f, 0.790f), new Vector2(0.400f, 0.860f));
            AddText(id.transform, $"# {card.id.ToUpperInvariant()}", 17, TextAnchor.MiddleCenter, accent, FontStyle.Bold, Vector2.zero, Vector2.one);

            GameObject rarity = NewSurface(canvas, "RarityPlate", UIFactory.Ink, new Vector2(0.720f, 0.872f), new Vector2(0.972f, 0.982f));
            AddText(rarity.transform, $"{(card.rarity ?? GameConstants.Common).ToUpperInvariant()}\n{Stars(card.rarity)}", 15, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one);

            GameObject identity = NewSurface(canvas, "IdentityPlate", new Color(UIFactory.Ink.r, UIFactory.Ink.g, UIFactory.Ink.b, 0.94f), new Vector2(0.030f, 0.610f), new Vector2(0.225f, 0.775f));
            AddText(identity.transform, $"PILLAR\n{card.GetPillar().ToUpperInvariant()}\n{card.GetArchetype().ToUpperInvariant()}", 12, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold, Vector2.zero, Vector2.one);

            GameObject title = NewSurface(canvas, "LiveCardNamePlate", UIFactory.Ink, new Vector2(0.030f, 0.185f), new Vector2(0.700f, 0.295f));
            AddText(title.transform, (card.name ?? "CARD").ToUpperInvariant(), 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold, new Vector2(0.04f, 0.24f), new Vector2(0.96f, 0.96f));
            AddText(title.transform, $"{(card.type ?? "CARD").ToUpperInvariant()}  •  {card.GetArchetype().ToUpperInvariant()}", 10, TextAnchor.MiddleCenter, accent, FontStyle.Bold, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.27f));

            AddStat(canvas, "AttackStat", "ATK", card.GetAttack().ToString(), UIFactory.HeartRed, new Vector2(0.720f, 0.335f), new Vector2(0.972f, 0.425f));
            AddStat(canvas, "DefenseStat", "DEF", card.GetDefense().ToString(), UIFactory.Blue, new Vector2(0.720f, 0.245f), new Vector2(0.972f, 0.335f));
            AddStat(canvas, "PillarStat", card.GetPillar().ToUpperInvariant(), UIFactory.PillarSymbol(card.GetPillar()), accent, new Vector2(0.720f, 0.155f), new Vector2(0.972f, 0.245f));

            GameObject rules = NewSurface(canvas, "LiveEffectsPlate", UIFactory.Ink, new Vector2(0.030f, 0.025f), new Vector2(0.972f, 0.178f));
            AddText(rules.transform, $"BUILD  {ShortRule(card.GetBuildEffect(), 96)}", 11, TextAnchor.MiddleLeft, Color.white, FontStyle.Normal, new Vector2(0.04f, 0.50f), new Vector2(0.96f, 0.96f));
            AddText(rules.transform, $"DISCARD  {ShortRule(card.GetDiscardEffect(), 96)}", 11, TextAnchor.MiddleLeft, accent, FontStyle.Normal, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.50f));
        }

        private static GameObject NewSurface(Transform parent, string name, Color color, Vector2 min, Vector2 max, bool mask = false)
        {
            GameObject surface = new GameObject(name, typeof(RectTransform), typeof(Image));
            surface.transform.SetParent(parent, false);
            Image image = surface.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (mask)
            {
                surface.AddComponent<RectMask2D>();
            }

            RectTransform rect = surface.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return surface;
        }

        private static void AddFittedImage(Transform parent, string name, Sprite sprite)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            AspectRatioFitter fitter = imageObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        private static void AddStat(Transform parent, string name, string label, string value, Color color, Vector2 min, Vector2 max)
        {
            GameObject stat = NewSurface(parent, name, UIFactory.Ink, min, max);
            AddText(stat.transform, $"{label}\n{value}", 18, TextAnchor.MiddleCenter, color, FontStyle.Bold, Vector2.zero, Vector2.one);
        }

        private static Text AddText(Transform parent, string value, int fontSize, TextAnchor alignment, Color color, FontStyle style, Vector2 min, Vector2 max)
        {
            GameObject textObject = new GameObject("MetadataText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = UIFactory.DefaultFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 4;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        private static string FramePath(string rarity)
        {
            if (string.Equals(rarity, GameConstants.Common, StringComparison.OrdinalIgnoreCase) || string.Equals(rarity, GameConstants.Uncommon, StringComparison.OrdinalIgnoreCase)) return "Art/Official/CardTemplate/rarity_frames/common_card_background";
            if (string.Equals(rarity, GameConstants.Rare, StringComparison.OrdinalIgnoreCase)) return "Art/Official/CardTemplate/rarity_frames/rare_card_background";
            if (string.Equals(rarity, GameConstants.Epic, StringComparison.OrdinalIgnoreCase)) return "Art/Official/CardTemplate/rarity_frames/epic_card_background";
            return "Art/Official/CardTemplate/rarity_frames/legendary_card_background";
        }

        private static Color AccentFor(string rarity)
        {
            if (string.Equals(rarity, GameConstants.Common, StringComparison.OrdinalIgnoreCase) || string.Equals(rarity, GameConstants.Uncommon, StringComparison.OrdinalIgnoreCase)) return UIFactory.NeonCyan;
            if (string.Equals(rarity, GameConstants.Rare, StringComparison.OrdinalIgnoreCase)) return UIFactory.IceBadge;
            if (string.Equals(rarity, GameConstants.Epic, StringComparison.OrdinalIgnoreCase)) return UIFactory.Accent;
            return UIFactory.NeonPink;
        }

        private static string Stars(string rarity)
        {
            if (string.Equals(rarity, GameConstants.Common, StringComparison.OrdinalIgnoreCase) || string.Equals(rarity, GameConstants.Uncommon, StringComparison.OrdinalIgnoreCase)) return "★";
            if (string.Equals(rarity, GameConstants.Rare, StringComparison.OrdinalIgnoreCase)) return "★★";
            if (string.Equals(rarity, GameConstants.Epic, StringComparison.OrdinalIgnoreCase)) return "★★★";
            return "★★★★★";
        }

        private static string ShortRule(string rule, int maximumLength)
        {
            string text = string.IsNullOrWhiteSpace(rule) ? "No additional effect." : rule.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= maximumLength ? text : text.Substring(0, Mathf.Max(1, maximumLength - 1)).TrimEnd() + "…";
        }
    }
}
