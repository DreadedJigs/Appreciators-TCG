using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class ScrollContentMinWidth : MonoBehaviour
    {
        public RectTransform Viewport { get; set; }

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void LateUpdate()
        {
            if (Viewport == null)
            {
                return;
            }

            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            float preferredWidth = LayoutUtility.GetPreferredWidth(rectTransform);
            float viewportWidth = Viewport.rect.width;
            rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(preferredWidth, viewportWidth));
        }
    }
}
