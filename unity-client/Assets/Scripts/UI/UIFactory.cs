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
        public static readonly Color Background = new Color(0.010f, 0.014f, 0.030f);
        public static readonly Color Panel = new Color(0.030f, 0.026f, 0.070f, 0.92f);
        public static readonly Color PanelAlt = new Color(0.040f, 0.054f, 0.120f, 0.86f);
        public static readonly Color GlassPanel = new Color(0.025f, 0.030f, 0.060f, 0.52f);
        public static readonly Color BoardPanel = new Color(0.020f, 0.044f, 0.070f, 0.88f);
        public static readonly Color AlleyFloor = new Color(0.025f, 0.145f, 0.235f, 0.88f);
        public static readonly Color AlleyWall = new Color(0.180f, 0.050f, 0.180f, 0.78f);
        public static readonly Color Ink = new Color(0.015f, 0.010f, 0.028f, 0.95f);
        public static readonly Color Cream = new Color(1.00f, 0.88f, 0.58f);
        public static readonly Color HeartRed = new Color(1.00f, 0.18f, 0.34f);
        public static readonly Color Accent = new Color(1.00f, 0.74f, 0.20f);
        public static readonly Color Blue = new Color(0.06f, 0.54f, 0.96f);
        public static readonly Color Green = new Color(0.05f, 0.78f, 0.36f);
        public static readonly Color Red = new Color(1.00f, 0.24f, 0.38f);
        public static readonly Color Parchment = new Color(0.96f, 0.84f, 0.62f);
        public static readonly Color CreamInk = new Color(0.20f, 0.10f, 0.075f);
        public static readonly Color WoodDark = new Color(0.23f, 0.105f, 0.085f);
        public static readonly Color IceBadge = new Color(0.25f, 0.78f, 1.00f);
        public static readonly Color NeonCyan = new Color(0.04f, 0.92f, 1.00f);
        public static readonly Color NeonPink = new Color(1.00f, 0.16f, 0.64f);
        public static readonly Color PortalViolet = new Color(0.34f, 0.15f, 0.74f);
        public static readonly Color CardBack = new Color(0.105f, 0.055f, 0.022f);
        public static readonly Color TextColor = new Color(0.97f, 0.98f, 1.00f);
        public static readonly Color MutedTextColor = new Color(0.67f, 0.77f, 0.90f);

        public static Font DefaultFont => LoadDefaultFont();

        public static Canvas CreateCanvas(string name)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

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
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.onClick.AddListener(action);

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.disabledColor = new Color(0.20f, 0.21f, 0.25f);
            button.colors = colors;
            AddNeonFrame(buttonObject, Color.Lerp(color, NeonCyan, 0.35f), 0.52f);

            bool endTurnArtApplied = label == "END TURN" && UIAssetPack.Apply(buttonObject.GetComponent<Image>(), "04_ui/buttons/button_end_turn.png", false);
            if (endTurnArtApplied)
            {
                ColorBlock artColors = button.colors;
                artColors.normalColor = Color.white;
                artColors.highlightedColor = new Color(1f, 1f, 1f, 0.92f);
                artColors.pressedColor = new Color(0.86f, 0.78f, 0.68f, 1f);
                artColors.disabledColor = new Color(0.55f, 0.50f, 0.45f, 0.70f);
                button.colors = artColors;
            }

            bool isLongLabel = label.Length > 28 || label.Contains("\n");
            Text labelText = CreateText(buttonObject.transform, endTurnArtApplied ? string.Empty : label, isLongLabel ? 18 : 23, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);
            Stretch(labelText.rectTransform);

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.minHeight = 58;
            layout.preferredHeight = 64;

            return button;
        }

        public static GameObject CreateHudPlate(Transform parent, string playerName, int health, int energy, int maxEnergy, bool opponent)
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
            CreateEnergyPips(stats.transform, energy, maxEnergy, opponent);
            return hud;
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

        public static GameObject CreateEnergyPips(Transform parent, int energy, int maxEnergy, bool opponent)
        {
            GameObject row = CreateHorizontalStack(parent, "EnergyPips", Color.clear, 4, 0);
            HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
            group.childForceExpandWidth = false;
            LayoutElement layout = row.AddComponent<LayoutElement>();
            layout.minHeight = 22;
            layout.preferredHeight = 26;
            Text count = CreateText(row.transform, $"{energy}/{maxEnergy}", 17, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
            LayoutElement countLayout = count.gameObject.AddComponent<LayoutElement>();
            countLayout.minWidth = 52;
            countLayout.preferredWidth = 58;
            int pipCount = Mathf.Max(maxEnergy, GameConstants.MaxTurn);
            for (int i = 0; i < pipCount; i++)
            {
                GameObject pip = CreatePanel(row.transform, "EnergyPip", i < energy ? (opponent ? NeonCyan : Accent) : new Color(0.025f, 0.030f, 0.080f, 0.92f));
                LayoutElement pipLayout = pip.AddComponent<LayoutElement>();
                pipLayout.minWidth = 18;
                pipLayout.preferredWidth = 20;
                pipLayout.minHeight = 18;
                pipLayout.preferredHeight = 20;
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
            GameObject fieldObject = CreatePanel(parent, "InputField", new Color(0.035f, 0.035f, 0.075f));
            InputField input = fieldObject.AddComponent<InputField>();
            Image image = fieldObject.GetComponent<Image>();
            image.color = new Color(0.035f, 0.035f, 0.075f);

            Text text = CreateText(fieldObject.transform, value, 26, TextAnchor.MiddleLeft, TextColor);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16, 0);
            text.rectTransform.offsetMax = new Vector2(-16, 0);

            Text placeholderText = CreateText(fieldObject.transform, placeholder, 26, TextAnchor.MiddleLeft, MutedTextColor);
            Stretch(placeholderText.rectTransform);
            placeholderText.rectTransform.offsetMin = new Vector2(16, 0);
            placeholderText.rectTransform.offsetMax = new Vector2(-16, 0);

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
            GameObject scrollObject = CreatePanel(parent, name, new Color(0.035f, 0.040f, 0.075f));
            scrollRect = scrollObject.AddComponent<ScrollRect>();
            scrollRect.scrollSensitivity = 42f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            ScrollWheelRelay wheelRelay = scrollObject.AddComponent<ScrollWheelRelay>();
            wheelRelay.Target = scrollRect;
            LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1;
            scrollLayout.flexibleWidth = 1;

            GameObject viewport = CreatePanel(scrollObject.transform, "Viewport", new Color(0.035f, 0.040f, 0.075f));
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
            Color color = selected ? Accent : ColorForType(card.type);
            GameObject panel = CreateVerticalStack(parent, card.name, color, compact ? 4 : 7, compact ? 8 : 10);
            AddNeonFrame(panel, selected ? Accent : ColorForRarity(card.rarity), selected ? 0.78f : 0.42f);
            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = compact ? 176 : 225;
            layout.preferredWidth = compact ? 194 : 255;
            layout.flexibleWidth = compact ? 0 : 1;
            layout.minHeight = compact ? 184 : 230;
            layout.preferredHeight = compact ? 202 : 255;
            layout.flexibleHeight = 0;

            if (action != null)
            {
                Button button = panel.AddComponent<Button>();
                button.targetGraphic = panel.GetComponent<Image>();
                button.onClick.AddListener(action);

                ColorBlock colors = button.colors;
                colors.normalColor = color;
                colors.highlightedColor = Color.Lerp(color, Color.white, 0.10f);
                colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
                colors.disabledColor = new Color(0.20f, 0.21f, 0.25f);
                button.colors = colors;
            }

            CreateCardArt(panel.transform, card, compact ? 92 : 88);

            string affinity = string.IsNullOrWhiteSpace(card.laneAffinity) ? "Any lane" : card.laneAffinity;
            string title = selected ? $"SELECTED\n{card.name}" : card.name;
            if (compact)
            {
                CreateText(panel.transform, title, 18, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
                CreateText(panel.transform, $"COST {card.cost}   POW {card.power}   APP {card.appreciation}", 16, TextAnchor.MiddleLeft, Accent, FontStyle.Bold);
                CreateText(panel.transform, $"{card.rarity} | {card.type}", 13, TextAnchor.MiddleLeft, MutedTextColor, FontStyle.Bold);
            }
            else
            {
                CreateText(panel.transform, title, 21, TextAnchor.MiddleLeft, TextColor, FontStyle.Bold);
                CreateText(panel.transform, $"Cost {card.cost} | Power {card.power} | Appreciation {card.appreciation}", 17, TextAnchor.MiddleLeft, Accent, FontStyle.Bold);
                CreateText(panel.transform, $"{card.rarity} | {card.type} | {card.traitGroup}", 17, TextAnchor.MiddleLeft, TextColor);
                CreateText(panel.transform, affinity, 16, TextAnchor.MiddleLeft, MutedTextColor);
                CreateText(panel.transform, card.effectText, 17, TextAnchor.UpperLeft, TextColor);
            }

            if (!string.IsNullOrWhiteSpace(footer))
            {
                CreateText(panel.transform, footer, compact ? 14 : 17, TextAnchor.MiddleLeft, Accent, FontStyle.Bold);
            }

            return panel;
        }

        public static GameObject CreateMiniCardPanel(Transform parent, CardDefinition card, string stats, bool selected = false)
        {
            Color color = selected ? Accent : new Color(0.82f, 0.56f, 0.32f, 0.98f);
            GameObject panel = CreateVerticalStack(parent, card.name, color, 1, 4);
            Image image = panel.GetComponent<Image>();
            if (!UIAssetPack.ApplyResource(image, "Art/Placeholder/UserMock/appcardframe_alpha", false))
            {
                image.color = color;
            }

            AddNeonFrame(panel, selected ? Accent : WoodDark, selected ? 0.82f : 0.66f);
            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = 70;
            layout.preferredWidth = 82;
            layout.flexibleWidth = 0;
            layout.minHeight = 72;
            layout.preferredHeight = 84;
            layout.flexibleHeight = 0;

            CreateCardArt(panel.transform, card, 34);
            CreateText(panel.transform, Shorten(card.name, 12), 10, TextAnchor.MiddleCenter, CreamInk, FontStyle.Bold);
            CreateText(panel.transform, stats, 12, TextAnchor.MiddleCenter, WoodDark, FontStyle.Bold);
            return panel;
        }

        public static GameObject CreateMatchHandCardPanel(Transform parent, CardDefinition card, UnityAction action, bool selected = false, string footer = null)
        {
            Color buttonColor = selected ? Accent : new Color(0.78f, 0.54f, 0.32f, 0.98f);
            GameObject panel = CreateVerticalStack(parent, card.name, buttonColor, 2, 8);
            Image panelImage = panel.GetComponent<Image>();
            if (!UIAssetPack.ApplyResource(panelImage, "Art/Placeholder/UserMock/appcardframe_alpha", false))
            {
                panelImage.color = buttonColor;
            }

            AddNeonFrame(panel, selected ? Accent : WoodDark, selected ? 0.92f : 0.82f);

            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = 142;
            layout.preferredWidth = 142;
            layout.flexibleWidth = 0;
            layout.minHeight = 212;
            layout.preferredHeight = 212;
            layout.flexibleHeight = 0;

            if (action != null)
            {
                Button button = panel.AddComponent<Button>();
                button.targetGraphic = panel.GetComponent<Image>();
                button.onClick.AddListener(action);

                ColorBlock colors = button.colors;
                colors.normalColor = buttonColor;
                colors.highlightedColor = Color.Lerp(buttonColor, Color.white, 0.10f);
                colors.pressedColor = Color.Lerp(buttonColor, Color.black, 0.18f);
                colors.disabledColor = new Color(0.20f, 0.21f, 0.25f);
                button.colors = colors;
            }

            CreateCardArt(panel.transform, card, 68);

            GameObject titleBar = CreateHorizontalStack(panel.transform, "CardTitleBar", new Color(0.96f, 0.87f, 0.69f, 0.98f), 5, 5);
            AddNeonFrame(titleBar, WoodDark, 0.72f);
            LayoutElement titleLayout = titleBar.AddComponent<LayoutElement>();
            titleLayout.minHeight = 25;
            titleLayout.preferredHeight = 27;
            titleLayout.flexibleHeight = 0;
            GameObject costBadge = CreatePanel(titleBar.transform, "CostBadge", IceBadge);
            AddNeonFrame(costBadge, Color.white, 0.72f);
            LayoutElement costLayout = costBadge.AddComponent<LayoutElement>();
            costLayout.minWidth = 25;
            costLayout.preferredWidth = 27;
            costLayout.flexibleWidth = 0;
            Text costText = CreateText(costBadge.transform, card.cost.ToString(), 16, TextAnchor.MiddleCenter, CreamInk, FontStyle.Bold);
            Stretch(costText.rectTransform);
            Text nameText = CreateText(titleBar.transform, Shorten(card.name, 16), 13, TextAnchor.MiddleLeft, CreamInk, FontStyle.Bold);
            LayoutElement nameLayout = nameText.gameObject.AddComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1;

            GameObject statsBar = CreateHorizontalStack(panel.transform, "CardStats", new Color(0.91f, 0.74f, 0.49f, 0.96f), 5, 4);
            LayoutElement statsLayout = statsBar.AddComponent<LayoutElement>();
            statsLayout.minHeight = 20;
            statsLayout.preferredHeight = 22;
            statsLayout.flexibleHeight = 0;
            CreateStatLabel(statsBar.transform, "POW", card.power, HeartRed);
            CreateStatLabel(statsBar.transform, "APP", card.appreciation, IceBadge);

            Text typeText = CreateText(panel.transform, $"{card.rarity} | {card.type}", 10, TextAnchor.MiddleLeft, CreamInk, FontStyle.Bold);
            LayoutElement typeLayout = typeText.gameObject.AddComponent<LayoutElement>();
            typeLayout.minHeight = 12;
            typeLayout.preferredHeight = 14;
            typeLayout.flexibleHeight = 0;

            GameObject rulesBox = CreatePanel(panel.transform, "RulesText", new Color(0.24f, 0.13f, 0.115f, 0.96f));
            AddNeonFrame(rulesBox, new Color(0.86f, 0.66f, 0.42f), 0.70f);
            LayoutElement rulesLayout = rulesBox.AddComponent<LayoutElement>();
            rulesLayout.minHeight = 32;
            rulesLayout.preferredHeight = 36;
            rulesLayout.flexibleHeight = 1;
            Text effectText = CreateText(rulesBox.transform, Shorten(card.effectText, 82), 11, TextAnchor.UpperLeft, new Color(1.0f, 0.93f, 0.82f), FontStyle.Bold);
            Stretch(effectText.rectTransform);
            effectText.rectTransform.offsetMin = new Vector2(4, 2);
            effectText.rectTransform.offsetMax = new Vector2(-4, -2);
            effectText.resizeTextForBestFit = true;
            effectText.resizeTextMinSize = 8;
            effectText.resizeTextMaxSize = 11;

            if (!string.IsNullOrWhiteSpace(footer))
            {
                Text footerText = CreateText(panel.transform, footer, 9, TextAnchor.MiddleLeft, CreamInk, FontStyle.Bold);
                LayoutElement footerLayout = footerText.gameObject.AddComponent<LayoutElement>();
                footerLayout.minHeight = 10;
                footerLayout.preferredHeight = 11;
                footerLayout.flexibleHeight = 0;
            }

            return panel;
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
            GameObject panel = CreateVerticalStack(parent, "CardBack", new Color(0.23f, 0.075f, 0.32f, 0.96f), 3, 6);
            AddNeonFrame(panel, Cream, 0.82f);
            LayoutElement layout = panel.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0;
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0;

            int variant = Mathf.Max(0, parent.childCount - 1) % 4 + 1;
            if (UIAssetPack.Apply(panel.GetComponent<Image>(), $"04_ui/card_backs_and_decks/opponent_hand_card_back_{variant:00}.png", true))
            {
                return panel;
            }

            GameObject sigil = CreatePanel(panel.transform, "BackSigil", new Color(0.40f, 0.12f, 0.50f, 0.95f));
            LayoutElement sigilLayout = sigil.AddComponent<LayoutElement>();
            sigilLayout.minHeight = 42;
            sigilLayout.preferredHeight = 50;
            Text mark = CreateText(sigil.transform, "✦", 30, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            Stretch(mark.rectTransform);
            CreateText(panel.transform, label, 12, TextAnchor.MiddleCenter, TextColor, FontStyle.Bold);
            return panel;
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
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                return;
            }

            string shortType = string.IsNullOrWhiteSpace(card.type) ? "CARD" : card.type.Substring(0, Mathf.Min(3, card.type.Length));
            Text fallback = CreateText(artObject.transform, shortType, 30, TextAnchor.MiddleCenter, Accent, FontStyle.Bold);
            Stretch(fallback.rectTransform);
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

        private static void AddNeonFrame(GameObject target, Color color, float alpha)
        {
            Outline outline = target.GetComponent<Outline>();
            if (outline == null)
            {
                outline = target.AddComponent<Outline>();
            }

            outline.effectColor = new Color(color.r, color.g, color.b, alpha);
            outline.effectDistance = new Vector2(2f, -2f);
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
                Object.Destroy(parent.GetChild(i).gameObject);
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
            try
            {
                Font display = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial Black", "Segoe UI Semibold", "Arial" }, 16);
                if (display != null)
                {
                    return display;
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
                    return legacy;
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
                    return arial;
                }
            }
            catch
            {
            }

            return Font.CreateDynamicFontFromOSFont("Arial", 16);
        }
    }
}
