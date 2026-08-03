using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AppreciatorsTcg.UI
{
    [AddComponentMenu("")]
    public sealed class UiGradientEffect : BaseMeshEffect
    {
        [SerializeField] private Color topMultiplier = Color.white;
        [SerializeField] private Color bottomMultiplier = new Color(0.68f, 0.72f, 0.86f, 1f);

        public void Configure(Color top, Color bottom)
        {
            topMultiplier = top;
            bottomMultiplier = bottom;
            if (graphic != null)
            {
                graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper.currentVertCount == 0)
            {
                return;
            }

            List<UIVertex> vertices = new List<UIVertex>();
            vertexHelper.GetUIVertexStream(vertices);
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (UIVertex vertex in vertices)
            {
                minY = Mathf.Min(minY, vertex.position.y);
                maxY = Mathf.Max(maxY, vertex.position.y);
            }

            float height = Mathf.Max(0.001f, maxY - minY);
            for (int index = 0; index < vertices.Count; index++)
            {
                UIVertex vertex = vertices[index];
                float t = Mathf.Clamp01((vertex.position.y - minY) / height);
                Color baseColor = vertex.color;
                vertex.color = baseColor * Color.Lerp(bottomMultiplier, topMultiplier, t);
                vertices[index] = vertex;
            }

            vertexHelper.Clear();
            vertexHelper.AddUIVertexTriangleStream(vertices);
        }
    }
}
