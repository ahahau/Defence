using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class TownTreeLineRenderer : MaskableGraphic
    {
        [SerializeField] private float thickness = 6f;
        private readonly List<Vector2> _segments = new();

        public void SetSegments(List<Vector2> segments)
        {
            _segments.Clear();
            if (segments != null)
            {
                _segments.AddRange(segments);
            }

            SetVerticesDirty();
            SetMaterialDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_segments.Count < 2)
            {
                return;
            }

            for (int i = 0; i + 1 < _segments.Count; i += 2)
            {
                AddLineSegment(vh, _segments[i], _segments[i + 1]);
            }
        }

        private void AddLineSegment(VertexHelper vh, Vector2 start, Vector2 end)
        {
            Vector2 direction = (end - start).normalized;
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);
            int vertexIndex = vh.currentVertCount;
            Color32 lineColor = color;

            vh.AddVert(start - normal, lineColor, Vector2.zero);
            vh.AddVert(start + normal, lineColor, Vector2.zero);
            vh.AddVert(end + normal, lineColor, Vector2.zero);
            vh.AddVert(end - normal, lineColor, Vector2.zero);

            vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
            vh.AddTriangle(vertexIndex, vertexIndex + 2, vertexIndex + 3);
        }
    }
}
