using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    /// <summary>
    /// Keeps text and controls legible when the WebGL canvas is embedded in a
    /// narrow browser or displayed on a phone. Anchored board geometry remains
    /// proportional; only pixel-authored typography and layout elements receive
    /// a more appropriate reference resolution.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class ResponsiveCanvasScaler : MonoBehaviour
    {
        private CanvasScaler scaler;
        private Vector2Int lastScreenSize;

        public static bool IsPhoneLayout
        {
            get
            {
                bool handheld = Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
                return handheld || Screen.width < 1000 || Screen.height < 560;
            }
        }

        public static bool IsCompactLayout => IsPhoneLayout || Screen.width < 1500 || Screen.height < 850;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void Update()
        {
            Vector2Int current = new Vector2Int(Screen.width, Screen.height);
            if (current != lastScreenSize)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (scaler == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            bool portrait = Screen.height > Screen.width;
            if (IsPhoneLayout)
            {
                scaler.referenceResolution = portrait ? new Vector2(540f, 960f) : new Vector2(960f, 540f);
            }
            else if (IsCompactLayout)
            {
                scaler.referenceResolution = portrait ? new Vector2(720f, 1280f) : new Vector2(1280f, 720f);
            }
            else
            {
                scaler.referenceResolution = portrait ? new Vector2(900f, 1600f) : new Vector2(1600f, 900f);
            }

            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
