#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.Editor
{
    /// <summary>Reports visible direct Canvas children and overlapping screen rectangles for UI layout QA.</summary>
    public static class SceneUiLayoutAudit
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";

        public static void Report()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvases = new List<Canvas>();
            foreach (var root in scene.GetRootGameObjects())
            {
                canvases.AddRange(root.GetComponentsInChildren<Canvas>(true));
            }
            if (canvases.Count == 0) throw new System.InvalidOperationException("SampleScene has no Canvas.");
            var panels = new List<(string Name, Rect Bounds)>();
            foreach (var canvas in canvases)
            {
                Debug.Log($"UI_CANVAS {canvas.name} mode={canvas.renderMode}");
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                foreach (Transform childTransform in canvas.transform)
                {
                    if (childTransform is not RectTransform child) continue;
                    if (!child.gameObject.activeInHierarchy || child.GetComponentInChildren<CanvasRenderer>(true) == null)
                        continue;
                    var corners = new Vector3[4]; child.GetWorldCorners(corners);
                    var bounds = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                    panels.Add(($"{canvas.name}/{child.name}", bounds));
                    Debug.Log($"UI_RECT {canvas.name}/{child.name} x={bounds.x:0} y={bounds.y:0} w={bounds.width:0} h={bounds.height:0}");
                }
            }

            for (var left = 0; left < panels.Count; left++)
            for (var right = left + 1; right < panels.Count; right++)
            {
                var overlap = Intersection(panels[left].Bounds, panels[right].Bounds);
                if (overlap.width >= 10f && overlap.height >= 10f)
                    Debug.LogWarning($"UI_OVERLAP {panels[left].Name} <-> {panels[right].Name} w={overlap.width:0} h={overlap.height:0}");
            }
        }

        private static Rect Intersection(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin); var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax); var yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : Rect.zero;
        }
    }
}
#endif
