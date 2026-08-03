using AppreciatorsTcg.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AppreciatorsTcg.UI
{
    public class MatchLaneDropZone : MonoBehaviour, IPointerClickHandler
    {
        public MatchScreenController Controller { get; set; }
        public LaneType Lane { get; set; }

        public void OnPointerClick(PointerEventData eventData)
        {
            GameObject pressed = eventData?.pointerPressRaycast.gameObject;
            if (pressed != null && pressed.GetComponentInParent<CardInspectionTrigger>() != null)
            {
                return;
            }

            Controller?.HandleLaneSurfaceClickFromInput(Lane);
        }
    }
}
