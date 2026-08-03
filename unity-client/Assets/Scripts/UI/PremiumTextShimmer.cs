using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    /// <summary>
    /// A lightweight holographic finish for premium UI callouts. It echoes the
    /// card foil palette without adding a second visual system to the board.
    /// </summary>
    public sealed class PremiumTextShimmer : MonoBehaviour
    {
        private Text label;
        private Outline outline;
        private Shadow shadow;
        private float phase;

        public void Configure(Text target)
        {
            label = target;
            phase = Mathf.Abs(GetInstanceID() % 211) / 37f;
            outline = GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            shadow = GetComponent<Shadow>() ?? gameObject.AddComponent<Shadow>();
            outline.effectDistance = new Vector2(2.5f, -2.5f);
            shadow.effectDistance = new Vector2(3f, -4f);
            shadow.effectColor = new Color(0.015f, 0.01f, 0.08f, 0.82f);
        }

        private void Update()
        {
            if (label == null) return;

            float shimmer = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.7f + phase);
            Color gold = new Color(1f, 0.78f, 0f, 1f);
            Color cyan = new Color(0f, 0.745f, 0.882f, 1f);
            Color cream = new Color(1f, 0.98f, 0.82f, 1f);
            label.color = Color.Lerp(Color.Lerp(gold, cyan, shimmer), cream, 0.28f);
            if (outline != null)
            {
                Color edge = Color.Lerp(new Color(0.06f, 0.04f, 0.27f, 1f), gold, shimmer * 0.35f);
                outline.effectColor = edge;
            }

            transform.localScale = Vector3.one * (1f + shimmer * 0.035f);
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
        }
    }
}
