#if UNITY_EDITOR
using _01.Code.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.Editor
{
    /// <summary>Replaces the legacy scene-only unit panel with the authored prefab instance.</summary>
    public static class UnitStatusPanelSceneInstaller
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string PrefabPath = "Assets/04.Prefab/UI/Panels/UnitStatusPanel.prefab";

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var oldView = FirstComponent<UnitStatusPanelView>(scene);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (oldView == null || prefab == null)
                throw new System.InvalidOperationException("Unit status scene view or prefab is missing.");

            var parent = oldView.transform.parent;
            var siblingIndex = oldView.transform.GetSiblingIndex();
            var oldSerialized = new SerializedObject(oldView);
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                throw new System.InvalidOperationException("Could not instantiate UnitStatusPanel prefab.");

            instance.name = "UnitStatusPanel";
            instance.transform.SetSiblingIndex(siblingIndex);
            var newView = instance.GetComponent<UnitStatusPanelView>();
            var newSerialized = new SerializedObject(newView);
            CopyReference(oldSerialized, newSerialized, "nodeEventChannel");
            CopyReference(oldSerialized, newSerialized, "costEventChannel");
            CopyReference(oldSerialized, newSerialized, "dayManager");
            newSerialized.FindProperty("panelCanvas").objectReferenceValue = parent != null
                ? parent.GetComponentInParent<Canvas>() : null;
            newSerialized.ApplyModifiedPropertiesWithoutUndo();

            Object.DestroyImmediate(oldView.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("UnitStatusPanel scene instance now uses the authored prefab.");
        }

        private static void CopyReference(SerializedObject source, SerializedObject destination, string property)
        {
            destination.FindProperty(property).objectReferenceValue = source.FindProperty(property).objectReferenceValue;
        }

        private static T FirstComponent<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }
    }
}
#endif
