#if UNITY_EDITOR
using _01.Code.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    /// <summary>Converts the legacy scene-only wave start control into an authored prefab instance.</summary>
    public static class WaveStartButtonPrefabInstaller
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string PrefabPath = "Assets/04.Prefab/UI/WaveStartButton.prefab";

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var oldView = FirstComponent<WaveView>(scene);
            if (oldView == null)
                throw new System.InvalidOperationException("WaveView is missing from SampleScene.");

            var oldObject = oldView.gameObject;
            var parent = oldObject.transform.parent;
            var siblingIndex = oldObject.transform.GetSiblingIndex();
            var source = new SerializedObject(oldView);

            PrefabUtility.SaveAsPrefabAsset(oldObject, PrefabPath);
            ConfigurePrefab();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                throw new System.InvalidOperationException("Could not instantiate WaveStartButton prefab.");

            instance.name = "WaveStartButton";
            instance.transform.SetSiblingIndex(siblingIndex);
            var destination = new SerializedObject(instance.GetComponent<WaveView>());
            CopyReference(source, destination, "waveEventChannel");
            CopyReference(source, destination, "dayManager");
            CopyReference(source, destination, "waveManager");
            CopyReference(source, destination, "nodeEventChannel");
            CopyReference(source, destination, "gameStateEventChannel");
            CopyReference(source, destination, "runtimeHudView");
            CopyReference(source, destination, "runtimeHudPrefab");
            destination.FindProperty("handleStartButtonClick").boolValue = true;
            destination.ApplyModifiedPropertiesWithoutUndo();

            var button = instance.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();

            Object.DestroyImmediate(oldObject);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wave start button now uses the WaveStartButton prefab.");
        }

        private static void ConfigurePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var waveView = root.GetComponent<WaveView>();
            var waveViewSerialized = new SerializedObject(waveView);
            waveViewSerialized.FindProperty("handleStartButtonClick").boolValue = true;
            waveViewSerialized.ApplyModifiedPropertiesWithoutUndo();

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 24f);
            rect.sizeDelta = new Vector2(260f, 88f);

            var image = root.GetComponent<Image>();
            image.color = new Color(0.34f, 0.055f, 0.035f, 0.98f);
            root.GetComponent<Button>().onClick.RemoveAllListeners();
            var outline = root.GetComponent<Outline>();
            if (outline == null)
                outline = root.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.58f, 0.16f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var label = root.GetComponentInChildren<TMP_Text>(true);
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 0.9f, 0.66f, 1f);
            label.fontStyle |= FontStyles.Bold;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 23f;
            label.enableWordWrapping = true;
            label.text = "습격 개시\nDAY 1 · 모험가 0명";

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
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
                if (component != null)
                    return component;
            }

            return null;
        }
    }
}
#endif
