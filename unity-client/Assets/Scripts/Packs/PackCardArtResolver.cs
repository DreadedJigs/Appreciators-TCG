using System.Collections.Generic;
using UnityEngine;

namespace AppreciatorsTcg.Packs
{
    // Pack cards use a separate data model from battle cards but share the approved
    // metadata illustrations and official reverse artwork.
    public static class PackCardArtResolver
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite LoadSprite(CardDefinition card)
        {
            if (card == null)
            {
                return null;
            }

            Sprite configured = LoadAt(card.EffectiveArtPath());
            if (configured != null)
            {
                return configured;
            }

            string safeType = string.IsNullOrWhiteSpace(card.type) ? "card" : card.type.ToLowerInvariant();
            return LoadAt($"Art/Placeholder/placeholder_{safeType}");
        }

        public static Sprite LoadPackSprite(PackDefinition pack)
        {
            if (pack == null)
            {
                return null;
            }

            // Unopened packs use a dedicated premium product render built from the
            // official reverse-art mascot and palette. Battle cards still retain
            // the original reverse artwork as their recognizable hidden state.
            Sprite premiumPack = LoadAt("Art/Official/Packs/appreciation_pack_isolated_v1", false);
            if (premiumPack != null)
            {
                return premiumPack;
            }

            premiumPack = LoadAt("Art/Official/Packs/appreciation_pack_premium", false);
            if (premiumPack != null)
            {
                return premiumPack;
            }

            Sprite configured = LoadAt(pack.packArtReference);
            if (configured != null)
            {
                return configured;
            }

            Sprite cardBack = LoadAt("Art/Official/Cards/app_card_reverse");
            if (cardBack != null)
            {
                return cardBack;
            }

            return null;
        }

        public static Sprite LoadCardFaceSprite(CardDefinition card)
        {
            return card == null ? null : LoadAt($"Art/Official/GeneratedCards/{card.id}", false);
        }

        private static Sprite LoadAt(string resourcePath, bool cropTallCardSheet = true)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            string key = resourcePath.Replace("\\", "/");
            int extension = key.LastIndexOf('.');
            if (extension >= 0)
            {
                key = key.Substring(0, extension);
            }

            string cacheKey = cropTallCardSheet ? key : $"full:{key}";
            if (Cache.TryGetValue(cacheKey, out Sprite cached))
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(key);
            if (texture == null)
            {
                Cache[cacheKey] = null;
                return null;
            }

            Rect spriteRect = new Rect(0, 0, texture.width, texture.height);
            if (cropTallCardSheet && texture.height >= texture.width * 1.35f)
            {
                spriteRect = new Rect(
                    texture.width * 0.055f,
                    texture.height * 0.414f,
                    texture.width * 0.89f,
                    texture.height * 0.452f);
            }

            Sprite sprite = Sprite.Create(texture, spriteRect, new Vector2(0.5f, 0.5f), 100f);
            Cache[cacheKey] = sprite;
            return sprite;
        }
    }
}
