using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    public class ScrollWheelRelay : MonoBehaviour, IScrollHandler
    {
        public ScrollRect Target { get; set; }
        public float Sensitivity { get; set; } = 0.08f;

        public void OnScroll(PointerEventData eventData)
        {
            if (Target == null)
            {
                Target = GetComponent<ScrollRect>();
            }

            if (Target == null)
            {
                return;
            }

            float delta = eventData.scrollDelta.y * Sensitivity;
            if (Target.horizontal && !Target.vertical)
            {
                Target.horizontalNormalizedPosition = Mathf.Clamp01(Target.horizontalNormalizedPosition - delta);
                return;
            }

            if (Target.vertical)
            {
                Target.verticalNormalizedPosition = Mathf.Clamp01(Target.verticalNormalizedPosition + delta);
            }
        }
    }
}
