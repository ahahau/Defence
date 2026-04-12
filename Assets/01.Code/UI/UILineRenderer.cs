using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class UILineRenderer : MaskableGraphic
    {
        [SerializeField] private float thickness = 6f;
        private readonly List<Vector2> _points = new();

        public void SetPoints(IReadOnlyList<Vector2> points)
        {
            _points.Clear();
            if (points != null)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    _points.Add(points[i]);
                }
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count < 2)
            {
                return;
            }

            for (int i = 0; i + 1 < _points.Count; i += 2)
            {
                AddLineSegment(vh, _points[i], _points[i + 1], color, thickness);
            }
        }

        private void AddLineSegment(VertexHelper vh, Vector2 start, Vector2 end, Color32 lineColor, float lineThickness)
        {
            Vector2 direction = (end - start).normalized;
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x) * (lineThickness * 0.5f);
            int vertexIndex = vh.currentVertCount;

            vh.AddVert(start - normal, lineColor, Vector2.zero);
            vh.AddVert(start + normal, lineColor, Vector2.zero);
            vh.AddVert(end + normal, lineColor, Vector2.zero);
            vh.AddVert(end - normal, lineColor, Vector2.zero);

            vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
            vh.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
        }
    }
}
