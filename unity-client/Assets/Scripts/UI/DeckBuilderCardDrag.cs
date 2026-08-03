using System.Collections.Generic;
using AppreciatorsTcg.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class DeckBuilderCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameObject ghost;
        private RectTransform ghostRect;
        private RectTransform canvasRect;
        private ScrollRect parentScroll;
        private bool dragging;
        private bool forwardingScroll;

        public DeckBuilderScreenController Controller { get; set; }
        public CardDefinition Card { get; set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (ShouldScrollCollection(eventData))
            {
                forwardingScroll = true;
                parentScroll = GetComponentInParent<ScrollRect>();
                parentScroll?.OnInitializePotentialDrag(eventData);
                parentScroll?.OnBeginDrag(eventData);
                return;
            }

            if (Controller == null || Card == null || !Controller.CanAddCard(Card.id))
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return;
            }

            dragging = true;
            canvasRect = canvas.GetComponent<RectTransform>();
            ghost = UIFactory.CreateCardPanel(canvas.transform, Card, null, true, "DROP INTO DECK", true);
            ghost.name = "DeckBuilderDraggingCard";
            ghostRect = ghost.GetComponent<RectTransform>();
            ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.sizeDelta = new Vector2(194f, 220f);
            ghost.transform.SetAsLastSibling();
            foreach (Graphic graphic in ghost.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
            MoveGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (forwardingScroll)
            {
                parentScroll?.OnDrag(eventData);
                return;
            }

            if (dragging)
            {
                MoveGhost(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (forwardingScroll)
            {
                parentScroll?.OnEndDrag(eventData);
                forwardingScroll = false;
                parentScroll = null;
                return;
            }

            if (!dragging)
            {
                return;
            }

            DeckBuilderDropZone dropZone = FindDropZone(eventData);
            DestroyGhost();
            dragging = false;
            if (dropZone != null && dropZone.Controller == Controller)
            {
                Controller.AddCardFromDrop(Card.id);
            }
        }

        private void MoveGhost(PointerEventData eventData)
        {
            if (ghostRect == null || canvasRect == null || eventData == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 point);
            ghostRect.anchoredPosition = point + new Vector2(0f, 28f);
        }

        private static bool ShouldScrollCollection(PointerEventData eventData)
        {
            if (eventData == null)
            {
                return false;
            }

            Vector2 delta = eventData.position - eventData.pressPosition;
            return Mathf.Abs(delta.y) > Mathf.Abs(delta.x) * 1.12f;
        }

        private static DeckBuilderDropZone FindDropZone(PointerEventData eventData)
        {
            if (EventSystem.current == null || eventData == null)
            {
                return null;
            }

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (RaycastResult result in results)
            {
                DeckBuilderDropZone zone = result.gameObject.GetComponentInParent<DeckBuilderDropZone>();
                if (zone != null)
                {
                    return zone;
                }
            }

            return null;
        }

        private void DestroyGhost()
        {
            if (ghost != null)
            {
                Destroy(ghost);
            }
            ghost = null;
            ghostRect = null;
            canvasRect = null;
        }

        private void OnDisable()
        {
            DestroyGhost();
            dragging = false;
            forwardingScroll = false;
            parentScroll = null;
        }
    }
}
