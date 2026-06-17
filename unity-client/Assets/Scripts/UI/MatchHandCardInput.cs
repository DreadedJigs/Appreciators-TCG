using System.Collections.Generic;
using AppreciatorsTcg.Cards;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class MatchHandCardInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameObject ghost;
        private RectTransform ghostRect;
        private RectTransform canvasRect;
        private bool dragging;
        private bool forwardingScroll;
        private ScrollRect parentScrollRect;

        public MatchScreenController Controller { get; set; }
        public int HandIndex { get; set; }
        public CardDefinition Card { get; set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ShouldScrollHand(eventData))
            {
                forwardingScroll = true;
                parentScrollRect = GetComponentInParent<ScrollRect>();
                if (parentScrollRect != null)
                {
                    parentScrollRect.OnInitializePotentialDrag(eventData);
                    parentScrollRect.OnBeginDrag(eventData);
                }

                return;
            }

            if (Controller == null || Card == null || !Controller.CanStartCardDrag(HandIndex))
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            dragging = true;
            CardInspectionOverlay.Hide();
            Controller.MarkDraggingHandCard(HandIndex);
            canvasRect = canvas.GetComponent<RectTransform>();
            ghost = UIFactory.CreateMatchHandCardPanel(canvas.transform, Card, null, true, "Drop on lane");
            ghost.name = "DraggingCardPreview";
            ghostRect = ghost.GetComponent<RectTransform>();
            ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.sizeDelta = new Vector2(160f, 204f);
            ghost.transform.SetAsLastSibling();
            DisableRaycasts(ghost);
            MoveGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (forwardingScroll)
            {
                if (parentScrollRect != null)
                {
                    parentScrollRect.OnDrag(eventData);
                }

                return;
            }

            if (!dragging)
            {
                return;
            }

            MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (forwardingScroll)
            {
                if (parentScrollRect != null)
                {
                    parentScrollRect.OnEndDrag(eventData);
                }

                forwardingScroll = false;
                parentScrollRect = null;
                return;
            }

            if (!dragging)
            {
                return;
            }

            MatchLaneDropZone dropZone = FindDropZone(eventData);
            DestroyGhost();
            dragging = false;

            if (dropZone != null && dropZone.Controller == Controller)
            {
                Controller.PlayHandCardFromDrop(HandIndex, dropZone.Lane);
                return;
            }

            Controller.CancelDraggingHandCard();
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (ghostRect == null || canvasRect == null || eventData == null)
            {
                return;
            }

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out localPoint);
            ghostRect.anchoredPosition = localPoint + new Vector2(0f, 34f);
        }

        private static bool ShouldScrollHand(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            Vector2 delta = eventData.position - eventData.pressPosition;
            return Mathf.Abs(delta.x) > Mathf.Abs(delta.y) * 1.12f;
        }

        private static MatchLaneDropZone FindDropZone(PointerEventData eventData)
        {
            if (EventSystem.current == null || eventData == null)
            {
                return null;
            }

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (RaycastResult result in results)
            {
                MatchLaneDropZone zone = result.gameObject.GetComponentInParent<MatchLaneDropZone>();
                if (zone != null)
                {
                    return zone;
                }
            }

            return null;
        }

        private static void DisableRaycasts(GameObject root)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        private void DestroyGhost()
        {
            if (ghost == null)
            {
                return;
            }

            Destroy(ghost);
            ghost = null;
            ghostRect = null;
            canvasRect = null;
        }

        private void OnDisable()
        {
            DestroyGhost();
            dragging = false;
            forwardingScroll = false;
            parentScrollRect = null;
        }
    }
}
