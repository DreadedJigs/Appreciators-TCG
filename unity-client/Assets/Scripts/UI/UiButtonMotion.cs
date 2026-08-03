using UnityEngine;
using UnityEngine.EventSystems;

namespace AppreciatorsTcg.UI
{
    [AddComponentMenu("")]
    public sealed class UiButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rectTransform;
        private Vector3 targetScale = Vector3.one;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnDisable()
        {
            targetScale = Vector3.one;
            if (rectTransform != null) rectTransform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (rectTransform == null) return;
            float blend = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
            rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, blend);
        }

        public void OnPointerEnter(PointerEventData eventData) => targetScale = Vector3.one * 1.018f;
        public void OnPointerExit(PointerEventData eventData) => targetScale = Vector3.one;
        public void OnPointerDown(PointerEventData eventData) => targetScale = Vector3.one * 0.975f;
        public void OnPointerUp(PointerEventData eventData) => targetScale = Vector3.one * 1.018f;
    }
}
