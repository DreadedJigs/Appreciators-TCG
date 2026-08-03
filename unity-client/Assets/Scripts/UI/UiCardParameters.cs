using UnityEngine;

namespace AppreciatorsTcg.UI
{
    [CreateAssetMenu(fileName = "UiCardParameters", menuName = "Appreciators/UI Card Parameters")]
    public class UiCardParameters : ScriptableObject
    {
        private const string ResourcePath = "UI/UiCardParameters";
        private static UiCardParameters cached;

        public float disabledAlpha = 0.5f;
        public float hoverHeight = 1f;
        public float hoverRotation;
        public float hoverScale = 1.3f;
        public float hoverSpeed = 15f;
        public float height = 0.12f;
        public float spacing;
        public float bentAngle = 20f;
        public float rotationSpeed = 20f;
        public float rotationSpeedP2 = 500f;
        public float movementSpeed = 4f;
        public float scaleSpeed = 8f;
        public float startSizeWhenDraw = 0.05f;
        public float discardedSize = 0.5f;

        public static UiCardParameters Load()
        {
            if (cached != null)
            {
                return cached;
            }

            cached = Resources.Load<UiCardParameters>(ResourcePath);
            if (cached == null)
            {
                cached = CreateInstance<UiCardParameters>();
                Debug.LogWarning($"Missing Resources/{ResourcePath}.asset. Using default card motion parameters.");
            }

            return cached;
        }
    }
}
