using AppreciatorsTcg.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    [AddComponentMenu("")]
    public sealed class UiButtonSfx : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Button button = GetComponent<Button>();
            if (button != null && button.IsInteractable()) UiAudioService.PlayButton();
        }
    }
}
