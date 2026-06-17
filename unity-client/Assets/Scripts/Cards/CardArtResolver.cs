using System;
using System.Collections.Generic;
using AppreciatorsTcg.Core;
using UnityEngine;

namespace AppreciatorsTcg.Cards
{
    public static class CardArtResolver
    {
        private const string AssetPackBase = "Art/Placeholder/app_Lane_Card_Game_UI_AssetPack";

        private static readonly string[] AssetPackPortraits =
        {
            "02_card_art/portraits/portrait_hand_winged_kid",
            "02_card_art/portraits/portrait_hand_monke_business",
            "02_card_art/portraits/portrait_hand_mutant_punk",
            "02_card_art/portraits/portrait_hand_cyber_beak",
            "02_card_art/portraits/portrait_hand_toxic_rex",
            "02_card_art/portraits/portrait_player_dragon_kid",
            "02_card_art/portraits/portrait_player_radiation_kid",
            "02_card_art/portraits/portrait_player_three_strikes",
            "02_card_art/portraits/portrait_opponent_winged_kid",
            "02_card_art/portraits/portrait_opponent_monke_business",
            "02_card_art/portraits/portrait_opponent_toxic_rex"
        };

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

            Sprite assetPackPlaceholder = LoadSpriteAtPath(AssetPackPortraitPath(card));
            if (assetPackPlaceholder != null)
            {
                return assetPackPlaceholder;
            }

            return LoadSpriteAtPath(PlaceholderPath(card.type));
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

        private static string AssetPackPortraitPath(CardDefinition card)
        {
            string identity = string.IsNullOrWhiteSpace(card.id) ? card.name : card.id;
            int index = StableIndex($"{card.type}:{card.traitGroup}:{identity}", AssetPackPortraits.Length);
            return $"{AssetPackBase}/{AssetPackPortraits[index]}";
        }

        private static int StableIndex(string value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;
                if (!string.IsNullOrEmpty(value))
                {
                    for (int i = 0; i < value.Length; i++)
                    {
                        hash = hash * 31 + value[i];
                    }
                }

                return (hash & 0x7fffffff) % count;
            }
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

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);

            SpriteCache[path] = sprite;
            return sprite;
        }
    }
}
