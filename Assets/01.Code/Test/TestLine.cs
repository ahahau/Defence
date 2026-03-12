using UnityEngine;

namespace _01.Code.Test
{
    public class TestLine : MonoBehaviour
    {
        [Min(1)] public int size = 30;
        [Min(0.1f)] public float cellSize = 1f;
        [Min(0.001f)] public float lineWidth = 0.03f;
        public Material lineMat;
        public Color gizmoColor = new Color(1f, 1f, 1f, 0.2f);

        private void Start()
        {
            RebuildGrid();
        }

        private void OnDisable()
        {
            ClearLinesImmediate();
        }

        private void RebuildGrid()
        {
            ClearLinesImmediate();

            float totalSize = size * cellSize;
            float halfCell = cellSize * 0.5f;
            float axisLength = totalSize * 2f;

            for (int i = -size; i <= size; i++)
            {
                float offset = i * cellSize - halfCell;

                Vector3 startV = new Vector3(offset, -totalSize - halfCell, 0f);
                Vector3 endV = new Vector3(offset, totalSize - halfCell, 0f);
                CreateLine(startV, endV, axisLength);

                Vector3 startH = new Vector3(-totalSize - halfCell, offset, 0f);
                Vector3 endH = new Vector3(totalSize - halfCell, offset, 0f);
                CreateLine(startH, endH, axisLength);
            }
        }

        private void CreateLine(Vector3 start, Vector3 end, float axisLength)
        {
            GameObject lineObj = new GameObject("GridLine");
            lineObj.transform.SetParent(transform, false);

            LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.sharedMaterial = GetLineMaterial();
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = false;
            lineRenderer.sortingOrder = 1000;
            lineRenderer.textureMode = LineTextureMode.Tile;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.textureScale = new Vector2(Mathf.Max(1f, axisLength / cellSize), 1f);
        }

        private Material GetLineMaterial()
        {
            if (lineMat != null)
            {
                return lineMat;
            }

            Shader fallbackShader = Shader.Find("Sprites/Default");
            if (fallbackShader == null)
            {
                return null;
            }

            lineMat = new Material(fallbackShader)
            {
                name = "TestLine_Fallback"
            };

            return lineMat;
        }

        private void ClearLinesImmediate()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != "GridLine")
                {
                    continue;
                }

                DestroyImmediate(child.gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;

            float totalSize = size * cellSize;
            float halfCell = cellSize * 0.5f;

            for (int i = -size; i <= size; i++)
            {
                float offset = i * cellSize - halfCell;

                Vector3 startV = transform.position + new Vector3(offset, -totalSize - halfCell, 0f);
                Vector3 endV = transform.position + new Vector3(offset, totalSize - halfCell, 0f);
                Gizmos.DrawLine(startV, endV);

                Vector3 startH = transform.position + new Vector3(-totalSize - halfCell, offset, 0f);
                Vector3 endH = transform.position + new Vector3(totalSize - halfCell, offset, 0f);
                Gizmos.DrawLine(startH, endH);
            }
        }
    }
}
