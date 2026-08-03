using System.Collections;
using AppreciatorsTcg.Audio;
using AppreciatorsTcg.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class CardInspectionTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler
    {
        private const float MouseHoldSeconds = 0.38f;
        private const float TouchHoldSeconds = 0.46f;
        private Coroutine holdRoutine;
        private bool pointerHeld;
        private bool pressPreviewVisible;
        private bool touchInput;
        private Vector2 holdPosition;
        private bool suppressClick;

        public CardDefinition Card { get; set; }
        public bool ClickToInspect { get; set; }
        public bool SuppressesCardPlay => suppressClick || pressPreviewVisible;

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Inspection is intentionally press-driven on every platform. Hovering alone
            // must not cover the hand or interfere with choosing and dragging cards.
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // A card inspection is modal and stays open until the player presses Back.
            // Pointer exit must not close it, especially while a mobile finger scrolls.
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            CancelPendingPreview();
            CardInspectionOverlay.Hide();
            pointerHeld = true;
            pressPreviewVisible = false;
            holdPosition = eventData == null ? Vector2.zero : eventData.position;
            touchInput = Application.isMobilePlatform || (eventData != null && eventData.pointerId >= 0);
            holdRoutine = StartCoroutine(ShowPreviewAfterHold(touchInput ? TouchHoldSeconds : MouseHoldSeconds));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            bool previewWasVisible = pressPreviewVisible;
            pointerHeld = false;
            CancelPendingPreview();
            pressPreviewVisible = false;
            if (previewWasVisible)
            {
                suppressClick = true;
                StartCoroutine(SuppressButtonClickForFrame());
                eventData?.Use();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (ClickToInspect && !suppressClick && Card != null)
            {
                bool mobile = Application.isMobilePlatform || (eventData != null && eventData.pointerId >= 0);
                CardInspectionOverlay.Show(Card, eventData == null ? holdPosition : eventData.position, mobile);
                eventData?.Use();
                return;
            }

            if (!suppressClick)
            {
                return;
            }

            eventData?.Use();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            pointerHeld = false;
            CancelPendingPreview();
            pressPreviewVisible = false;
            CardInspectionOverlay.Hide();
        }

        private IEnumerator ShowPreviewAfterHold(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            holdRoutine = null;
            if (!pointerHeld)
            {
                yield break;
            }

            pressPreviewVisible = true;
            CardInspectionOverlay.Show(Card, holdPosition, touchInput);
        }

        private void CancelPendingPreview()
        {
            if (holdRoutine == null)
            {
                return;
            }

            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        private void OnDisable()
        {
            pointerHeld = false;
            CancelPendingPreview();
            pressPreviewVisible = false;
        }

        private IEnumerator SuppressButtonClickForFrame()
        {
            Button button = GetComponent<Button>();
            bool wasInteractable = button == null || button.interactable;
            if (button != null)
            {
                button.interactable = false;
            }

            yield return null;
            yield return null;

            if (button != null)
            {
                button.interactable = wasInteractable;
            }

            suppressClick = false;
        }
    }

    public static class CardInspectionOverlay
    {
        private static GameObject overlay;
        private static RectTransform detailRect;
        private static CanvasGroup overlayGroup;

        public static void Show(CardDefinition card)
        {
            Show(card, Vector2.zero, false);
        }

        public static void Show(CardDefinition card, Vector2 screenPosition, bool mobileTouch)
        {
            if (card == null)
            {
                return;
            }

            Hide();

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            UiAudioService.PlayInspect();

            overlay = new GameObject("CardInspectionOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
            overlay.transform.SetParent(canvas.transform, false);
            overlayGroup = overlay.AddComponent<CanvasGroup>();
            overlayGroup.alpha = 0f;
            overlayGroup.blocksRaycasts = true;
            overlayGroup.interactable = true;
            Image dim = overlay.GetComponent<Image>();
            dim.color = new Color(0.005f, 0.008f, 0.025f, 0.46f);
            dim.raycastTarget = true;
            UIFactory.Stretch(overlay.GetComponent<RectTransform>());

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            bool narrowPortrait = canvasRect != null && canvasRect.rect.height > canvasRect.rect.width * 1.08f;
            bool compactViewport = canvasRect != null && (canvasRect.rect.width < 980f || canvasRect.rect.height < 720f);
            GameObject safeRoot = new GameObject("SafeArea", typeof(RectTransform));
            safeRoot.transform.SetParent(overlay.transform, false);
            Rect safe = Screen.safeArea;
            Vector2 safeMin = new Vector2(Screen.width > 0 ? safe.xMin / Screen.width : 0f, Screen.height > 0 ? safe.yMin / Screen.height : 0f);
            Vector2 safeMax = new Vector2(Screen.width > 0 ? safe.xMax / Screen.width : 1f, Screen.height > 0 ? safe.yMax / Screen.height : 1f);
            UIFactory.SetAnchors(safeRoot.GetComponent<RectTransform>(), safeMin, safeMax, Vector2.zero, Vector2.zero);

            GameObject detail = UIFactory.CreateCardPanel(safeRoot.transform, card);
            detail.name = "CardPreview";
            detailRect = detail.GetComponent<RectTransform>();
            detailRect.anchorMin = narrowPortrait ? new Vector2(0.24f, 0.56f) : compactViewport || mobileTouch ? new Vector2(0.04f, 0.12f) : new Vector2(0.12f, 0.12f);
            detailRect.anchorMax = narrowPortrait ? new Vector2(0.76f, 0.94f) : compactViewport || mobileTouch ? new Vector2(0.42f, 0.88f) : new Vector2(0.40f, 0.88f);
            detailRect.offsetMin = Vector2.zero;
            detailRect.offsetMax = Vector2.zero;
            LayoutElement detailLayout = detail.GetComponent<LayoutElement>();
            if (detailLayout != null) Object.Destroy(detailLayout);
            Image detailBackground = detail.GetComponent<Image>();
            if (detailBackground != null) detailBackground.color = Color.clear;
            DisableRaycasts(detail);

            RectTransform rulesContent = UIFactory.CreateScrollContent(safeRoot.transform, "DetailedRules", false, out ScrollRect rulesScroll);
            RectTransform rulesRect = rulesScroll.GetComponent<RectTransform>();
            rulesRect.anchorMin = narrowPortrait ? new Vector2(0.05f, 0.13f) : compactViewport || mobileTouch ? new Vector2(0.45f, 0.18f) : new Vector2(0.43f, 0.18f);
            rulesRect.anchorMax = narrowPortrait ? new Vector2(0.95f, 0.545f) : compactViewport || mobileTouch ? new Vector2(0.95f, 0.92f) : new Vector2(0.91f, 0.92f);
            rulesRect.offsetMin = Vector2.zero;
            rulesRect.offsetMax = Vector2.zero;
            rulesScroll.scrollSensitivity = 54f;
            Image rulesBackground = rulesScroll.GetComponent<Image>();
            if (rulesBackground != null) rulesBackground.color = new Color(0.018f, 0.022f, 0.065f, 0.97f);

            VerticalLayoutGroup rulesLayout = rulesContent.GetComponent<VerticalLayoutGroup>();
            if (rulesLayout != null)
            {
                rulesLayout.enabled = false;
            }
            ContentSizeFitter rulesFitter = rulesContent.GetComponent<ContentSizeFitter>();
            if (rulesFitter != null) rulesFitter.enabled = false;

            int bodySize = narrowPortrait ? 18 : compactViewport || mobileTouch ? 20 : 18;
            string rulesDocument =
                $"<color=#{ColorUtility.ToHtmlStringRGB(UIFactory.NeonCyan)}>BUILD</color>\n\n" +
                $"Attack: {card.GetAttack()}   Defense: {card.GetDefense()}\n" +
                $"Growth: {card.GetBaseGrowth()}\n\n" +
                $"Board ability: {card.GetBuildEffect()}\n\n" +
                $"Archetype: {card.GetArchetype()}\n" +
                $"{UIFactory.PillarSymbol(card.GetPillar())} Pillar: {card.GetPillar()}\n" +
                $"{UIFactory.RaritySymbol(card.rarity)} Rarity: {card.rarity}\n" +
                "Active effects: none in hand\n\n\n" +
                $"<color=#{ColorUtility.ToHtmlStringRGB(UIFactory.Accent)}>DISCARDED</color>\n\n" +
                $"{card.GetDiscardEffect()}\n\n" +
                $"Category: {card.GetDiscardCategory()}\n" +
                $"Targets: {card.discardTargets}\n" +
                $"Condition: {card.discardCondition}\n" +
                $"Appreciation: {card.discardAppreciationChange:+#;-#;0}\n" +
                $"Growth: {card.discardGrowthChange:+#;-#;0}\n" +
                $"Board cost: {(string.IsNullOrWhiteSpace(card.discardBoardCost) ? "None" : card.discardBoardCost)}\n" +
                $"Duration: {card.discardEffectDuration}\n" +
                $"Source: {card.discardEffectSource}";

            rulesContent.anchorMin = new Vector2(0f, 1f);
            rulesContent.anchorMax = new Vector2(1f, 1f);
            rulesContent.pivot = new Vector2(0.5f, 1f);
            rulesContent.anchoredPosition = Vector2.zero;
            rulesContent.sizeDelta = new Vector2(0f, narrowPortrait ? 760f : compactViewport || mobileTouch ? 820f : 900f);

            Text rulesText = UIFactory.CreateText(rulesContent, rulesDocument, bodySize, TextAnchor.UpperLeft, UIFactory.TextColor, FontStyle.Bold);
            rulesText.supportRichText = true;
            rulesText.lineSpacing = 1.22f;
            rulesText.horizontalOverflow = HorizontalWrapMode.Wrap;
            rulesText.verticalOverflow = VerticalWrapMode.Overflow;
            rulesText.raycastTarget = false;
            UIFactory.SetAnchors(rulesText.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -18f));
            Canvas.ForceUpdateCanvases();
            float viewportHeight = Mathf.Max(1f, rulesRect.rect.height);
            float readableDocumentHeight = rulesText.preferredHeight + 44f;
            rulesContent.sizeDelta = new Vector2(0f, Mathf.Max(viewportHeight, readableDocumentHeight));
            rulesScroll.verticalNormalizedPosition = 1f;

            Button close = UIFactory.CreateButton(safeRoot.transform, "BACK", Hide, UIFactory.PortalViolet);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = narrowPortrait ? new Vector2(0.05f, 0.055f) : compactViewport || mobileTouch ? new Vector2(0.45f, 0.09f) : new Vector2(0.43f, 0.09f);
            closeRect.anchorMax = narrowPortrait ? new Vector2(0.95f, 0.115f) : compactViewport || mobileTouch ? new Vector2(0.95f, 0.16f) : new Vector2(0.91f, 0.16f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            close.transform.SetAsLastSibling();

            CardInspectionGestureController gestures = overlay.AddComponent<CardInspectionGestureController>();
            gestures.Configure(detailRect, rulesRect, rulesText, rulesContent, rulesScroll);
            CardInspectionOverlayAnimator animator = overlay.AddComponent<CardInspectionOverlayAnimator>();
            animator.PlayIn(detailRect, overlayGroup);
        }

        private static void AddRulesSection(Transform parent, string value, int fontSize, Color color)
        {
            Text text = UIFactory.CreateText(parent, value, fontSize, TextAnchor.UpperLeft, color, FontStyle.Bold);
            text.lineSpacing = 1.35f;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            LayoutElement layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.preferredWidth = 0f;
            layout.flexibleWidth = 1f;
            int lineCount = Mathf.Max(1, value.Split('\n').Length);
            float measuredHeight = lineCount * fontSize * 1.48f + 18f;
            layout.minHeight = fontSize + 16f;
            layout.preferredHeight = Mathf.Max(measuredHeight, text.preferredHeight + 12f);
            layout.flexibleHeight = 0f;
        }

        public static void Hide()
        {
            if (overlay == null)
            {
                return;
            }

            overlay.SetActive(false);
            Object.Destroy(overlay);
            overlay = null;
            detailRect = null;
            overlayGroup = null;
        }

        public static IEnumerator HideAnimated(Vector2 screenPosition)
        {
            if (overlay == null || detailRect == null)
            {
                Hide();
                yield break;
            }

            GameObject closingOverlay = overlay;
            RectTransform closingDetail = detailRect;
            CanvasGroup closingGroup = overlayGroup;
            overlay = null;
            detailRect = null;
            overlayGroup = null;

            RectTransform canvasRect = closingOverlay.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            Vector2 target = closingDetail.anchoredPosition;
            if (canvasRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out target);
            }

            Vector2 startPosition = closingDetail.anchoredPosition;
            Vector3 startScale = closingDetail.localScale;
            float elapsed = 0f;
            const float duration = 0.18f;
            while (elapsed < duration && closingOverlay != null && closingDetail != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                closingDetail.anchoredPosition = Vector2.LerpUnclamped(startPosition, target, eased);
                closingDetail.localScale = Vector3.LerpUnclamped(startScale, Vector3.one * 0.18f, eased);
                if (closingGroup != null)
                {
                    closingGroup.alpha = 1f - eased;
                }

                yield return null;
            }

            if (closingOverlay != null)
            {
                Object.Destroy(closingOverlay);
            }
        }

        private static void PositionMobilePreview(RectTransform canvasRect, RectTransform detailRect, Vector2 screenPosition)
        {
            detailRect.anchorMin = new Vector2(0.5f, 0.5f);
            detailRect.anchorMax = new Vector2(0.5f, 0.5f);
            detailRect.pivot = new Vector2(0.5f, 0f);

            float width = Mathf.Min(canvasRect.rect.width * 0.72f, 430f);
            float height = Mathf.Min(canvasRect.rect.height * 0.70f, 520f);
            detailRect.sizeDelta = new Vector2(width, height);

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out localPoint);
            Vector2 target = localPoint + new Vector2(0f, 34f);
            float minX = canvasRect.rect.xMin + width * 0.5f + 12f;
            float maxX = canvasRect.rect.xMax - width * 0.5f - 12f;
            float minY = canvasRect.rect.yMin + 16f;
            float maxY = canvasRect.rect.yMax - height - 16f;
            detailRect.anchoredPosition = new Vector2(Mathf.Clamp(target.x, minX, maxX), Mathf.Clamp(target.y, minY, maxY));
        }

        private static void DisableRaycasts(GameObject root)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }
    }

    public class CardInspectionGestureController : MonoBehaviour
    {
        private const float MinZoom = 0.82f;
        private const float MaxZoom = 2.0f;
        private const float DoubleTapSeconds = 0.34f;
        private const float DoubleTapDistance = 90f;
        private RectTransform cardRect;
        private RectTransform rulesRect;
        private RectTransform rulesContent;
        private Text rulesText;
        private ScrollRect rulesScroll;
        private Vector3 originalCardScale = Vector3.one;
        private int originalRulesFontSize;
        private float cardZoom = 1f;
        private float rulesZoom = 1f;
        private float pinchStartDistance;
        private float pinchStartZoom = 1f;
        private bool pinchingRules;
        private float lastTapTime = -10f;
        private Vector2 lastTapPosition;

        public void Configure(RectTransform card, RectTransform rules, Text text, RectTransform content, ScrollRect scroll)
        {
            cardRect = card;
            rulesRect = rules;
            rulesText = text;
            rulesContent = content;
            rulesScroll = scroll;
            originalCardScale = card == null ? Vector3.one : card.localScale;
            originalRulesFontSize = text == null ? 18 : text.fontSize;
        }

        private void Update()
        {
            if (Input.touchCount >= 2)
            {
                Touch first = Input.GetTouch(0);
                Touch second = Input.GetTouch(1);
                Vector2 midpoint = (first.position + second.position) * 0.5f;
                float distance = Vector2.Distance(first.position, second.position);
                if (first.phase == TouchPhase.Began || second.phase == TouchPhase.Began || pinchStartDistance <= 0f)
                {
                    pinchStartDistance = Mathf.Max(1f, distance);
                    pinchingRules = rulesRect != null && RectTransformUtility.RectangleContainsScreenPoint(rulesRect, midpoint, null);
                    pinchStartZoom = pinchingRules ? rulesZoom : cardZoom;
                    return;
                }

                float nextZoom = Mathf.Clamp(pinchStartZoom * distance / pinchStartDistance, MinZoom, MaxZoom);
                if (pinchingRules) ApplyRulesZoom(nextZoom);
                else ApplyCardZoom(nextZoom);
                return;
            }

            pinchStartDistance = 0f;
            if (Input.touchCount != 1) return;
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Ended) return;
            float now = Time.unscaledTime;
            if (now - lastTapTime <= DoubleTapSeconds && Vector2.Distance(touch.position, lastTapPosition) <= DoubleTapDistance)
            {
                ResetZoom();
                lastTapTime = -10f;
            }
            else
            {
                lastTapTime = now;
                lastTapPosition = touch.position;
            }
        }

        private void ApplyCardZoom(float zoom)
        {
            cardZoom = zoom;
            if (cardRect != null) cardRect.localScale = originalCardScale * cardZoom;
        }

        private void ApplyRulesZoom(float zoom)
        {
            rulesZoom = zoom;
            if (rulesText == null || rulesContent == null || rulesRect == null) return;
            float previousPosition = rulesScroll == null ? 1f : rulesScroll.verticalNormalizedPosition;
            rulesText.fontSize = Mathf.RoundToInt(originalRulesFontSize * rulesZoom);
            Canvas.ForceUpdateCanvases();
            rulesContent.sizeDelta = new Vector2(0f, Mathf.Max(rulesRect.rect.height, rulesText.preferredHeight + 44f));
            if (rulesScroll != null) rulesScroll.verticalNormalizedPosition = previousPosition;
        }

        private void ResetZoom()
        {
            ApplyCardZoom(1f);
            ApplyRulesZoom(1f);
        }
    }

    public class CardInspectionOverlayAnimator : MonoBehaviour
    {
        public void PlayIn(RectTransform detail, CanvasGroup group)
        {
            StartCoroutine(AnimateIn(detail, group));
        }

        private static IEnumerator AnimateIn(RectTransform detail, CanvasGroup group)
        {
            if (detail == null)
            {
                yield break;
            }

            Vector3 targetScale = detail.localScale;
            detail.localScale = targetScale * 0.88f;
            float elapsed = 0f;
            const float duration = 0.14f;
            while (elapsed < duration && detail != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                detail.localScale = Vector3.LerpUnclamped(targetScale * 0.88f, targetScale, eased);
                if (group != null)
                {
                    group.alpha = eased;
                }

                yield return null;
            }

            if (detail != null)
            {
                detail.localScale = targetScale;
            }

            if (group != null)
            {
                group.alpha = 1f;
            }
        }
    }
}
