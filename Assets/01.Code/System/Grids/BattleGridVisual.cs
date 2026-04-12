using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.System.Grids
{
    [ExecuteAlways]
    public class BattleGridVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField] private float zOffset = 0.02f;
        [SerializeField] private int sortingOrder = -200;

        private readonly List<LineRenderer> _lineRenderers = new List<LineRenderer>();

        public void Refresh(GridManager gridManager)
        {
            if (gridManager == null || gridManager.Tilemap == null)
            {
                Clear();
                return;
            }

            float cellStep = gridManager.CellStep;
            float halfStep = cellStep * 0.5f;
            int lineIndex = 0;

            foreach (Vector2Int cell in gridManager.ActiveCells)
            {
                Vector3 origin = gridManager.CellToWorld(cell) + new Vector3(-halfStep, -halfStep, zOffset);
                Vector3 bottomLeft = origin;
                Vector3 bottomRight = origin + new Vector3(cellStep, 0f, 0f);
                Vector3 topLeft = origin + new Vector3(0f, cellStep, 0f);
                Vector3 topRight = origin + new Vector3(cellStep, cellStep, 0f);

                lineIndex = SetLine(lineIndex, bottomLeft, topLeft);
                lineIndex = SetLine(lineIndex, topLeft, topRight);

                if (!gridManager.Tilemap.ContainsCell(cell + Vector2Int.right))
                {
                    lineIndex = SetLine(lineIndex, bottomRight, topRight);
                }

                if (!gridManager.Tilemap.ContainsCell(cell + Vector2Int.down))
                {
                    lineIndex = SetLine(lineIndex, bottomLeft, bottomRight);
                }
            }

            for (int i = lineIndex; i < _lineRenderers.Count; i++)
            {
                if (_lineRenderers[i] != null)
                {
                    _lineRenderers[i].gameObject.SetActive(false);
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _lineRenderers.Count; i++)
            {
                if (_lineRenderers[i] != null)
                {
                    _lineRenderers[i].gameObject.SetActive(false);
                }
            }
        }

        private int SetLine(int lineIndex, Vector3 start, Vector3 end)
        {
            LineRenderer lineRenderer = GetOrCreateLineRenderer(lineIndex);
            lineRenderer.gameObject.SetActive(true);
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            return lineIndex + 1;
        }

        private LineRenderer GetOrCreateLineRenderer(int lineIndex)
        {
            while (_lineRenderers.Count <= lineIndex)
            {
                GameObject lineObject = new GameObject($"GridLine_{_lineRenderers.Count}", typeof(LineRenderer));
                lineObject.transform.SetParent(transform, false);
                LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
                lineRenderer.useWorldSpace = true;
                lineRenderer.alignment = LineAlignment.TransformZ;
                lineRenderer.numCapVertices = 0;
                lineRenderer.numCornerVertices = 0;
                lineRenderer.textureMode = LineTextureMode.Stretch;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
                lineRenderer.sortingOrder = sortingOrder;
                if (lineMaterial != null)
                {
                    lineRenderer.sharedMaterial = lineMaterial;
                }

                _lineRenderers.Add(lineRenderer);
            }

            return _lineRenderers[lineIndex];
        }
    }
}
