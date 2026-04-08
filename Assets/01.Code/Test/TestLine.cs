using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _01.Code.Test
{
    [ExecuteAlways]
    public class TestLine : MonoBehaviour
    {
        [Min(1)] public int size = 30;
        [Min(0.1f)] public float cellSize = 1f;
        [Min(0.001f)] public float lineWidth = 0.03f;
        [Min(0.5f)] public float minScreenPixelWidth = 2f;
        public Material lineMat;
        public Color gizmoColor = default;

        private void Awake()
        {
            if (IsUnset(gizmoColor))
            {
                gizmoColor = GetWhiteWithAlpha(0.2f);
            }
        }

        private void OnEnable()
        {
            RebuildGrid();
        }

        private void Start()
        {
            RebuildGrid();
        }

        private void LateUpdate()
        {
            RefreshLineWidths();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= RebuildGrid;
            EditorApplication.delayCall += RebuildGrid;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= RebuildGrid;
#endif
            ClearLinesImmediate();
        }

        private void RebuildGrid()
        {
            ClearLinesImmediate();

            float halfCell = cellSize * 0.5f;
            Vector3 lineOrigin = new Vector3(halfCell, halfCell, 0f);
            float totalSize = size * cellSize;
            for (int i = -size; i <= size; i++)
            {
                float offset = i * cellSize;

                Vector3 startV = lineOrigin + new Vector3(offset, -totalSize, 0f);
                Vector3 endV = lineOrigin + new Vector3(offset, totalSize, 0f);
                CreateLine(startV, endV);

                Vector3 startH = lineOrigin + new Vector3(-totalSize, offset, 0f);
                Vector3 endH = lineOrigin + new Vector3(totalSize, offset, 0f);
                CreateLine(startH, endH);
            }

            RefreshLineWidths();
        }

        private void CreateLine(Vector3 start, Vector3 end)
        {
            GameObject lineObj = new GameObject("GridLine");
            lineObj.transform.SetParent(transform, false);
            lineObj.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

            LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.sharedMaterial = GetLineMaterial();
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            float appliedWidth = GetAppliedLineWidth();
            lineRenderer.startWidth = appliedWidth;
            lineRenderer.endWidth = appliedWidth;
            lineRenderer.useWorldSpace = false;
            lineRenderer.sortingOrder = 1000;
            lineRenderer.startColor = gizmoColor;
            lineRenderer.endColor = gizmoColor;
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.alignment = LineAlignment.View;
            lineRenderer.textureScale = Vector2.one;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
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
                if (child == null || child.GetComponent<LineRenderer>() == null)
                {
                    continue;
                }

                DestroyImmediate(child.gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;

            float halfCell = cellSize * 0.5f;
            Vector3 lineOrigin = transform.position + new Vector3(halfCell, halfCell, 0f);
            float totalSize = size * cellSize;
            for (int i = -size; i <= size; i++)
            {
                float offset = i * cellSize;

                Vector3 startV = lineOrigin + new Vector3(offset, -totalSize, 0f);
                Vector3 endV = lineOrigin + new Vector3(offset, totalSize, 0f);
                Gizmos.DrawLine(startV, endV);

                Vector3 startH = lineOrigin + new Vector3(-totalSize, offset, 0f);
                Vector3 endH = lineOrigin + new Vector3(totalSize, offset, 0f);
                Gizmos.DrawLine(startH, endH);
            }
        }

        private void RefreshLineWidths()
        {
            float appliedWidth = GetAppliedLineWidth();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || child.name != "GridLine")
                {
                    continue;
                }

                LineRenderer lineRenderer = child.GetComponent<LineRenderer>();
                if (lineRenderer == null)
                {
                    continue;
                }

                lineRenderer.startWidth = appliedWidth;
                lineRenderer.endWidth = appliedWidth;
            }
        }

        private float GetAppliedLineWidth()
        {
            Camera targetCamera = Camera.main;
            if (targetCamera == null || !targetCamera.orthographic || Screen.height <= 0)
            {
                return lineWidth;
            }

            float worldUnitsPerPixel = (targetCamera.orthographicSize * 2f) / Screen.height;
            float minimumWidthFromScreen = worldUnitsPerPixel * minScreenPixelWidth;
            return Mathf.Max(lineWidth, minimumWidthFromScreen);
        }

        private Color GetWhiteWithAlpha(float alpha)
        {
            Color color = Color.white;
            color.a = alpha;
            return color;
        }

        private bool IsUnset(Color color)
        {
            return Mathf.Approximately(color.r, 0f) &&
                   Mathf.Approximately(color.g, 0f) &&
                   Mathf.Approximately(color.b, 0f) &&
                   Mathf.Approximately(color.a, 0f);
        }
    }
}
