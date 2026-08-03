using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    [RequireComponent(typeof(InputField))]
    public class InputFieldContextMenu : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        private static GameObject activeMenu;
        private InputField input;
        private bool rightPressHandled;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void AppreciatorsPasteText(string gameObjectName, string successMethod, string errorMethod);
#endif

        private void Awake()
        {
            input = GetComponent<InputField>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right) return;
            if (rightPressHandled) return;
            eventData.Use();
            ShowMenu(eventData.position);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right) return;
            rightPressHandled = true;
            input?.ActivateInputField();
            eventData.Use();
            ShowMenu(eventData.position);
        }

        private void LateUpdate()
        {
            // PointerDown is used because some WebGL browsers do not dispatch a
            // right-button PointerClick to Unity's InputField.
            rightPressHandled = false;
        }

        private void ShowMenu(Vector2 screenPosition)
        {
            CloseMenu();
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            activeMenu = new GameObject("InputClipboardMenu", typeof(RectTransform), typeof(Image), typeof(Button));
            activeMenu.transform.SetParent(canvas.transform, false);
            UIFactory.Stretch(activeMenu.GetComponent<RectTransform>());
            Image backdrop = activeMenu.GetComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.01f);
            activeMenu.GetComponent<Button>().onClick.AddListener(CloseMenu);

            GameObject menu = UIFactory.CreateVerticalStack(activeMenu.transform, "ClipboardActions", UIFactory.PanelAlt, 5, 7);
            UIFactory.MakeDimensionalPanel(menu, UIFactory.NeonCyan);
            RectTransform rect = menu.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(205f, 202f);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, canvas.worldCamera, out Vector2 localPoint);
            float maxX = canvasRect.rect.xMax - rect.sizeDelta.x - 8f;
            float minY = canvasRect.rect.yMin + rect.sizeDelta.y + 8f;
            rect.anchoredPosition = new Vector2(
                Mathf.Clamp(localPoint.x, canvasRect.rect.xMin + 8f, maxX),
                Mathf.Clamp(localPoint.y, minY, canvasRect.rect.yMax - 8f));

            CreateAction(menu.transform, "COPY", Copy);
            CreateAction(menu.transform, "PASTE", Paste);
            CreateAction(menu.transform, "CUT", Cut);
            CreateAction(menu.transform, "SELECT ALL", SelectAll);
            menu.transform.SetAsLastSibling();
        }

        private static void CreateAction(Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            Button button = UIFactory.CreateButton(parent, label, action, UIFactory.PortalViolet);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minHeight = 40f;
                layout.preferredHeight = 42f;
            }
            Text text = button.GetComponentInChildren<Text>();
            if (text != null) text.fontSize = 16;
        }

        private void Copy()
        {
            if (input == null) return;
            string selected = SelectedText();
            GUIUtility.systemCopyBuffer = string.IsNullOrEmpty(selected) ? input.text : selected;
            CloseMenu();
        }

        private void Paste()
        {
            if (input == null || input.readOnly) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            AppreciatorsPasteText(gameObject.name, nameof(PasteFromBrowserClipboard), nameof(PasteFromBrowserClipboardError));
            CloseMenu();
#else
            ReplaceSelection(GUIUtility.systemCopyBuffer ?? string.Empty);
            CloseMenu();
#endif
        }

        // Invoked from the WebGL clipboard bridge after the user selects Paste
        // from the right-click menu. This uses the browser's real clipboard,
        // not a Unity-only buffer.
        public void PasteFromBrowserClipboard(string value)
        {
            if (input == null || input.readOnly) return;
            ReplaceSelection(value ?? string.Empty);
        }

        public void PasteFromBrowserClipboardError(string error)
        {
            Debug.LogWarning($"Browser clipboard paste failed: {error}");
        }

        private void Cut()
        {
            if (input == null || input.readOnly) return;
            string selected = SelectedText();
            if (string.IsNullOrEmpty(selected)) selected = input.text;
            GUIUtility.systemCopyBuffer = selected;
            if (SelectedLength() == 0) input.text = string.Empty;
            else ReplaceSelection(string.Empty);
            CloseMenu();
        }

        private void SelectAll()
        {
            if (input == null) return;
            input.ActivateInputField();
            input.selectionAnchorPosition = 0;
            input.selectionFocusPosition = input.text?.Length ?? 0;
            CloseMenu();
        }

        private string SelectedText()
        {
            if (input == null || string.IsNullOrEmpty(input.text)) return string.Empty;
            int start = Mathf.Min(input.selectionAnchorPosition, input.selectionFocusPosition);
            int length = SelectedLength();
            return length <= 0 ? string.Empty : input.text.Substring(start, length);
        }

        private int SelectedLength()
        {
            return input == null ? 0 : Mathf.Abs(input.selectionFocusPosition - input.selectionAnchorPosition);
        }

        private void ReplaceSelection(string value)
        {
            int start = Mathf.Min(input.selectionAnchorPosition, input.selectionFocusPosition);
            int end = Mathf.Max(input.selectionAnchorPosition, input.selectionFocusPosition);
            string current = input.text ?? string.Empty;
            input.text = current.Substring(0, start) + value + current.Substring(end);
            int caret = start + value.Length;
            input.caretPosition = caret;
            input.selectionAnchorPosition = caret;
            input.selectionFocusPosition = caret;
            input.ActivateInputField();
        }

        private static void CloseMenu()
        {
            if (activeMenu == null) return;
            Object.Destroy(activeMenu);
            activeMenu = null;
        }
    }
}
