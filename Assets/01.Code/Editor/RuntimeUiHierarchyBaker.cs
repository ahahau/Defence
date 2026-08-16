#if UNITY_EDITOR
using _01.Code.UI;
using _01.Code.MapCreateSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.Editor
{
    /// <summary>Places persistent HUD prefabs in SampleScene so the edit-time hierarchy matches Play mode.</summary>
    public static class RuntimeUiHierarchyBaker
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";

        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<DungeonGraphController>(true))
                    controller.EditorBakeInitialScenePreview();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Baked the Play-start hierarchy into SampleScene.");
        }

        private static Canvas FindRootCanvas(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    // GetComponentInParent includes the Canvas itself, so inspect its
                    // parent hierarchy when selecting the primary scene canvas.
                    if (canvas != null
                        && (canvas.transform.parent == null
                            || canvas.transform.parent.GetComponentInParent<Canvas>() == null))
                        return canvas;
                }
            }

            return null;
        }

        private static void EnsurePanel<T>(Canvas canvas, string prefabPath) where T : Component
        {
            if (canvas.GetComponentInChildren<T>(true) != null)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Persistent UI prefab missing: {prefabPath}");
                return;
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
            if (instance == null)
                return;

            instance.name = prefab.name;
            instance.SetActive(false);
        }
    }
}
#endif
