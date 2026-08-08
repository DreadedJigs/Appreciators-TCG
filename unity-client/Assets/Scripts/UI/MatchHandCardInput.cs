using System.Collections;
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
        private RectTransform sourceRect;
        private Coroutine returnRoutine;

        public MatchScreenController Controller { get; set; }
        public int HandIndex { get; set; }
        public CardDefinition Card { get; set; }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // The live rules only retain two cards, so a drag beginning on a card is
            // always a card-play gesture. Previously the first few pixels could be
            // classified as horizontal hand scrolling, which made an otherwise valid
            // drag appear to stop responding (especially on touch screens).
            RecoverInterruptedDrag();

            if (Controller == null || Card == null)
            {
                return;
            }

            if (!Controller.CanStartCardDrag(HandIndex))
            {
                Controller.ExplainBlockedCardDrag(HandIndex);
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
            sourceRect = GetComponent<RectTransform>();
            ghost = UIFactory.CreateMatchHandCardPanel(canvas.transform, Card, null, true, "Choose Build or Action");
            ghost.name = "DraggingCardPreview";
            ghostRect = ghost.GetComponent<RectTransform>();
            ghostRect.anchorMin = new Vector2(0.5f, 0.5f);
            ghostRect.anchorMax = new Vector2(0.5f, 0.5f);
            ghostRect.pivot = new Vector2(0.5f, 0.5f);
            ghostRect.sizeDelta = new Vector2(162f, 248f);
            ghost.transform.SetAsLastSibling();
            DisableRaycasts(ghost);
            MoveGhost(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            MatchLaneDropZone dropZone = FindDropZone(eventData);
            MatchShardDropZone shardDropZone = FindShardDropZone(eventData);
            dragging = false;

            if (shardDropZone != null && shardDropZone.Controller == Controller)
            {
                DestroyGhost();
                Controller.SetBattlefieldDropHighlight(false);
                Controller.DiscardHandCard(HandIndex);
                return;
            }

            if (dropZone != null && dropZone.Controller == Controller)
            {
                DestroyGhost();
                Controller.PlayHandCardFromDrop(HandIndex, dropZone.Lane);
                return;
            }

            // Mobile cards, art, and HUD elements can sit above the transparent
            // surface in the raycast stack. Treat every point inside the shared
            // battlefield as a valid Build drop rather than requiring a precise
            // pixel-perfect hit on the hidden lane zone.
            if (Controller.IsBattlefieldDropPoint(eventData.position, eventData.pressEventCamera))
            {
                DestroyGhost();
                Controller.PlayHandCardFromDrop(HandIndex, LaneType.Community);
                return;
            }

            Controller.CancelDraggingHandCard();
            returnRoutine = StartCoroutine(AnimateGhostBackToHand());
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

        private static MatchShardDropZone FindShardDropZone(PointerEventData eventData)
        {
            if (EventSystem.current == null || eventData == null)
            {
                return null;
            }

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (RaycastResult result in results)
            {
                MatchShardDropZone zone = result.gameObject.GetComponentInParent<MatchShardDropZone>();
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
            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
                returnRoutine = null;
            }

            if (ghost == null)
            {
                return;
            }

            Destroy(ghost);
            ghost = null;
            ghostRect = null;
            canvasRect = null;
            sourceRect = null;
        }

        private IEnumerator AnimateGhostBackToHand()
        {
            if (ghost == null || ghostRect == null || canvasRect == null || sourceRect == null)
            {
                DestroyGhost();
                yield break;
            }

            CanvasGroup group = ghost.GetComponent<CanvasGroup>() ?? ghost.AddComponent<CanvasGroup>();
            Vector2 start = ghostRect.anchoredPosition;
            Vector2 target = canvasRect.InverseTransformPoint(sourceRect.position);
            Vector3 startScale = ghostRect.localScale;
            const float duration = 0.20f;
            float elapsed = 0f;
            while (elapsed < duration && ghostRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                ghostRect.anchoredPosition = Vector2.LerpUnclamped(start, target, eased);
                ghostRect.localScale = Vector3.LerpUnclamped(startScale, Vector3.one * 0.84f, eased);
                group.alpha = 1f - eased * 0.78f;
                yield return null;
            }

            returnRoutine = null;
            DestroyGhost();
        }

        private void OnDisable()
        {
            RecoverInterruptedDrag();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                RecoverInterruptedDrag();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                RecoverInterruptedDrag();
            }
        }

        private void RecoverInterruptedDrag()
        {
            bool wasDragging = dragging;
            dragging = false;
            DestroyGhost();
            if (wasDragging && Controller != null)
            {
                Controller.CancelDraggingHandCard();
            }
        }
    }
}
