using System.Collections.Generic;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public static class UIFactory
    {
        private const string OfficialPlaymatResourcePath = "Art/Official/Board/app_playmat_native_no_appreciation_stars";
        private const string BrandStarfieldResourcePath = "Art/Official/Backgrounds/appreciators_starfield_motif_v2_8k";
        private static Font cachedDefaultFont;
        private static Sprite cachedBrandStarfieldPanelSprite;
        private static readonly Dictionary<string, Sprite> playmatSpriteCache = new Dictionary<string, Sprite>();
        // Official alpha palette. Keep these centralized so final art drops do not
        // require scene-by-scene color edits.
        public static Color Background => ThemeService.Surface(Hex("0F0A46"), Hex("ECE9FA"));
        public static Color Panel => ThemeService.Surface(WithAlpha(Hex("0F0A46"), 0.92f), WithAlpha(Hex("FAFAD2"), 0.96f));
        public static Color PanelAlt => ThemeService.Surface(WithAlpha(Hex("7841AA"), 0.90f), WithAlpha(Hex("D7C3EB"), 0.96f));
        public static Color GlassPanel => ThemeService.Surface(WithAlpha(Hex("0F0A46"), 0.72f), WithAlpha(Hex("FFFFFF"), 0.82f));
        public static Color MenuInset => ThemeService.Surface(WithAlpha(Hex("7841AA"), 0.30f), WithAlpha(Hex("7841AA"), 0.14f));
        public static Color BoardPanel => ThemeService.Surface(WithAlpha(Hex("FAFAD2"), 0.94f), WithAlpha(Hex("FFFFFF"), 0.96f));
        public static readonly Color AlleyFloor = new Color(0.025f, 0.145f, 0.235f, 0.88f);
        public static readonly Color AlleyWall = new Color(0.180f, 0.050f, 0.180f, 0.78f);
        public static readonly Color Ink = WithAlpha(Hex("0F0A46"), 0.98f);
        public static readonly Color Cream = Hex("FAFAD2");
        public static readonly Color HeartRed = Hex("FF2314");
        public static readonly Color Accent = Hex("FFC700");
        public static readonly Color Blue = Hex("00BEE1");
        public static readonly Color Green = Hex("46CB37");
        public static readonly Color Red = Hex("FF2314");
        public static readonly Color Parchment = Hex("FAFAD2");
        public static readonly Color CreamInk = Hex("0F0A46");
        public static readonly Color WoodDark = new Color(0.23f, 0.105f, 0.085f);
        public static readonly Color IceBadge = Hex("C8FAFA");
        public static readonly Color Mouthwash = Hex("C8FAC3");
        public static readonly Color Blush = Hex("FAD7FA");
        public static readonly Color Bruise = Hex("D7C3EB");
        public static readonly Color NeonCyan = Hex("00BEE1");
        public static readonly Color NeonPink = Hex("FAD7FA");
        public static readonly Color PortalViolet = Hex("7841AA");
        public static readonly Color CardBack = Hex("7841AA");
        public static Color TextColor => ThemeService.Surface(Hex("FFFFFF"), Hex("0F0A46"));
        public static Color MutedTextColor => ThemeService.Surface(Hex("C8FAFA"), Hex("57317F"));

        public static Font DefaultFont => LoadDefaultFont();

        public static Canvas CreateCanvas(string name)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // The 16:9 design reference scales cleanly to 1080p and 2160p while
            // retaining touch-sized controls on narrower browser canvases.
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<ResponsiveCanvasScaler>();

            EnsureEventSystem();
            return canvas;
        }

        public static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            if (color.a > 0.02f)
            {
                AddNeonFrame(panel, Color.Lerp(color, NeonCyan, 0.35f), 0.24f);
                AddSoftShadow(panel);
            }

            return panel;
        }

        public static void CreateBackdrop(Transform parent)
        {
            if (CreateResourceBackdropImage(parent, "SnowBoardMock", "Art/Placeholder/UserMock/appBCGmock", new Vector2(0, 0), new Vector2(1, 1), Color.white, true))
            {
                CreateBackdropPanel(parent, "SnowBoardReadabilityWash", new Color(0.76f, 0.94f, 1.00f, 0.08f), Vector2.zero, Vector2.one, 0);
                CreateBackdropPanel(parent, "SnowBoardLowerShade", new Color(0.09f, 0.03f, 0.025f, 0.18f), new Vector2(0, 0), new Vector2(1, 0.34f), 0);
                return;
            }

            CreateBackdropPanel(parent, "AlleyWall", AlleyWall, new Vector2(0, 0.70f), new Vector2(1, 1), 0);
            CreateBackdropPanel(parent, "AlleyFloor", AlleyFloor, new Vector2(0, 0), new Vector2(1, 0.72f), 0);
            CreateBackdropImage(parent, "AssetPackTopBackdrop", "05_board/background_crops/top_graffiti_backdrop.png", new Vector2(0, 0.68f), new Vector2(1, 1), new Color(1f, 1f, 1f, 0.50f));
            CreateBackdropImage(parent, "AssetPackBattlefield", "05_board/background_crops/battlefield_midfield_wide.png", new Vector2(0.04f, 0.11f), new Vector2(0.96f, 0.76f), new Color(1f, 1f, 1f, 0.14f));
            CreateBackdropPanel(parent, "DeepCenter", new Color(0.010f, 0.020f, 0.045f, 0.64f), new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.74f), 0);
            CreateBackdropPanel(parent, "MagentaGraffitiGlow", new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.22f), new Vector2(0.13f, 0.78f), new Vector2(0.36f, 0.84f), -3);
            CreateBackdropPanel(parent, "WallPipeA", new Color(0.08f, 0.045f, 0.060f, 0.65f), new Vector2(0.76f, 0.72f), new Vector2(0.785f, 1.04f), 0);
            CreateBackdropPanel(parent, "WallPipeB", new Color(0.30f, 0.12f, 0.035f, 0.42f), new Vector2(0.81f, 0.77f), new Vector2(0.835f, 0.98f), 0);
            CreateBackdropPanel(parent, "StreetLaneLeft", new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.18f), new Vector2(0.245f, 0.14f), new Vector2(0.250f, 0.72f), -8);
            CreateBackdropPanel(parent, "StreetLaneMid", new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.18f), new Vector2(0.500f, 0.12f), new Vector2(0.505f, 0.72f), 0);
            CreateBackdropPanel(parent, "StreetLaneRight", new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.18f), new Vector2(0.750f, 0.14f), new Vector2(0.755f, 0.72f), 8);
            CreateBackdropPanel(parent, "FloorShadowTop", new Color(0f, 0f, 0f, 0.22f), new Vector2(0, 0.62f), new Vector2(1, 0.72f), 0);
            CreateBackdropPanel(parent, "FloorShadowBottom", new Color(0f, 0f, 0f, 0.24f), new Vector2(0, 0), new Vector2(1, 0.14f), 0);
            CreateBackdropPanel(parent, "GoldHudLineTop", new Color(Accent.r, Accent.g, Accent.b, 0.28f), new Vector2(0.02f, 0.705f), new Vector2(0.98f, 0.713f), 0);
            CreateBackdropPanel(parent, "GoldHudLineBottom", new Color(Accent.r, Accent.g, Accent.b, 0.28f), new Vector2(0.02f, 0.145f), new Vector2(0.98f, 0.153f), 0);

            Text graffiti = CreateText(parent, "BE ORIGINAL", 44, TextAnchor.MiddleCenter, new Color(NeonPink.r, NeonPink.g, NeonPink.b, 0.18f), FontStyle.Bold);
            SetAnchors(graffiti.rectTransform, new Vector2(0.12f, 0.73f), new Vector2(0.40f, 0.90f), Vector2.zero, Vector2.zero);
            graffiti.raycastTarget = false;
        }

        public static bool CreateOfficialPlaymatBackdrop(Transform parent)
        {
            return CreateResourceBackdropImage(
                parent,
                "OfficialAppreciatorsPlaymat",
                OfficialPlaymatResourcePath,
                Vector2.zero,
                Vector2.one,
                Color.white,
                true);
        }

        public static RectTransform CreateOfficialPlaymatRoot(Transform parent)
        {
            GameObject root = new GameObject("OfficialPlaymatRoot", typeof(RectTransform), typeof(AspectRatioFitter));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            Texture2D texture = Resources.Load<Texture2D>(OfficialPlaymatResourcePath);
            AspectRatioFitter fitter = root.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture == null ? 16f / 9f : (float)texture.width / texture.height;

            GameObject imageObject = new GameObject("PlaymatArt", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = ThemeService.IsDark ? new Color(0.58f, 0.50f, 0.68f, 1f) : Color.white;
            image.sprite = LoadPlaymatSprite(new Rect(0f, 0f, 1f, 1f));
            image.preserveAspect = false;
            Stretch(imageObject.GetComponent<RectTransform>());

            if (ThemeService.IsDark)
            {
                CreateDarkPlaymatCover(root.transform, "DarkModeWash", new Rect(0f, 0f, 1f, 1f), new Color(0.018f, 0.012f, 0.085f, 0.26f));
            }
            return rootRect;
        }

        public static RectTransform CreateDeckBackMenuRoot(Transform parent)
        {
            GameObject root = new GameObject("DeckBackMenuRoot", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            GameObject basePanel = CreatePanel(root.transform, "DeckBackBase", new Color(0.012f, 0.010f, 0.055f, 1f));
            Stretch(basePanel.GetComponent<RectTransform>());

            for (int index = 0; index < 3; index++)
            {
                GameObject artObject = new GameObject($"DeckBackArt_{index + 1}", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(root.transform, false);
                RectTransform artRect = artObject.GetComponent<RectTransform>();
                float left = index / 3f;
                float right = (index + 1) / 3f;
                SetAnchors(artRect, new Vector2(left, 0.025f), new Vector2(right, 0.975f), new Vector2(8f, 8f), new Vector2(-8f, -8f));
                Image art = artObject.GetComponent<Image>();
                art.raycastTarget = false;
                if (UIAssetPack.ApplyResource(art, "Art/Official/Cards/app_card_reverse", true))
                {
                    art.color = new Color(0.86f, 0.90f, 1f, index == 1 ? 0.78f : 0.62f);
                }
            }

            GameObject wash = CreatePanel(root.transform, "MenuReadabilityWash", new Color(0.010f, 0.008f, 0.045f, 0.48f));
            Stretch(wash.GetComponent<RectTransform>());
            return rootRect;
        }

        public static RectTransform CreateBrandMenuRoot(Transform parent)
        {
            GameObject root = new GameObject("BrandMenuRoot", typeof(RectTransform), typeof(AspectRatioFitter));
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            Texture2D texture = Resources.Load<Texture2D>(BrandStarfieldResourcePath);
            AspectRatioFitter fitter = root.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = texture == null ? 16f / 9f : (float)texture.width / texture.height;

            GameObject artObject = new GameObject("BrandStarfieldArt", typeof(RectTransform), typeof(Image));
            artObject.transform.SetParent(root.transform, false);
            Image art = artObject.GetComponent<Image>();
            art.raycastTarget = false;
            if (!UIAssetPack.ApplyResource(art, BrandStarfieldResourcePath, false))
            {
                art.color = Background;
            }
            Stretch(artObject.GetComponent<RectTransform>());
            return rootRect;
        }

        public static void ApplyBrandStarfield(GameObject target)
        {
            Image image = target == null ? null : target.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            if (cachedBrandStarfieldPanelSprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>(BrandStarfieldResourcePath);
                if (texture != null)
                {
                    Rect crop = new Rect(0f, texture.height * 0.40f, texture.width, texture.height * 0.58f);
                    cachedBrandStarfieldPanelSprite = Sprite.Create(texture, crop, new Vector2(0.5f, 0.5f), 100f);
                }
            }

            if (cachedBrandStarfieldPanelSprite != null)
            {
                image.sprite = cachedBrandStarfieldPanelSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = ThemeService.IsDark
                    ? new Color(0.66f, 0.76f, 1f, 0.98f)
                    : new Color(0.88f, 0.94f, 1f, 0.98f);
            }
        }

        public static void MakePanelTransparent(GameObject target, bool keepOutline = false)
        {
            Image image = target == null ? null : target.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = null;
            image.color = Color.clear;

            UiGradientEffect gradient = target.GetComponent<UiGradientEffect>();
            if (gradient != null)
            {
                gradient.enabled = false;
            }

            Shadow shadow = target.GetComponent<Shadow>();
            if (shadow != null)
            {
                shadow.enabled = false;
            }

            Outline outline = target.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = keepOutline;
                outline.useGraphicAlpha = false;
            }
        }

        private static void CreateDarkPlaymatCover(Transform parent, string name, Rect normalizedRect, Color color)
        {
            GameObject cover = new GameObject(name, typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(parent, false);
            RectTransform rect = cover.GetComponent<RectTransform>();
            SetAnchors(
                rect,
                new Vector2(normalizedRect.xMin, normalizedRect.yMin),
                new Vector2(normalizedRect.xMax, normalizedRect.yMax),
                Vector2.zero,
                Vector2.zero);
            Image coverImage = cover.GetComponent<Image>();
            coverImage.color = color;
            coverImage.raycastTarget = false;
        }

        public static GameObject CreatePlaymatZoneButton(Transform parent, string name, Rect normalizedRect, UnityAction action)
        {
            GameObject zone = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(PlaymatZoneMotion));
            zone.transform.SetParent(parent, false);
            RectTransform rect = zone.GetComponent<RectTransform>();
            SetAnchors(
                rect,
                new Vector2(normalizedRect.xMin, normalizedRect.yMin),
                new Vector2(normalizedRect.xMax, normalizedRect.yMax),
                Vector2.zero,
                Vector2.zero);

            // The full playmat already contains the zone art. A transparent hit area
            // avoids sub-pixel crop seams and keeps the printed box edges intact.
            Image image = zone.GetComponent<Image>();
            image.color = Color.clear;

            PlaymatZoneMotion motion = zone.GetComponent<PlaymatZoneMotion>();
            // Interaction zones must never paint over the printed playmat artwork.
            motion.Configure(false, Color.clear);

            Button button = zone.GetComponent<Button>();
            button.targetGraphic = image;
            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.36f);
            button.colors = colors;
            return zone;
        }

        public static bool ApplyPlaymatCrop(Image image, Rect normalizedRect)
        {
            Sprite sprite = LoadPlaymatSprite(normalizedRect);
            if (image == null || sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;
            return true;
        }

        public static GameObject CreateVerticalStack(Transform parent, string name, Color color, int spacing = 12, int padding = 16)
        {
            GameObject stack = CreatePanel(parent, name, color);
            VerticalLayoutGroup layout = stack.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return stack;
        }

        public static GameObject CreateHorizontalStack(Transform parent, string name, Color color, int spacing = 12, int padding = 16)
        {
            GameObject stack = CreatePanel(parent, name, color);
            HorizontalLayoutGroup layout = stack.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            return stack;
        }

        public static Text CreateText(Transform parent, string text, int fontSize, TextAnchor alignment, Color color, FontStyle style = FontStyle.Normal)
        {
            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text textComponent = textObject.GetComponent<Text>();
            textComponent.text = text;
            textComponent.font = DefaultFont;
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = color;
            textComponent.fontStyle = style;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            textComponent.lineSpacing = 0.92f;

            return textComponent;
        }

        public static Button CreateButton(Transform parent, string label, UnityAction action, Color color)
        {
            GameObject buttonObject = CreatePanel(parent, label, color);
            AddDimensionalGradient(buttonObject, true);
            buttonObject.AddComponent<UiButtonMotion>();
            buttonObject.AddComponent<UiButtonSfx>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(action);

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.disabledColor = ThemeService.IsDark
                ? new Color(0.20f, 0.21f, 0.25f)
                : new Color(0.82f, 0.79f, 0.90f, 1f);
            button.colors = colors;
            AddNeonFrame(buttonObject, Color.Lerp(color, NeonCyan, 0.35f), 0.52f);

            bool isLongLabel = label.Length > 28 || label.Contains("\n");
            Text labelText = CreateText(
                buttonObject.transform,
                label,
                isLongLabel ? 20 : 25,
                TextAnchor.MiddleCenter,
                ReadableTextColor(color),
                FontStyle.Bold);
            Stretch(labelText.rectTransform);

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.minHeight = 58;
            layout.preferredHeight = 64;

            return button;
        }

        public static void MakeDimensionalPanel(GameObject target, Color accent)
        {
            if (target == null)
            {
                return;
            }

            AddDimensionalGradient(target, false);
            Shadow shadow = target.GetComponent<Shadow>() ?? target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
            shadow.effectDistance = new Vector2(7f, -9f);
            shadow.useGraphicAlpha = true;
            AddNeonFrame(target, accent, 0.90f);
        }

        private static void AddDimensionalGradient(GameObject target, bool addBevelBands)
        {
            Image image = target == null ? null : target.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            UiGradientEffect gradient = target.GetComponent<UiGradientEffect>() ?? target.AddComponent<UiGradientEffect>();
            gradient.Configure(Color.white, ThemeService.IsDark
                ? new Color(0.52f, 0.60f, 0.78f, 1f)
                : new Color(0.72f, 0.76f, 0.88f, 1f));

            if (!addBevelBands)
            {
                return;
            }

            GameObject shine = new GameObject("TopBevel", typeof(RectTransform), typeof(Image));
            shine.transform.SetParent(target.transform, false);
            SetAnchors(shine.GetComponent<RectTransform>(), new Vector2(0.02f, 0.88f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
            Image shineImage = shine.GetComponent<Image>();
            shineImage.color = new Color(1f, 1f, 1f, 0.18f);
            shineImage.raycastTarget = false;

            GameObject lip = new GameObject("BottomBevel", typeof(RectTransform), typeof(Image));
            lip.transform.SetParent(target.transform, false);
            SetAnchors(lip.GetComponent<RectTransform>(), new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.10f), Vector2.zero, Vector2.zero);
            Image lipImage = lip.GetComponent<Image>();
            lipImage.color = new Color(0.02f, 0.01f, 0.08f, 0.32f);
            lipImage.raycastTarget = false;
        }

        public static GameObject CreateShardStackButton(
            Transform parent,
            string name,
            string laneLabel,
            int available,
            string actionLabel,
            Color color,
            UnityAction action)
        {
            GameObject root = CreatePanel(parent, name, new Color(0.02f, 0.02f, 0.08f, 0.78f));
            AddNeonFrame(root, color, 0.90f);
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.minWidth = 78;
            layout.preferredWidth = 86;
            layout.flexibleWidth = 1;
            layout.minHeight = 50;
            layout.preferredHeight = 56;
            layout.flexibleHeight = 1;

            for (int i = 0; i < 3; i++)
            {
                GameObject shard = new GameObject($"Shard_{i + 1}", typeof(RectTransform), typeof(Image));
                shard.transform.SetParent(root.transform, false);
                RectTransform shardRect = shard.GetComponent<RectTransform>();
                shardRect.anchorMin = new Vector2(0.18f, 0.5f);
                shardRect.anchorMax = new Vector2(0.18f, 0.5f);
                shardRect.pivot = new Vector2(0.5f, 0.5f);
                shardRect.sizeDelta = new Vector2(24f, 24f);
                shardRect.anchoredPosition = new Vector2(i * 5f, (i - 1) * 3f);
                shardRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Image shardImage = shard.GetComponent<Image>();
                shardImage.color = new Color(color.r, color.g, color.b, 0.34f + i * 0.20f);
                shardImage.raycastTarget = false;
            }

            Text label = CreateText(
                root.transform,
                $"{laneLabel} {available}\n{actionLabel}",
                13,
                TextAnchor.MiddleCenter,
                Color.white,
                FontStyle.Bold);
            label.rectTransform.anchorMin = new Vector2(0.28f, 0f);
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(2f, 2f);
            label.rectTransform.offsetMax = new Vector2(-3f, -2f);
            label.raycastTarget = false;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 9;
            label.resizeTextMaxSize = 13;

            if (action != null)
            {
                Button button = root.AddComponent<Button>();
                button.targetGraphic = root.GetComponent<Image>();
                button.interactable = available > 0;
                button.onClick.AddListener(action);
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 1f, 0.94f);
                colors.pressedColor = new Color(0.72f, 0.72f, 0.82f, 1f);
                colors.disabledColor = new Color(0.42f, 0.42f, 0.48f, 0.64f);
                button.colors = colors;
                root.AddComponent<PlaymatZoneMotion>();
            }

            return root;
        }

        public static GameObject CreateHudPlate(Transform parent, string playerName, int health, int appreciation, int maxAppreciation, bool opponent)
        {
            GameObject hud = CreateHorizontalStack(parent, opponent ? "OpponentHud" : "PlayerHud", new Color(0.014f, 0.018f, 0.040f, 0.94f), 8, 8);
            AddNeonFrame(hud, opponent ? NeonPink : Accent, 0.62f);
            HorizontalLayoutGroup group = hud.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = true;
            LayoutElement hudLayout = hud.AddComponent<LayoutElement>();
            hudLayout.minWidth = 214;
            hudLayout.preferredWidth = 248;
            hudLayout.flexibleWidth = 0;
            hudLayout.minHeight = 92;
            hudLayout.preferredHeight = 100;

            GameObject avatar = CreatePanel(hud.transform, "Avatar", opponent ? new Color(0.45f, 0.10f, 0.42f) : new Color(0.10f, 0.62f, 0.32f));
            AddNeonFrame(avatar, Cream, 0.84f);
            bool avatarArtApplied = UIAssetPack.Apply(avatar.GetComponent<Image>(), opponent ? "03_hud/opponent/opponent_hero_avatar.png" : "03_hud/player/player_hero_avatar.png", true);
            LayoutElement avatarLayout = avatar.AddComponent<LayoutElement>();
            avatarLayout.minWidth = 62;
            avatarLayout.preferredWidth = 72;
            avatarLayout.flexibleWidth = 0;
            if (!avatarArtApplied)
            {
                Text avatarText = CreateText(avatar.transform, opponent ? "AI" : "A", 34, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);
                Stretch(avatarText.rectTransform);
            }

            GameObject stats = CreateVerticalStack(hud.transform, "HudStats", Color.clear, 5, 0);
            LayoutElement statsLayout = stats.AddComponent<LayoutElement>();
            statsLayout.flexibleWidth = 1;
            CreateNamePlate(stats.transform, playerName);
            CreateHealthPlate(stats.transform, health);
            CreateAppreciationPips(stats.transform, appreciation, maxAppreciation, opponent);
            return hud;
        }

        public static GameObject CreateCompactMatchHud(Transform parent, string playerName, int health, int appreciation, int turn, bool opponent)
        {
            Color surface = ThemeService.IsDark ? new Color(0.025f, 0.020f, 0.095f, 0.94f) : new Color(0.96f, 0.94f, 0.84f, 0.94f);
            GameObject hud = CreateHorizontalStack(parent, opponent ? "OpponentHud" : "PlayerHud", surface, 2, 2);
            AddNeonFrame(hud, opponent ? Red : NeonCyan, 0.72f);
            Shadow hudShadow = hud.AddComponent<Shadow>();
            hudShadow.effectColor = new Color(0f, 0f, 0f, 0.52f);
            hudShadow.effectDistance = new Vector2(3f, -3f);
            HorizontalLayoutGroup group = hud.GetComponent<HorizontalLayoutGroup>();
            group.childControlWidth = true;
            group.childForceExpandWidth = true;
            group.childAlignment = TextAnchor.MiddleCenter;
            LayoutElement hudLayout = hud.AddComponent<LayoutElement>();
            hudLayout.flexibleWidth = 1f;
            hudLayout.minHeight = 42f;
            hudLayout.preferredHeight = 48f;

            Text name = CreateText(hud.transform, string.IsNullOrWhiteSpace(playerName) ? "PLAYER" : playerName.ToUpperInvariant(), 13, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
            LayoutElement nameLayout = name.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;
            nameLayout.minWidth = 72f;
            nameLayout.preferredWidth = 90f;
            CreateCompactStat(hud.transform, "HP", health, HeartRed);
            CreateCompactStat(hud.transform, appreciation >= GameConstants.SpotlightGrowthThreshold ? "APPRECIATION ★" : "APPRECIATION", appreciation, opponent ? Blue : Accent);
            CreateCompactStat(hud.transform, "ROUND", turn, PortalViolet);
            return hud;
        }

        private static void CreateCompactStat(Transform parent, string label, int value, Color color)
        {
            GameObject stat = CreateHorizontalStack(parent, label, new Color(color.r, color.g, color.b, 0.22f), 3, 3);
            LayoutElement layout = stat.AddComponent<LayoutElement>();
            bool isAppreciation = label.StartsWith("APPRECIATION", System.StringComparison.Ordinal);
            layout.minWidth = isAppreciation ? 96f : 62f;
            layout.preferredWidth = isAppreciation ? 106f : 72f;
            layout.flexibleWidth = 1f;
            Text text = CreateText(stat.transform, $"{label} {value}", 12, TextAnchor.MiddleCenter, Cream, FontStyle.Bold);
            LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1f;
        }

        public static GameObject CreateNamePlate(Transform parent, string playerName)
        {
            GameObject plate = CreateHorizontalStack(parent, "NamePlate", new Color(0.055f, 0.115f, 0.220f, 0.90f), 4, 5);
            LayoutElement layout = plate.AddComponent<LayoutElement>();
            layout.minHeight = 28;
            layout.preferredHeight = 32;
            Text star = CreateText(plate.transform, "★", 22, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            LayoutElement starLayout = star.gameObject.AddComponent<LayoutElement>();
            starLayout.minWidth = 28;
            starLayout.preferredWidth = 30;
            Text name = CreateText(plate.transform, string.IsNullOrWhiteSpace(playerName) ? "APPRECIATOR" : playerName.ToUpperInvariant(), 17, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
            LayoutElement nameLayout = name.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;
            CreateText(plate.transform, "★", 22, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            return plate;
        }

        public static GameObject CreateHealthPlate(Transform parent, int health)
        {
            GameObject plate = CreateHorizontalStack(parent, "HealthPlate", new Color(0.055f, 0.015f, 0.040f, 0.80f), 5, 4);
            LayoutElement layout = plate.AddComponent<LayoutElement>();
            layout.minHeight = 38;
            layout.preferredHeight = 42;
            Text heart = CreateText(plate.transform, "♥", 30, TextAnchor.MiddleCenter, HeartRed, FontStyle.Bold);
            LayoutElement heartLayout = heart.gameObject.AddComponent<LayoutElement>();
            heartLayout.minWidth = 42;
            heartLayout.preferredWidth = 46;
            Text value = CreateText(plate.transform, health.ToString(), 30, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
            LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleWidth = 1;
            return plate;
        }

        public static GameObject CreateAppreciationPips(Transform parent, int appreciation, int maxAppreciation, bool opponent)
        {
            GameObject row = CreateHorizontalStack(parent, "AppreciationPips", Color.clear, 4, 0);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 22;
            layout.preferredHeight = 26;
            Text count = CreateText(row.transform, $"{appreciation}/{maxAppreciation}", 17, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            LayoutElement countLayout = count.gameObject.AddComponent<LayoutElement>();
            countLayout.minWidth = 62;
            countLayout.preferredWidth = 68;
            count.resizeTextForBestFit = true;
            count.resizeTextMinSize = 12;
            count.resizeTextMaxSize = 17;
            int pipCount = 10;
            int filledPips = maxAppreciation <= 0
                ? 0
                : Mathf.Clamp(Mathf.CeilToInt((float)appreciation / maxAppreciation * pipCount), 0, pipCount);
            for (int i = 0; i < pipCount; i++)
            {
                GameObject pip = CreatePanel(row.transform, "AppreciationPip", i < filledPips ? (opponent ? NeonCyan : Accent) : new Color(0.025f, 0.030f, 0.080f, 0.92f));
                LayoutElement pipLayout = pip.AddComponent<LayoutElement>();
                pipLayout.minWidth = 10;
                pipLayout.preferredWidth = 12;
                pipLayout.minHeight = 14;
                pipLayout.preferredHeight = 16;
                pipLayout.flexibleWidth = 0;
            }

            return row;
        }

        public static GameObject CreateResourceBadge(Transform parent, string icon, int value, Color color)
        {
            GameObject badge = CreateHorizontalStack(parent, $"Resource{icon}", new Color(0.026f, 0.022f, 0.060f, 0.92f), 6, 7);
            LayoutElement layout = badge.AddComponent<LayoutElement>();
            layout.minWidth = 110;
            layout.preferredWidth = 126;
            layout.minHeight = 42;
            layout.preferredHeight = 46;
            Text iconText = CreateText(badge.transform, icon, 26, TextAnchor.MiddleCenter, color, FontStyle.Bold);
            LayoutElement iconLayout = iconText.gameObject.AddComponent<LayoutElement>();
            iconLayout.minWidth = 34;
            iconLayout.preferredWidth = 38;
            Text valueText = CreateText(badge.transform, value.ToString(), 26, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
            LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleWidth = 1;
            return badge;
        }

        public static GameObject CreateDeckStack(Transform parent, string icon, int count, bool opponent)
        {
            GameObject stack = CreateVerticalStack(parent, opponent ? "OpponentDeckStack" : "PlayerDeckStack", new Color(0.050f, 0.018f, 0.085f, 0.90f), 5, 7);
            AddNeonFrame(stack, Cream, 0.76f);
            Image stackImage = stack.GetComponent<Image>();
            string deckArt = opponent ? "04_ui/resource_panels/left_token_panel_A.png" : "04_ui/card_backs_and_decks/right_deck_panel_skull.png";
            if (UIAssetPack.Apply(stackImage, deckArt, true))
            {
                stackImage.color = new Color(1f, 1f, 1f, 0.94f);
            }

            LayoutElement layout = stack.AddComponent<LayoutElement>();
            layout.minWidth = 86;
            layout.preferredWidth = 96;
            layout.minHeight = 112;
            layout.preferredHeight = 132;
            layout.flexibleWidth = 0;
            Text symbol = CreateText(stack.transform, icon, 38, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            LayoutElement symbolLayout = symbol.gameObject.AddComponent<LayoutElement>();
            symbolLayout.minHeight = 54;
            symbolLayout.preferredHeight = 62;
            Text countText = CreateText(stack.transform, count.ToString(), 25, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);
            LayoutElement countLayout = countText.gameObject.AddComponent<LayoutElement>();
            countLayout.minHeight = 32;
            countLayout.preferredHeight = 36;
            return stack;
        }

        public static InputField CreateInputField(Transform parent, string placeholder, string value)
        {
            GameObject fieldObject = CreatePanel(parent, "InputField", WithAlpha(Background, 0.96f));
            InputField input = fieldObject.AddComponent<InputField>();
            fieldObject.AddComponent<InputFieldContextMenu>();
            Image image = fieldObject.GetComponent<Image>();
            image.color = WithAlpha(Background, 0.96f);

            Text text = CreateText(fieldObject.transform, value, 26, TextAnchor.MiddleLeft, TextColor);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16, 0);
            text.rectTransform.offsetMax = new Vector2(-16, 0);
            text.raycastTarget = false;

            Text placeholderText = CreateText(fieldObject.transform, placeholder, 26, TextAnchor.MiddleLeft, MutedTextColor);
            Stretch(placeholderText.rectTransform);
            placeholderText.rectTransform.offsetMin = new Vector2(16, 0);
            placeholderText.rectTransform.offsetMax = new Vector2(-16, 0);
            placeholderText.raycastTarget = false;

            input.textComponent = text;
            input.placeholder = placeholderText;
            input.text = value;

            LayoutElement layout = fieldObject.AddComponent<LayoutElement>();
            layout.minHeight = 64;
            layout.preferredHeight = 70;
            return input;
        }

        public static RectTransform CreateScrollContent(Transform parent, string name, bool horizontal, out ScrollRect scrollRect, bool centerHorizontalContent = false)
        {
            GameObject scrollObject = CreatePanel(parent, name, WithAlpha(Background, 0.82f));
            scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.scrollSensitivity = 42f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            ScrollWheelRelay wheelRelay = scrollObject.AddComponent<ScrollWheelRelay>();
            wheelRelay.Target = scrollRect;
            LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1;
            scrollLayout.flexibleWidth = 1;

            GameObject viewport = CreatePanel(scrollObject.transform, "Viewport", WithAlpha(Background, 0.82f));
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport.GetComponent<RectTransform>());

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);

            if (horizontal)
            {
                HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 14;
                int verticalPadding = centerHorizontalContent ? 4 : 12;
                layout.padding = new RectOffset(28, 28, verticalPadding, verticalPadding);
                layout.childAlignment = centerHorizontalContent ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                if (!centerHorizontalContent)
                {
                    ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }
            else
            {
                VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10;
                layout.padding = new RectOffset(28, 28, 12, 12);
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = horizontal ? new Vector2(0, 0) : new Vector2(0, 1);
            contentRect.anchorMax = horizontal ? new Vector2(0, 1) : new Vector2(1, 1);
            contentRect.pivot = horizontal ? new Vector2(0, 0.5f) : new Vector2(0.5f, 1);

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = horizontal;
            scrollRect.vertical = !horizontal;

            if (horizontal && centerHorizontalContent)
            {
                ScrollContentMinWidth centerer = content.AddComponent<ScrollContentMinWidth>();
                centerer.Viewport = scrollRect.viewport;
            }

            return contentRect;
        }

        public static RectTransform CreateGridScrollContent(
            Transform parent,
            string name,
            Vector2 cellSize,
            int columns,
            out ScrollRect scrollRect)
        {
            GameObject scrollObject = CreatePanel(parent, name, WithAlpha(Background, 0.82f));
            scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.scrollSensitivity = 42f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            ScrollWheelRelay wheelRelay = scrollObject.AddComponent<ScrollWheelRelay>();
            wheelRelay.Target = scrollRect;
            LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1;
            scrollLayout.flexibleWidth = 1;

            GameObject viewport = CreatePanel(scrollObject.transform, "Viewport", WithAlpha(Background, 0.82f));
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport.GetComponent<RectTransform>());

            GameObject content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(10f, 12f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);

            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            return contentRect;
        }

        public static GameObject CreateCardPanel(Transform parent, string title, string body, Color color)
        {
            GameObject card = CreateVerticalStack(parent, title, color, 6, 10);
            LayoutElement layout = card.AddComponent<LayoutElement>();
            layout.minWidth = 210;
            layout.preferredWidth = 240;
            layout.minHeight = 145;
            layout.preferredHeight = 160;

            CreateText(card.transform, title, 22, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
            CreateText(card.transform, body, 18, TextAnchor.UpperLeft, MutedTextColor);
            return card;
        }

        public static GameObject CreateCardPanel(
            Transform parent,
            CardDefinition card,
            UnityAction action = null,
            bool selected = false,
            string footer = null,
            bool compact = false)
        {
            Color color = selected ? Hex("25105A") : Color.clear;
            GameObject panel = CreatePanel(parent, card.name, color);
            if (selected)
            {
                AddNeonFrame(panel, Accent, 0.98f);
            }
            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = compact ? 176 : 225;
            layout.preferredWidth = compact ? 194 : 255;
            layout.flexibleWidth = compact ? 0 : 1;
            layout.minHeight = compact ? 218 : 292;
            layout.preferredHeight = compact ? 232 : 310;
            layout.flexibleHeight = 0;

            if (action != null)
            {
                Button button = panel.AddComponent<Button>();
                button.targetGraphic = panel.GetComponent<Image>();
                button.onClick.AddListener(action);

                ColorBlock colors = button.colors;
                colors.normalColor = color;
                colors.highlightedColor = Color.Lerp(color, PortalViolet, 0.26f);
                colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
                colors.disabledColor = new Color(0.20f, 0.21f, 0.25f);
                button.colors = colors;
            }

            PopulateOfficialCardFront(panel.transform, card, compact ? OfficialCardScale.Compact : OfficialCardScale.Full, footer);

            return panel;
        }

        public static GameObject CreateMiniCardPanel(
            Transform parent,
            CardDefinition card,
            string stats,
            bool selected = false,
            int width = 82,
            int height = 84,
            int artHeight = 34)
        {
            Color color = selected ? Hex("25105A") : Color.clear;
            GameObject panel = CreatePanel(parent, card.name, color);
            Image image = panel.GetComponent<Image>();
            image.color = color;
            if (selected)
            {
                AddNeonFrame(panel, Accent, 0.96f);
            }
            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;

            PopulateOfficialCardFront(panel.transform, card, OfficialCardScale.Mini, null);
            Text miniStats = CreateText(panel.transform, stats, width < 78 ? 8 : 10, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            miniStats.gameObject.name = "RuntimeStats";
            SetAnchors(miniStats.rectTransform, new Vector2(0.66f, 0.225f), new Vector2(0.91f, 0.285f), Vector2.zero, Vector2.zero);
            miniStats.resizeTextForBestFit = true;
            miniStats.resizeTextMinSize = 5;
            miniStats.resizeTextMaxSize = width < 78 ? 8 : 10;
            miniStats.raycastTarget = false;
            return panel;
        }

        public static GameObject CreateCardArtThumbnail(Transform parent, CardDefinition card, int width = 64, int height = 62)
        {
            GameObject thumbnail = CreatePanel(parent, $"Thumbnail_{card.id}", Cream);
            AddNeonFrame(thumbnail, ColorForType(card.type), 0.84f);
            LayoutElement layout = thumbnail.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;

            Image image = thumbnail.GetComponent<Image>();
            Sprite sprite = CardArtResolver.LoadSprite(card);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
            }
            else
            {
                Text fallback = CreateText(thumbnail.transform, "ART", 13, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
                Stretch(fallback.rectTransform);
            }
            return thumbnail;
        }

        public static GameObject CreateMatchHandCardPanel(Transform parent, CardDefinition card, UnityAction action, bool selected = false, string footer = null)
        {
            Color buttonColor = selected ? Hex("25105A") : Color.clear;
            GameObject panel = CreatePanel(parent, card.name, buttonColor);
            Image panelImage = panel.GetComponent<Image>();
            panelImage.color = buttonColor;
            if (selected)
            {
                AddNeonFrame(panel, Accent, 0.98f);
            }

            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = 127;
            layout.preferredWidth = 127;
            layout.flexibleWidth = 0;
            layout.minHeight = 190;
            layout.preferredHeight = 190;
            layout.flexibleHeight = 0;

            if (action != null)
            {
                Button button = panel.AddComponent<Button>();
                button.targetGraphic = panel.GetComponent<Image>();
                button.onClick.AddListener(action);

                ColorBlock colors = button.colors;
                colors.normalColor = buttonColor;
                colors.highlightedColor = Color.Lerp(buttonColor, PortalViolet, 0.26f);
                colors.pressedColor = Color.Lerp(buttonColor, Color.black, 0.18f);
                colors.disabledColor = new Color(0.20f, 0.21f, 0.25f);
                button.colors = colors;
            }

            PopulateOfficialCardFront(panel.transform, card, OfficialCardScale.Hand, footer);

            return panel;
        }

        private enum OfficialCardScale
        {
            Full,
            Compact,
            Hand,
            Mini
        }

        public static string PillarSymbol(string pillar)
        {
            if (string.Equals(pillar, "Learn", System.StringComparison.OrdinalIgnoreCase)) return "◉";
            if (string.Equals(pillar, "Grow", System.StringComparison.OrdinalIgnoreCase)) return "✦";
            return "⬢";
        }

        public static string RaritySymbol(string rarity)
        {
            if (string.Equals(rarity, GameConstants.OneOfOne, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rarity, GameConstants.Crown, System.StringComparison.OrdinalIgnoreCase)) return "♛";
            if (string.Equals(rarity, GameConstants.Legendary, System.StringComparison.OrdinalIgnoreCase)) return "★";
            if (string.Equals(rarity, GameConstants.Rare, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rarity, GameConstants.Epic, System.StringComparison.OrdinalIgnoreCase)) return "◆";
            return "●";
        }

        private static void CreateCardClassificationStrip(Transform parent, CardDefinition card, OfficialCardScale scale)
        {
            GameObject strip = CreatePanel(parent, "ClassificationSymbols", ThemeService.IsDark ? new Color(0.03f, 0.02f, 0.14f, 0.94f) : new Color(0.98f, 0.97f, 0.86f, 0.95f));
            RectTransform rect = strip.GetComponent<RectTransform>();
            float height = scale == OfficialCardScale.Mini ? 0.14f : 0.075f;
            SetAnchors(rect, new Vector2(0.10f, 0.008f), new Vector2(0.90f, height), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup layout = strip.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.padding = new RectOffset(3, 3, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            int size = scale == OfficialCardScale.Mini ? 8 : scale == OfficialCardScale.Hand ? 13 : 16;
            Color iconColor = ThemeService.IsDark ? Cream : Ink;
            CreateText(strip.transform, "◈", size, TextAnchor.MiddleCenter, iconColor, FontStyle.Bold).raycastTarget = false;
            CreateText(strip.transform, PillarSymbol(card.GetPillar()), size, TextAnchor.MiddleCenter, iconColor, FontStyle.Bold).raycastTarget = false;
            CreateText(strip.transform, RaritySymbol(card.rarity), size, TextAnchor.MiddleCenter, Accent, FontStyle.Bold).raycastTarget = false;
            CardClassificationTooltipTrigger trigger = strip.AddComponent<CardClassificationTooltipTrigger>();
            trigger.Card = card;
        }

        private static void PopulateOfficialCardFront(Transform parent, CardDefinition card, OfficialCardScale scale, string footer)
        {
            Sprite bakedFace = CardArtResolver.LoadCardFaceSprite(card);
            if (bakedFace != null)
            {
                CreateBakedCardVisual(parent, bakedFace, card.rarity);
                return;
            }

            Debug.LogError($"Missing baked production card face for '{card?.id ?? "<null>"}'. Regenerate cards with scripts/generate-card-faces.ps1.");
            string footerLabel = $"{card.rarity.ToUpperInvariant()}  |  {card.type.ToUpperInvariant()}";
            if (!string.IsNullOrWhiteSpace(footer) && scale == OfficialCardScale.Full)
            {
                footerLabel += $"  |  {footer.ToUpperInvariant()}";
            }

            CreateOfficialCardVisual(
                parent,
                card.name,
                card.GetAttack(),
                card.GetDefense(),
                card.GetCardRulesText(),
                footerLabel,
                CardArtResolver.LoadSprite(card),
                scale);
        }

        public static GameObject CreateBakedCardVisual(Transform parent, Sprite faceSprite, string rarity = null)
        {
            GameObject canvas = new GameObject("BakedCardFace", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            canvas.transform.SetParent(parent, false);
            Image image = canvas.GetComponent<Image>();
            image.sprite = faceSprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            AspectRatioFitter fitter = canvas.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 2f / 3f;
            PremiumCardPresentation.Attach(canvas, rarity);
            return canvas;
        }

        public static GameObject CreateOfficialCardVisual(
            Transform parent,
            string cardName,
            int attack,
            int defense,
            string effectText,
            string metadata,
            Sprite artSprite,
            bool compact = false)
        {
            return CreateOfficialCardVisual(
                parent,
                cardName,
                attack,
                defense,
                effectText,
                metadata,
                artSprite,
                compact ? OfficialCardScale.Compact : OfficialCardScale.Full);
        }

        private static GameObject CreateOfficialCardVisual(
            Transform parent,
            string cardName,
            int attack,
            int defense,
            string effectText,
            string metadata,
            Sprite artSprite,
            OfficialCardScale scale)
        {
            GameObject canvas = new GameObject("OfficialCardCanvas", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            canvas.transform.SetParent(parent, false);
            Image canvasImage = canvas.GetComponent<Image>();
            canvasImage.color = Color.clear;
            canvasImage.raycastTarget = false;
            AspectRatioFitter fitter = canvas.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 2f / 3f;

            GameObject frame = new GameObject("OfficialCardTemplate", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(canvas.transform, false);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.raycastTarget = false;
            if (!UIAssetPack.ApplyResource(frameImage, "Art/Official/CardTemplate/templates/full_card_template_blank", false))
            {
                Debug.LogError("Missing official card template at Resources/Art/Official/CardTemplate/templates/full_card_template_blank.png.");
                frameImage.color = Hex("2B1762");
            }
            Stretch(frame.GetComponent<RectTransform>());

            GameObject artViewport = new GameObject("CardArt", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            artViewport.transform.SetParent(canvas.transform, false);
            Image viewportImage = artViewport.GetComponent<Image>();
            viewportImage.color = Hex("6C67EB");
            viewportImage.raycastTarget = false;
            SetAnchors(artViewport.GetComponent<RectTransform>(), new Vector2(0.076f, 0.337f), new Vector2(0.924f, 0.848f), Vector2.zero, Vector2.zero);
            if (artSprite != null)
            {
                CreateFitImage(artViewport.transform, "MetadataArt", artSprite);
            }

            // The artist's template remains the source of all frame geometry. Text is
            // positioned into its blank value fields so every runtime card keeps the
            // same proportions at collection, hand, board, and reveal sizes.
            GameObject combatHeader = CreateHorizontalStack(canvas.transform, "CombatStats", Ink, 8, 7);
            SetAnchors(combatHeader.GetComponent<RectTransform>(), new Vector2(0.055f, 0.858f), new Vector2(0.945f, 0.974f), Vector2.zero, Vector2.zero);
            CreateCombatStatBadge(combatHeader.transform, "ATTACK", Mathf.Max(0, attack), HeartRed, scale);
            CreateCombatStatBadge(combatHeader.transform, "DEFENSE", Mathf.Max(0, defense), Blue, scale);

            GameObject nameStrip = CreatePanel(canvas.transform, "CardNameStrip", PortalViolet);
            AddNeonFrame(nameStrip, Accent, 0.82f);
            SetAnchors(nameStrip.GetComponent<RectTransform>(), new Vector2(0.060f, 0.218f), new Vector2(0.940f, 0.307f), Vector2.zero, Vector2.zero);
            Text nameText = CreateText(nameStrip.transform, (cardName ?? "CARD").ToUpperInvariant(), NameFont(scale), TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);
            nameText.gameObject.name = "CardName";
            SetAnchors(nameText.rectTransform, new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f), Vector2.zero, Vector2.zero);
            ConfigureTemplateText(nameText, Mathf.Max(7, NameFont(scale) - 7), NameFont(scale));

            GameObject rules = new GameObject("CardRules", typeof(RectTransform));
            rules.transform.SetParent(canvas.transform, false);
            SetAnchors(rules.GetComponent<RectTransform>(), new Vector2(0.075f, 0.035f), new Vector2(0.925f, 0.203f), Vector2.zero, Vector2.zero);
            Text effect = CreateText(rules.transform, string.IsNullOrWhiteSpace(effectText) ? "No effect." : effectText, RulesFont(scale), TextAnchor.MiddleCenter, TextColor, FontStyle.Italic);
            effect.gameObject.name = "EffectText";
            SetAnchors(effect.rectTransform, new Vector2(0.05f, 0.27f), new Vector2(0.95f, 0.90f), Vector2.zero, Vector2.zero);
            ConfigureTemplateText(effect, Mathf.Max(5, RulesFont(scale) - 5), RulesFont(scale));

            if (scale != OfficialCardScale.Mini && !string.IsNullOrWhiteSpace(metadata))
            {
                Text metadataText = CreateText(rules.transform, metadata.ToUpperInvariant(), MetadataFont(scale), TextAnchor.MiddleCenter, MutedTextColor, FontStyle.Bold);
                metadataText.gameObject.name = "CardMetadata";
                SetAnchors(metadataText.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.30f), Vector2.zero, Vector2.zero);
                ConfigureTemplateText(metadataText, 5, MetadataFont(scale));
            }

            PremiumCardPresentation.Attach(canvas, metadata);
            return canvas;
        }

        private static void CreateCombatStatBadge(Transform parent, string label, int value, Color color, OfficialCardScale scale)
        {
            GameObject badge = CreateHorizontalStack(parent, label, new Color(color.r * 0.24f, color.g * 0.24f, color.b * 0.24f, 0.98f), 4, 4);
            AddNeonFrame(badge, color, 0.90f);
            LayoutElement layout = badge.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            Text text = CreateText(badge.transform, $"{label}  {value}", LaneValueFont(scale), TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(6, LaneValueFont(scale) - 9);
            text.resizeTextMaxSize = LaneValueFont(scale);
            text.raycastTarget = false;
        }

        private static void CreateTemplateValue(Transform parent, string name, string value, Vector2 min, Vector2 max, int fontSize)
        {
            Text text = CreateText(parent, value, fontSize, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            text.gameObject.name = name;
            SetAnchors(text.rectTransform, min, max, Vector2.zero, Vector2.zero);
            ConfigureTemplateText(text, Mathf.Max(6, fontSize - 8), fontSize);
        }

        private static void ConfigureTemplateText(Text text, int minSize, int maxSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static int LaneValueFont(OfficialCardScale scale)
        {
            return scale == OfficialCardScale.Full ? 28 : scale == OfficialCardScale.Mini ? 9 : scale == OfficialCardScale.Hand ? 16 : 18;
        }

        private static int NameFont(OfficialCardScale scale)
        {
            return scale == OfficialCardScale.Full ? 22 : scale == OfficialCardScale.Mini ? 7 : scale == OfficialCardScale.Hand ? 12 : 14;
        }

        private static int CostFont(OfficialCardScale scale)
        {
            return scale == OfficialCardScale.Full ? 28 : scale == OfficialCardScale.Mini ? 9 : scale == OfficialCardScale.Hand ? 16 : 18;
        }

        private static int RulesFont(OfficialCardScale scale)
        {
            return scale == OfficialCardScale.Full ? 15 : scale == OfficialCardScale.Mini ? 5 : scale == OfficialCardScale.Hand ? 8 : 10;
        }

        private static int MetadataFont(OfficialCardScale scale)
        {
            return scale == OfficialCardScale.Full ? 9 : scale == OfficialCardScale.Hand ? 6 : 7;
        }

        private static void CreateLaneStrengthHeader(Transform parent, CardDefinition card, int height, bool compact)
        {
            GameObject header = CreateHorizontalStack(parent, "LaneStrengths", Hex("09082E"), compact ? 2 : 4, compact ? 2 : 4);
            AddNeonFrame(header, PortalViolet, 0.96f);
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.minHeight = height;
            headerLayout.preferredHeight = height;
            headerLayout.flexibleHeight = 0;

            CreateLaneStrengthBadge(header.transform, card, LaneType.Art, "ART", "♥", HeartRed, compact);
            CreateLaneStrengthBadge(header.transform, card, LaneType.Blockchain, "CHAIN", "◆", Hex("1769FF"), compact);
            CreateLaneStrengthBadge(header.transform, card, LaneType.Community, "COMM", "★", Accent, compact);
        }

        private static void CreateLaneStrengthBadge(Transform parent, CardDefinition card, LaneType lane, string label, string icon, Color color, bool compact)
        {
            bool strongest = card.StrongestLane() == lane;
            GameObject badge = CreateVerticalStack(parent, $"{lane}Strength", new Color(color.r * 0.18f, color.g * 0.18f, color.b * 0.18f, 0.98f), 0, 1);
            AddNeonFrame(badge, strongest ? color : Color.Lerp(color, Ink, 0.48f), strongest ? 0.98f : 0.62f);
            LayoutElement badgeLayout = badge.AddComponent<LayoutElement>();
            badgeLayout.flexibleWidth = 1;

            Text value = CreateText(badge.transform, $"{icon} {card.GetLaneStrength(lane)}", compact ? 11 : 18, TextAnchor.MiddleCenter, strongest ? Color.white : Color.Lerp(Color.white, color, 0.24f), FontStyle.Bold);
            value.resizeTextForBestFit = true;
            value.resizeTextMinSize = compact ? 7 : 12;
            value.resizeTextMaxSize = compact ? 11 : 18;
            LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
            valueLayout.flexibleHeight = 1;

            Text laneLabel = CreateText(badge.transform, label, compact ? 6 : 9, TextAnchor.MiddleCenter, strongest ? color : MutedTextColor, FontStyle.Bold);
            SetFixedLayoutHeight(laneLabel.gameObject, compact ? 9 : 13);
        }

        private static void CreateOfficialNameStrip(Transform parent, CardDefinition card, int height, int nameSize, int costSize)
        {
            GameObject strip = CreateHorizontalStack(parent, "CardNameStrip", Hex("311067"), 3, 4);
            AddNeonFrame(strip, Accent, 0.82f);
            LayoutElement stripLayout = strip.AddComponent<LayoutElement>();
            stripLayout.minHeight = height;
            stripLayout.preferredHeight = height;
            stripLayout.flexibleHeight = 0;

            Text name = CreateText(strip.transform, card.name.ToUpperInvariant(), nameSize, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = Mathf.Max(7, nameSize - 5);
            name.resizeTextMaxSize = nameSize;
            LayoutElement nameLayout = name.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;

            GameObject cost = CreatePanel(strip.transform, "CostBadge", Accent);
            AddNeonFrame(cost, Ink, 0.96f);
            LayoutElement costLayout = cost.AddComponent<LayoutElement>();
            costLayout.minWidth = height;
            costLayout.preferredWidth = height;
            costLayout.flexibleWidth = 0;
            Text costText = CreateText(cost.transform, card.cost.ToString(), costSize, TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            Stretch(costText.rectTransform);
        }

        private static void SetFixedLayoutHeight(GameObject target, float height)
        {
            LayoutElement layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static void CreateStatLabel(Transform parent, string label, int value, Color color)
        {
            GameObject stat = CreateHorizontalStack(parent, label, new Color(0.99f, 0.88f, 0.65f, 0.88f), 2, 2);
            LayoutElement layout = stat.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            Text text = CreateText(stat.transform, $"{label} {value}", 11, TextAnchor.MiddleCenter, color, FontStyle.Bold);
            LayoutElement textLayout = text.gameObject.AddComponent<LayoutElement>();
            textLayout.flexibleWidth = 1;
        }

        public static GameObject CreateCardBackPanel(Transform parent, string label = "APP", int width = 92, int height = 116)
        {
            // Keep the layout hit area requested by the caller, but let the visible
            // card use the artwork's true 2:3 silhouette. Older callers use several
            // slightly different slot ratios; a transparent host prevents those
            // slots from showing as stretched blue rectangles behind the card.
            GameObject panel = CreatePanel(parent, "CardBack", Color.clear);
            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;

            GameObject officialArt = new GameObject("OfficialCardBackArt", typeof(RectTransform), typeof(Image));
            officialArt.transform.SetParent(panel.transform, false);
            Image officialImage = officialArt.GetComponent<Image>();
            officialImage.raycastTarget = false;
            Stretch(officialArt.GetComponent<RectTransform>());
            if (UIAssetPack.ApplyResource(officialImage, "Art/Official/Cards/app_card_reverse_premium", true))
            {
                ConfigureCardBackArt(officialArt, officialImage);
                return panel;
            }

            if (UIAssetPack.ApplyResource(officialImage, "Art/Official/Cards/app_card_reverse", true))
            {
                ConfigureCardBackArt(officialArt, officialImage);
                return panel;
            }

            officialArt.SetActive(false);

            int variant = Mathf.Max(0, parent.childCount - 1) % 4 + 1;
            if (UIAssetPack.Apply(panel.GetComponent<Image>(), $"04_ui/card_backs_and_decks/opponent_hand_card_back_{variant:00}.png", true))
            {
                return panel;
            }

            GameObject sigil = CreatePanel(panel.transform, "BackSigil", PortalViolet);
            SetAnchors(sigil.GetComponent<RectTransform>(), new Vector2(0.18f, 0.24f), new Vector2(0.82f, 0.76f), Vector2.zero, Vector2.zero);
            Text mark = CreateText(sigil.transform, "✦", 30, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            Stretch(mark.rectTransform);
            Text fallbackLabel = CreateText(panel.transform, label, 12, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);
            SetAnchors(fallbackLabel.rectTransform, new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.22f), Vector2.zero, Vector2.zero);
            return panel;
        }

        private static void ConfigureCardBackArt(GameObject officialArt, Image officialImage)
        {
            officialImage.preserveAspect = false;
            AspectRatioFitter fitter = officialArt.GetComponent<AspectRatioFitter>() ?? officialArt.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = officialImage.sprite != null && officialImage.sprite.rect.height > 0f
                ? officialImage.sprite.rect.width / officialImage.sprite.rect.height
                : 2f / 3f;

            // The finish belongs to the fitted art, not its surrounding layout slot.
            // This keeps the mask, foil edges, and physical card border coincident.
            PremiumCardPresentation.Attach(officialArt, "Rare");
        }

        public static Color ColorForType(string type)
        {
            if (type == GameConstants.Original)
            {
                return new Color(0.120f, 0.055f, 0.205f);
            }

            if (type == GameConstants.Companion)
            {
                return new Color(0.025f, 0.135f, 0.155f);
            }

            if (type == GameConstants.Item)
            {
                return new Color(0.185f, 0.125f, 0.030f);
            }

            if (type == GameConstants.Event)
            {
                return new Color(0.155f, 0.040f, 0.120f);
            }

            return new Color(0.085f, 0.075f, 0.135f);
        }

        public static Color ColorForRarity(string rarity)
        {
            if (rarity == "1/1")
            {
                return NeonPink;
            }

            if (rarity == "Legendary")
            {
                return Accent;
            }

            if (rarity == "Rare")
            {
                return NeonCyan;
            }

            return MutedTextColor;
        }

        private static void CreateCardArt(Transform parent, CardDefinition card, int preferredHeight)
        {
            GameObject artObject = CreatePanel(parent, "CardArt", new Color(0.94f, 0.84f, 0.66f, 0.96f));
            AddNeonFrame(artObject, WoodDark, 0.58f);
            LayoutElement layout = artObject.AddComponent<LayoutElement>();
            layout.minHeight = preferredHeight;
            layout.preferredHeight = preferredHeight;

            Image image = artObject.GetComponent<Image>();
            Sprite sprite = CardArtResolver.LoadSprite(card);
            if (sprite != null)
            {
                image.color = Hex("151056");
                artObject.AddComponent<RectMask2D>();
                CreateCoverImage(artObject.transform, "MetadataArt", sprite);
                return;
            }

            string shortType = string.IsNullOrWhiteSpace(card.type) ? "CARD" : card.type.Substring(0, Mathf.Min(3, card.type.Length));
            Text fallback = CreateText(artObject.transform, shortType, 30, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            Stretch(fallback.rectTransform);
        }

        public static Image CreateCoverImage(Transform parent, string name, Sprite sprite)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            AspectRatioFitter fitter = imageObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite == null || sprite.rect.height <= 0f ? 1f : sprite.rect.width / sprite.rect.height;
            return image;
        }

        public static Image CreateFitImage(Transform parent, string name, Sprite sprite)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            AspectRatioFitter fitter = imageObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = sprite == null || sprite.rect.height <= 0f ? 1f : sprite.rect.width / sprite.rect.height;
            return image;
        }

        private static void CreateBackdropPanel(Transform parent, string name, Color color, Vector2 min, Vector2 max, float rotation)
        {
            GameObject panel = CreatePanel(parent, name, color);
            Image image = panel.GetComponent<Image>();
            image.raycastTarget = false;
            RectTransform rect = panel.GetComponent<RectTransform>();
            SetAnchors(rect, min, max, Vector2.zero, Vector2.zero);
            rect.localRotation = Quaternion.Euler(0, 0, rotation);
        }

        private static void CreateBackdropImage(Transform parent, string name, string relativePath, Vector2 min, Vector2 max, Color tint)
        {
            GameObject imageObject = UIAssetPack.CreateImage(parent, name, relativePath, false);
            Image image = imageObject.GetComponent<Image>();
            if (image.sprite == null)
            {
                Object.Destroy(imageObject);
                return;
            }

            image.color = tint;
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            SetAnchors(rect, min, max, Vector2.zero, Vector2.zero);
        }

        private static bool CreateResourceBackdropImage(Transform parent, string name, string resourcePath, Vector2 min, Vector2 max, Color tint, bool cover)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            if (!UIAssetPack.ApplyResource(image, resourcePath, true))
            {
                Object.Destroy(imageObject);
                return false;
            }

            image.color = tint;
            image.raycastTarget = false;
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            SetAnchors(rect, min, max, Vector2.zero, Vector2.zero);

            if (cover && image.sprite != null)
            {
                AspectRatioFitter fitter = imageObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            }

            return true;
        }

        public static Sprite LoadPlaymatSprite(Rect normalizedRect)
        {
            string key = $"{normalizedRect.x:F4}:{normalizedRect.y:F4}:{normalizedRect.width:F4}:{normalizedRect.height:F4}";
            if (playmatSpriteCache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>(OfficialPlaymatResourcePath);
            if (texture == null)
            {
                Debug.LogError("Missing native official playmat at Resources/Art/Official/Board/app_playmat_native.png.");
                return null;
            }

            Rect pixelRect = new Rect(
                normalizedRect.x * texture.width,
                normalizedRect.y * texture.height,
                normalizedRect.width * texture.width,
                normalizedRect.height * texture.height);
            Sprite sprite = Sprite.Create(texture, pixelRect, new Vector2(0.5f, 0.5f), 100f);
            playmatSpriteCache[key] = sprite;
            return sprite;
        }

        public static void AddNeonFrame(GameObject target, Color color, float alpha)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            Color brandLine = Color.Lerp(Ink, color, 0.12f);
            outline.effectColor = new Color(brandLine.r, brandLine.g, brandLine.b, Mathf.Clamp01(alpha + 0.08f));
            outline.effectDistance = new Vector2(3.5f, -3.5f);
            outline.useGraphicAlpha = true;
        }

        private static void AddSoftShadow(GameObject target)
        {
            Shadow shadow = target.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = target.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0f, 0f, 0f, 0.30f);
            shadow.effectDistance = new Vector2(3f, -4f);
            shadow.useGraphicAlpha = true;
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, Mathf.Max(0, maxLength - 1)) + ".";
        }

        public static void Stretch(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        public static void Stretch(RectTransform rectTransform, float inset)
        {
            Stretch(rectTransform);
            rectTransform.offsetMin = new Vector2(inset, inset);
            rectTransform.offsetMax = new Vector2(-inset, -inset);
        }

        public static void SetAnchors(RectTransform rectTransform, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    child.SetActive(false);
                    Object.Destroy(child);
                }
                else
                {
                    Object.DestroyImmediate(child);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Font LoadDefaultFont()
        {
            if (cachedDefaultFont != null)
            {
                return cachedDefaultFont;
            }

            Font officialFont = Resources.Load<Font>("Art/Official/StyleGuide/AppreciatorsDisplay");
            if (officialFont != null)
            {
                cachedDefaultFont = officialFont;
                return cachedDefaultFont;
            }

            try
            {
                Font display = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial Black", "Segoe UI Semibold", "Arial" }, 16);
                if (display != null)
                {
                    cachedDefaultFont = display;
                    return cachedDefaultFont;
                }
            }
            catch
            {
            }

            try
            {
                Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacy != null)
                {
                    cachedDefaultFont = legacy;
                    return cachedDefaultFont;
                }
            }
            catch
            {
            }

            try
            {
                Font arial = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (arial != null)
                {
                    cachedDefaultFont = arial;
                    return cachedDefaultFont;
                }
            }
            catch
            {
            }

            cachedDefaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return cachedDefaultFont;
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString($"#{value}", out Color color) ? color : Color.magenta;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Color ReadableTextColor(Color background)
        {
            float luminance = background.r * 0.2126f + background.g * 0.7152f + background.b * 0.0722f;
            return luminance > 0.54f ? Ink : Color.white;
        }
    }
}
