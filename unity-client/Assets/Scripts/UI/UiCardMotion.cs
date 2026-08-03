using System.Collections;
using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class UiCardMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler
    {
        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private UiCardParameters parameters;
        private Vector3 baseLocalPosition;
        private float baseRotation;
        private float targetScale = 1f;
        private float targetHeight;
        private float targetRotation;
        private bool selected;
        private bool pointerInside;
        private bool dragging;
        private bool baseCaptured;
        private float hoverScaleOverride = -1f;
        private float selectedScale = 1.10f;
        private float dropOffset;
        private float dropRotation;
        private float dropScale = 1f;
        private float dropAlpha = 1f;
        private Vector2 drawOffset;

        private void Awake()
        {
            EnsureInitialized();
            // Card views are rebuilt as match state changes. Starting every rebuilt
            // view at a reduced scale made unchanged cards visibly blink. Explicit
            // draw/drop animations now own all entrance motion.
            rectTransform.localScale = Vector3.one;
            StartCoroutine(CaptureBaseAfterLayout());
        }

        private IEnumerator CaptureBaseAfterLayout()
        {
            yield return null;
            baseLocalPosition = rectTransform.localPosition;
            baseCaptured = true;
        }

        private void LateUpdate()
        {
            EnsureInitialized();
            float delta = Time.unscaledDeltaTime;
            float scale = Mathf.Lerp(rectTransform.localScale.x, targetScale * dropScale, 1f - Mathf.Exp(-parameters.scaleSpeed * delta));
            rectTransform.localScale = Vector3.one * scale;

            float z = Mathf.LerpAngle(rectTransform.localEulerAngles.z, targetRotation + dropRotation, 1f - Mathf.Exp(-parameters.rotationSpeed * delta));
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, z);

            if (baseCaptured && !dragging)
            {
                Vector3 current = rectTransform.localPosition;
                float targetX = baseLocalPosition.x + drawOffset.x;
                float targetY = baseLocalPosition.y + targetHeight * 18f + dropOffset + drawOffset.y;
                current.x = Mathf.Lerp(current.x, targetX, 1f - Mathf.Exp(-parameters.movementSpeed * delta));
                current.y = Mathf.Lerp(current.y, targetY, 1f - Mathf.Exp(-parameters.movementSpeed * delta));
                rectTransform.localPosition = current;
            }

            Selectable selectable = GetComponent<Selectable>();
            canvasGroup.alpha = (selectable == null || selectable.interactable ? 1f : parameters.disabledAlpha) * dropAlpha;
        }

        public void ConfigureHandPosition(int index, int count, bool opponent)
        {
            EnsureInitialized();
            float center = (count - 1) * 0.5f;
            float normalized = count <= 1 ? 0f : (index - center) / Mathf.Max(1f, center);
            baseRotation = -normalized * parameters.bentAngle * 0.5f;
            if (opponent)
            {
                baseRotation *= -1f;
            }

            RefreshTargets();
        }

        public void SetSelected(bool value)
        {
            EnsureInitialized();
            selected = value;
            RefreshTargets();
        }

        public void ConfigureInteractionScale(float hoverScale, float activeScale)
        {
            hoverScaleOverride = Mathf.Max(1f, hoverScale);
            selectedScale = Mathf.Max(1f, activeScale);
            RefreshTargets();
        }

        public void ConfigureBoardDrop(bool opponent)
        {
            EnsureInitialized();
            dropOffset = opponent ? 150f : -150f;
            dropRotation = opponent ? -9f : 9f;
            dropScale = 0.90f;
            dropAlpha = 1f;
            StartCoroutine(PlayBoardDrop());
        }

        public void ConfigureDrawFromDeck(RectTransform deckSource, bool opponent)
        {
            if (deckSource == null)
            {
                return;
            }

            EnsureInitialized();
            StartCoroutine(PlayDrawFromDeck(deckSource, opponent));
        }

        private IEnumerator PlayDrawFromDeck(RectTransform deckSource, bool opponent)
        {
            yield return null;
            yield return null;
            baseLocalPosition = rectTransform.localPosition;
            baseCaptured = true;
            Vector3 sourceWorld = deckSource.TransformPoint(deckSource.rect.center);
            Vector3 sourceLocal = rectTransform.parent.InverseTransformPoint(sourceWorld);
            Vector2 startOffset = new Vector2(sourceLocal.x - baseLocalPosition.x, sourceLocal.y - baseLocalPosition.y);
            drawOffset = startOffset;
            dropRotation = opponent ? -13f : 13f;
            dropScale = 0.90f;
            dropAlpha = 1f;

            float elapsed = 0f;
            float duration = ThemeService.ReducedMotion ? 0.16f : 0.78f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                drawOffset = Vector2.Lerp(startOffset, Vector2.zero, eased);
                drawOffset.y += Mathf.Sin(t * Mathf.PI) * (opponent ? 24f : 42f);
                dropRotation = Mathf.Lerp(opponent ? -13f : 13f, 0f, eased);
                dropScale = Mathf.Lerp(0.90f, 1f, eased);
                dropAlpha = 1f;
                yield return null;
            }

            drawOffset = Vector2.zero;
            dropRotation = 0f;
            dropScale = 1f;
            dropAlpha = 1f;
            baseLocalPosition = rectTransform.localPosition;
            RefreshTargets();
        }

        private IEnumerator PlayBoardDrop()
        {
            yield return null;
            baseLocalPosition = rectTransform.localPosition;
            baseCaptured = true;
            float startOffset = dropOffset;
            float startRotation = dropRotation;
            float elapsed = 0f;
            float duration = ThemeService.ReducedMotion ? 0.12f : 0.62f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                dropOffset = Mathf.Lerp(startOffset, 0f, eased) + Mathf.Sin(t * Mathf.PI) * (1f - t) * 18f;
                dropRotation = Mathf.Lerp(startRotation, 0f, eased);
                dropScale = Mathf.Lerp(0.90f, 1f, eased);
                dropAlpha = 1f;
                yield return null;
            }

            dropOffset = 0f;
            dropRotation = 0f;
            dropScale = 1f;
            dropAlpha = 1f;
            baseLocalPosition = rectTransform.localPosition;
            RefreshTargets();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnsureInitialized();
            pointerInside = true;
            RefreshTargets();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            EnsureInitialized();
            pointerInside = false;
            RefreshTargets();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            EnsureInitialized();
            dragging = true;
            targetScale = 1.06f;
            targetRotation = 0f;
            targetHeight = parameters.hoverHeight;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EnsureInitialized();
            dragging = false;
            baseLocalPosition = rectTransform.localPosition;
            RefreshTargets();
        }

        private void RefreshTargets()
        {
            if (dragging)
            {
                return;
            }

            if (pointerInside)
            {
                targetScale = hoverScaleOverride > 0f ? hoverScaleOverride : parameters.hoverScale;
                targetHeight = parameters.hoverHeight;
                targetRotation = parameters.hoverRotation;
                return;
            }

            targetScale = selected ? selectedScale : 1f;
            targetHeight = selected ? parameters.height : 0f;
            targetRotation = baseRotation;
        }

        private void EnsureInitialized()
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            }

            if (parameters == null)
            {
                parameters = UiCardParameters.Load();
            }
        }
    }
}
