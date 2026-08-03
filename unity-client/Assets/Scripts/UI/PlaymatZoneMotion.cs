using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class PlaymatZoneMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public bool ScaleOnHover { get; set; } = true;
        public Color HighlightColor { get; set; } = Color.white;

        private RectTransform rectTransform;
        private Image image;
        private Vector3 targetScale = Vector3.one;
        private Color baseColor = Color.white;
        private Color targetColor = Color.white;

        public void Configure(bool scaleOnHover, Color highlightColor)
        {
            ScaleOnHover = scaleOnHover;
            HighlightColor = highlightColor;
            rectTransform = rectTransform != null ? rectTransform : GetComponent<RectTransform>();
            image = image != null ? image : GetComponent<Image>();
            baseColor = image != null ? image.color : Color.white;
            targetColor = baseColor;
            targetScale = Vector3.one;
            if (rectTransform != null)
            {
                rectTransform.localScale = Vector3.one;
            }
        }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            image = GetComponent<Image>();
            if (image != null)
            {
                baseColor = image.color;
                targetColor = baseColor;
            }
        }

        private void Update()
        {
            float blend = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            if (ScaleOnHover)
            {
                rectTransform.localScale = Vector3.Lerp(rectTransform.localScale, targetScale, blend);
            }
            else
            {
                rectTransform.localScale = Vector3.one;
            }
            if (image != null)
            {
                image.color = Color.Lerp(image.color, targetColor, blend);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = ScaleOnHover ? Vector3.one * 1.025f : Vector3.one;
            targetColor = HighlightColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = Vector3.one;
            targetColor = baseColor;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            targetScale = ScaleOnHover ? Vector3.one * 0.985f : Vector3.one;
            targetColor = Color.Lerp(baseColor, HighlightColor, 0.58f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            targetScale = ScaleOnHover ? Vector3.one * 1.025f : Vector3.one;
            targetColor = HighlightColor;
        }
    }
}
