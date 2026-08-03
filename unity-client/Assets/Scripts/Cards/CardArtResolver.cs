using System;
using System.Collections.Generic;
using AppreciatorsTcg.Core;
using UnityEngine;

namespace AppreciatorsTcg.Cards
{
    public static class CardArtResolver
    {
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static Sprite LoadSprite(CardDefinition card)
        {
            if (card == null)
            {
                return null;
            }

            string artPath = card.EffectiveArtPath();
            Sprite configured = LoadSpriteAtPath(artPath);
            if (configured != null)
            {
                return configured;
            }

            return LoadSpriteAtPath(PlaceholderPath(card.type));
        }

        public static Sprite LoadCardFaceSprite(CardDefinition card)
        {
            return card == null ? null : LoadFullSpriteAtPath($"Art/Official/GeneratedCards/{card.id}");
        }

        public static bool HasFinalArt(CardDefinition card)
        {
            return card != null && Resources.Load<Texture2D>(card.EffectiveArtPath()) != null;
        }

        public static string PlaceholderPath(string cardType)
        {
            string safeType = string.IsNullOrWhiteSpace(cardType) ? "card" : cardType.ToLowerInvariant();
            if (safeType == GameConstants.Item.ToLowerInvariant())
            {
                return "Art/Placeholder/placeholder_background";
            }

            if (safeType == GameConstants.Event.ToLowerInvariant())
            {
                return "Art/Placeholder/placeholder_trait";
            }

            return $"Art/Placeholder/placeholder_{safeType}";
        }

        private static Sprite LoadSpriteAtPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (SpriteCache.TryGetValue(path, out Sprite cached))
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 4;

            // The first official mock drops are complete portrait card sheets. Runtime
            // cards use only their illustration window so the approved game rules stay
            // authoritative and sample text on the reference sheet is never displayed.
            Rect spriteRect = new Rect(0, 0, texture.width, texture.height);
            if (texture.height >= texture.width * 1.35f)
            {
                spriteRect = new Rect(
                    texture.width * 0.055f,
                    texture.height * 0.414f,
                    texture.width * 0.89f,
                    texture.height * 0.452f);
            }

            Sprite sprite = Sprite.Create(
                texture,
                spriteRect,
                new Vector2(0.5f, 0.5f),
                100f);

            SpriteCache[path] = sprite;
            return sprite;
        }

        private static Sprite LoadFullSpriteAtPath(string path)
        {
            string cacheKey = $"full:{path}";
            if (SpriteCache.TryGetValue(cacheKey, out Sprite cached))
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                SpriteCache[cacheKey] = null;
                return null;
            }
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = 4;

            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            SpriteCache[cacheKey] = sprite;
            return sprite;
        }
    }
}
