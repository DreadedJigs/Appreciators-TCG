using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public static class UIFactory
    {
        public static readonly Color Background = new Color(0.05f, 0.06f, 0.09f);
        public static readonly Color Panel = new Color(0.12f, 0.14f, 0.19f);
        public static readonly Color PanelAlt = new Color(0.16f, 0.18f, 0.24f);
        public static readonly Color Accent = new Color(0.93f, 0.70f, 0.21f);
        public static readonly Color Blue = new Color(0.25f, 0.45f, 0.95f);
        public static readonly Color Green = new Color(0.23f, 0.70f, 0.46f);
        public static readonly Color Red = new Color(0.78f, 0.25f, 0.30f);
        public static readonly Color TextColor = new Color(0.94f, 0.95f, 0.98f);
        public static readonly Color MutedTextColor = new Color(0.70f, 0.74f, 0.82f);

        public static Font DefaultFont => Resources.GetBuiltinResource<Font>("Arial.ttf");

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
            panel.GetComponent<Image>().color = color;
            return panel;
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

            bool isLongLabel = label.Length > 28 || label.Contains("\n");
            Text labelText = CreateText(buttonObject.transform, label, isLongLabel ? 18 : 24, TextAnchor.MiddleCenter, TextColor, isLongLabel ? FontStyle.Normal : FontStyle.Bold);
            Stretch(labelText.rectTransform);

            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.minHeight = 58;
            layout.preferredHeight = 64;

            return button;
        }

        public static InputField CreateInputField(Transform parent, string placeholder, string value)
        {
            GameObject fieldObject = CreatePanel(parent, "InputField", new Color(0.08f, 0.09f, 0.13f));
            InputField input = fieldObject.AddComponent<InputField>();
            Image image = fieldObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.09f, 0.13f);

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

        public static RectTransform CreateScrollContent(Transform parent, string name, bool horizontal, out ScrollRect scrollRect)
        {
            GameObject scrollObject = CreatePanel(parent, name, new Color(0.07f, 0.08f, 0.12f));
            scrollRect = scrollObject.AddComponent<ScrollRect>();
            LayoutElement scrollLayout = scrollObject.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1;
            scrollLayout.flexibleWidth = 1;

            GameObject viewport = CreatePanel(scrollObject.transform, "Viewport", new Color(0.07f, 0.08f, 0.12f));
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            Stretch(viewport.GetComponent<RectTransform>());

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);

            if (horizontal)
            {
                HorizontalLayoutGroup layout = content.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10;
                layout.padding = new RectOffset(12, 12, 12, 12);
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = true;
                ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10;
                layout.padding = new RectOffset(12, 12, 12, 12);
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
    }
}
