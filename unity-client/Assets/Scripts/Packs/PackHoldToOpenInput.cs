using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AppreciatorsTcg.Packs
{
    public class PackHoldToOpenInput : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private float holdDuration = 4f;
        private Action<float> progressChanged;
        private Action completed;
        private Action tapped;
        private Coroutine holdRoutine;
        private bool pointerHeld;
        private bool interactable = true;
        private Graphic hitGraphic;
        private bool completedThisPress;

        public void Configure(float duration, Action<float> onProgressChanged, Action onCompleted, Action onTapped = null)
        {
            holdDuration = Mathf.Max(0.25f, duration);
            progressChanged = onProgressChanged;
            completed = onCompleted;
            tapped = onTapped;
            hitGraphic = GetComponent<Graphic>();
            SetInteractable(true);
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (hitGraphic == null)
            {
                hitGraphic = GetComponent<Graphic>();
            }

            if (hitGraphic != null)
            {
                hitGraphic.raycastTarget = value;
            }

            if (!value)
            {
                CancelHold();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!interactable || (eventData != null && eventData.button != PointerEventData.InputButton.Left))
            {
                return;
            }

            CancelHold();
            completedThisPress = false;
            pointerHeld = true;
            holdRoutine = StartCoroutine(HoldRoutine());
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            CancelHold();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (interactable && !completedThisPress)
            {
                tapped?.Invoke();
            }
        }

        private IEnumerator HoldRoutine()
        {
            float elapsed = 0f;
            progressChanged?.Invoke(0f);
            while (pointerHeld && elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                progressChanged?.Invoke(Mathf.Clamp01(elapsed / holdDuration));
                if (elapsed >= holdDuration)
                {
                    holdRoutine = null;
                    pointerHeld = false;
                    completedThisPress = true;
                    progressChanged?.Invoke(1f);
                    completed?.Invoke();
                    yield break;
                }

                yield return null;
            }

            holdRoutine = null;
        }

        private void CancelHold()
        {
            pointerHeld = false;
            if (holdRoutine != null)
            {
                StopCoroutine(holdRoutine);
                holdRoutine = null;
            }

            progressChanged?.Invoke(0f);
        }

        private void OnDisable()
        {
            CancelHold();
        }
    }
}
