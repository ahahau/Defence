using System.IO;
using _01.Code.Core;
using _01.Code.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    public static class EnemyStatusPanelPrefabInstaller
    {
        private const string PrefabPath = "Assets/04.Prefab/UI/EnemyStatusPanel.prefab";
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string NodeEventChannelPath = "Assets/03.SO/Event/NodeGameEventChannel.asset";

        public static void Reinstall()
        {
            Directory.CreateDirectory("Assets/04.Prefab/UI");

            var prefabRoot = BuildPrefabHierarchy();
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            Object.DestroyImmediate(prefabRoot);

            if (prefab == null)
                throw new UnityException($"Failed to save {PrefabPath}");

            InstallIntoSampleScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static GameObject BuildPrefabHierarchy()
        {
            var root = CreateRectObject("EnemyStatus", null, new Vector2(300f, 220f));
            var view = root.AddComponent<EnemyStatusPanelView>();
            var nodeEventChannel = AssetDatabase.LoadAssetAtPath<GameEventChannelSO>(NodeEventChannelPath);

            var panel = CreateRectObject("Panel", root.transform, new Vector2(300f, 220f));
            panel.SetActive(false);
            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = CreateRectObject("Header", panel.transform, new Vector2(272f, 28f));
            var headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 8f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            var title = CreateText("TitleText", header.transform, "Enemy", 18, FontStyles.Bold, new Color(1f, 0.92f, 0.72f));
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var closeButton = CreateButton("CloseButton", header.transform);

            var state = CreateText("StateText", panel.transform, "이동 중", 13, FontStyles.Bold, new Color(0.76f, 0.92f, 1f));
            var hp = CreateText("HpText", panel.transform, "HP -", 13, FontStyles.Normal, Color.white);
            var attack = CreateText("AttackText", panel.transform, "ATK -  SPD -", 13, FontStyles.Normal, Color.white);
            var location = CreateText("LocationText", panel.transform, "위치 -", 12, FontStyles.Normal, new Color(0.82f, 0.84f, 0.86f));
            CreateText("StatusTitleText", panel.transform, "상태이상", 12, FontStyles.Bold, new Color(1f, 0.88f, 0.58f));
            var empty = CreateText("EmptyStatusText", panel.transform, "적용된 상태이상 없음", 12, FontStyles.Normal, new Color(0.68f, 0.7f, 0.72f));

            var statusList = CreateRectObject("StatusList", panel.transform, new Vector2(272f, 40f));
            var statusLayout = statusList.AddComponent<VerticalLayoutGroup>();
            statusLayout.spacing = 5f;
            statusLayout.childControlWidth = true;
            statusLayout.childControlHeight = true;
            statusLayout.childForceExpandWidth = true;
            statusLayout.childForceExpandHeight = false;

            var entryPrefab = CreateText("StatusEntry", statusList.transform, "상태이상", 12, FontStyles.Bold, new Color(1f, 0.84f, 0.48f));
            entryPrefab.gameObject.AddComponent<LayoutElement>().minHeight = 32f;
            entryPrefab.gameObject.SetActive(false);

            var serializedView = new SerializedObject(view);
            SetObject(serializedView, "nodeEventChannel", nodeEventChannel);
            SetObject(serializedView, "panelRoot", panel);
            SetObject(serializedView, "titleText", title);
            SetObject(serializedView, "stateText", state);
            SetObject(serializedView, "hpText", hp);
            SetObject(serializedView, "attackText", attack);
            SetObject(serializedView, "locationText", location);
            SetObject(serializedView, "emptyStatusText", empty);
            SetObject(serializedView, "statusListRoot", statusList.GetComponent<RectTransform>());
            SetObject(serializedView, "statusEntryPrefab", entryPrefab);
            SetObject(serializedView, "closeButton", closeButton);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static void InstallIntoSampleScene(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvasObject = GameObject.Find("Canvas");
            var canvas = canvasObject != null ? canvasObject.GetComponent<Canvas>() : null;
            if (canvas == null)
                throw new UnityException("Canvas not found in SampleScene.");

            foreach (var existing in Object.FindObjectsByType<EnemyStatusPanelView>(FindObjectsInactive.Include))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab || existing.gameObject.name == "EnemyStatus")
                    Object.DestroyImmediate(existing.gameObject);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "EnemyStatus";
            instance.transform.SetParent(canvas.transform, false);

            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-110f, 0f);
            rect.sizeDelta = new Vector2(300f, 220f);

            var view = instance.GetComponent<EnemyStatusPanelView>();
            var serializedView = new SerializedObject(view);
            SetObject(serializedView, "panelCanvas", canvas);
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject CreateRectObject(string name, Transform parent, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = size;
            return gameObject;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, FontStyles style, Color color)
        {
            var gameObject = CreateRectObject(name, parent, new Vector2(0f, 24f));
            var textComponent = gameObject.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = size;
            textComponent.fontStyle = style;
            textComponent.color = color;
            textComponent.textWrappingMode = TextWrappingModes.Normal;
            textComponent.raycastTarget = false;
            return textComponent;
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var gameObject = CreateRectObject(name, parent, new Vector2(28f, 24f));
            var image = gameObject.AddComponent<Image>();
            image.color = new Color(0.22f, 0.22f, 0.22f, 0.95f);
            var button = gameObject.AddComponent<Button>();
            var layout = gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 28f;
            layout.preferredHeight = 24f;

            var label = CreateText("Label", gameObject.transform, "X", 13, FontStyles.Bold, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return button;
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }
    }
}
