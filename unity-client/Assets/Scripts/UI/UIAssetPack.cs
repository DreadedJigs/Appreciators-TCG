using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public static class UIAssetPack
    {
        public const string BasePath = "Art/Placeholder/app_Lane_Card_Game_UI_AssetPack";

        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        public static bool TryGetSprite(string relativePath, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            string key = $"{BasePath}/{relativePath}".Replace("\\", "/");
            int extensionIndex = key.LastIndexOf('.');
            if (extensionIndex >= 0)
            {
                key = key.Substring(0, extensionIndex);
            }

            if (SpriteCache.TryGetValue(key, out sprite))
            {
                return sprite != null;
            }

            Texture2D texture = Resources.Load<Texture2D>(key);
            if (texture == null)
            {
                SpriteCache[key] = null;
                return false;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            SpriteCache[key] = sprite;
            return true;
        }

        public static bool Apply(Image image, string relativePath, bool preserveAspect = true)
        {
            if (image == null || !TryGetSprite(relativePath, out Sprite sprite))
            {
                return false;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            return true;
        }

        public static bool ApplyResource(Image image, string resourcePath, bool preserveAspect = true)
        {
            if (image == null || string.IsNullOrWhiteSpace(resourcePath))
            {
                return false;
            }

            string key = resourcePath.Replace("\\", "/");
            int extensionIndex = key.LastIndexOf('.');
            if (extensionIndex >= 0)
            {
                key = key.Substring(0, extensionIndex);
            }

            Texture2D texture = Resources.Load<Texture2D>(key);
            if (texture == null)
            {
                return false;
            }

            image.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            image.color = Color.white;
            image.preserveAspect = preserveAspect;
            return true;
        }

        public static GameObject CreateImage(Transform parent, string name, string relativePath, bool preserveAspect = true)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            Image image = imageObject.GetComponent<Image>();
            if (!Apply(image, relativePath, preserveAspect))
            {
                image.color = new Color(1f, 1f, 1f, 0f);
            }

            image.raycastTarget = false;
            return imageObject;
        }
    }
}
