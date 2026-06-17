using System.Collections;
using AppreciatorsTcg.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class CardInspectionTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler
    {
        private const float MobileHoldSeconds = 0.34f;
        private Coroutine holdRoutine;
        private bool longPressVisible;
        private Vector2 holdPosition;

        public CardDefinition Card { get; set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!ShouldShowHover(eventData))
            {
                return;
            }

            CardInspectionOverlay.Show(Card);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            CancelLongPress();
            CardInspectionOverlay.Hide();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            holdPosition = eventData == null ? Vector2.zero : eventData.position;
            if (ShouldUseLongPress(eventData))
            {
                CancelLongPress();
                holdRoutine = StartCoroutine(ShowMobilePreviewAfterHold());
                return;
            }

            if (!ShouldShowHover(eventData))
            {
                CardInspectionOverlay.Hide();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            CancelLongPress();
            if (longPressVisible)
            {
                longPressVisible = false;
                CardInspectionOverlay.Hide();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            CancelLongPress();
            longPressVisible = false;
            CardInspectionOverlay.Hide();
        }

        private IEnumerator ShowMobilePreviewAfterHold()
        {
            yield return new WaitForSeconds(MobileHoldSeconds);
            holdRoutine = null;
            longPressVisible = true;
            CardInspectionOverlay.Show(Card, holdPosition, true);
        }

        private void CancelLongPress()
        {
            if (holdRoutine == null)
            {
                return;
            }

            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        private static bool ShouldShowHover(PointerEventData eventData)
        {
            if (Application.isMobilePlatform)
            {
                return false;
            }

            return eventData == null || eventData.pointerId < 0;
        }

        private static bool ShouldUseLongPress(PointerEventData eventData)
        {
            return Application.isMobilePlatform || (eventData != null && eventData.pointerId >= 0);
        }

        private void OnDisable()
        {
            CancelLongPress();
            if (longPressVisible)
            {
                longPressVisible = false;
                CardInspectionOverlay.Hide();
            }
        }
    }

    public static class CardInspectionOverlay
    {
        private static GameObject overlay;

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

            overlay = new GameObject("CardInspectionOverlay", typeof(RectTransform), typeof(Image));
            overlay.transform.SetParent(canvas.transform, false);
            Image dim = overlay.GetComponent<Image>();
            dim.color = mobileTouch ? new Color(0f, 0f, 0f, 0f) : new Color(0f, 0f, 0f, 0.34f);
            dim.raycastTarget = false;
            UIFactory.Stretch(overlay.GetComponent<RectTransform>());

            GameObject detail = UIFactory.CreateVerticalStack(overlay.transform, "CardPreview", UIFactory.Panel, 8, 14);
            RectTransform detailRect = detail.GetComponent<RectTransform>();
            if (mobileTouch)
            {
                PositionMobilePreview(canvas.GetComponent<RectTransform>(), detailRect, screenPosition);
            }
            else
            {
                detailRect.anchorMin = new Vector2(0.68f, 0.10f);
                detailRect.anchorMax = new Vector2(0.98f, 0.92f);
                detailRect.offsetMin = Vector2.zero;
                detailRect.offsetMax = Vector2.zero;
            }

            UIFactory.CreateText(detail.transform, card.name, mobileTouch ? 26 : 30, TextAnchor.MiddleLeft, UIFactory.NeonCyan, FontStyle.Bold);
            CreateLargeArt(detail.transform, card, mobileTouch);
            UIFactory.CreateText(detail.transform, $"Cost {card.cost}    Power {card.power}    Appreciation {card.appreciation}", mobileTouch ? 19 : 22, TextAnchor.MiddleLeft, UIFactory.Accent, FontStyle.Bold);
            UIFactory.CreateText(detail.transform, $"{card.rarity} | {card.type} | {card.traitGroup}", mobileTouch ? 18 : 20, TextAnchor.MiddleLeft, UIFactory.TextColor);
            string affinity = string.IsNullOrWhiteSpace(card.laneAffinity) ? "Any lane" : card.laneAffinity;
            UIFactory.CreateText(detail.transform, affinity, mobileTouch ? 17 : 19, TextAnchor.MiddleLeft, UIFactory.MutedTextColor);
            UIFactory.CreateText(detail.transform, card.effectText, mobileTouch ? 18 : 21, TextAnchor.UpperLeft, UIFactory.TextColor);
            DisableRaycasts(overlay);
        }

        public static void Hide()
        {
            if (overlay == null)
            {
                return;
            }

            Object.Destroy(overlay);
            overlay = null;
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

        private static void CreateLargeArt(Transform parent, CardDefinition card, bool mobileTouch)
        {
            GameObject art = UIFactory.CreatePanel(parent, "CardPreviewArt", Color.Lerp(UIFactory.ColorForType(card.type), Color.black, 0.16f));
            LayoutElement layout = art.AddComponent<LayoutElement>();
            layout.minHeight = mobileTouch ? 210 : 185;
            layout.preferredHeight = mobileTouch ? 245 : 210;

            Image image = art.GetComponent<Image>();
            Sprite sprite = CardArtResolver.LoadSprite(card);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                return;
            }

            Text fallback = UIFactory.CreateText(art.transform, string.IsNullOrWhiteSpace(card.type) ? "CARD" : card.type, 34, TextAnchor.MiddleCenter, UIFactory.Accent, FontStyle.Bold);
            UIFactory.Stretch(fallback.rectTransform);
        }

        private static void DisableRaycasts(GameObject root)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }
    }
}
