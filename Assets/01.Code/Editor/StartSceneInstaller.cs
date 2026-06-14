#if UNITY_EDITOR
using _01.Code.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    public static class StartSceneInstaller
    {
        private const string StartScenePath = "Assets/00.Scenes/Start.unity";
        private const string GameScenePath = "Assets/00.Scenes/SampleScene.unity";

        [MenuItem("Tools/Defence/Install Start Scene")]
        public static void Install()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Start";

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.08f);
            cameraObject.tag = "MainCamera";

            EnsureEventSystem();
            var canvas = CreateCanvas();
            var controller = canvas.gameObject.AddComponent<StartMenuController>();

            var background = CreatePanel("Background", canvas.transform, new Color(0.08f, 0.1f, 0.12f, 1f));
            Stretch(background);

            var title = CreateText("Title", canvas.transform, "DEFENCE", 76, TextAlignmentOptions.Center);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 0.72f), new Vector2(560f, 110f));

            var startButton = CreateButton("StartButton", canvas.transform, "START", new Vector2(0.5f, 0.52f));
            var settingsButton = CreateButton("SettingsButton", canvas.transform, "SETTINGS", new Vector2(0.5f, 0.41f));
            var quitButton = CreateButton("QuitButton", canvas.transform, "QUIT", new Vector2(0.5f, 0.3f));

            var settingsPanel = CreateSettingsPanel(canvas.transform, out var soundSettings, out var closeButton, out var resetButton);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("gameSceneName").stringValue = "SampleScene";
            serialized.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            serialized.FindProperty("soundSettingsController").objectReferenceValue = soundSettings;
            serialized.FindProperty("startButton").objectReferenceValue = startButton;
            serialized.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            serialized.FindProperty("quitButton").objectReferenceValue = quitButton;
            serialized.FindProperty("closeSettingsButton").objectReferenceValue = closeButton;
            serialized.FindProperty("resetSoundButton").objectReferenceValue = resetButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, StartScenePath);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Start scene installed.");
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject("StartCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private static GameObject CreateSettingsPanel(Transform parent, out SoundSettingsController soundSettings, out Button closeButton, out Button resetButton)
        {
            var root = CreatePanel("SettingsPanel", parent, new Color(0.04f, 0.05f, 0.06f, 0.94f));
            Stretch(root);
            root.SetActive(false);

            var window = CreatePanel("SettingsWindow", root.transform, new Color(0.12f, 0.14f, 0.16f, 1f));
            SetRect(window.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(620f, 520f));

            var title = CreateText("SettingsTitle", window.transform, "SETTINGS", 42, TextAlignmentOptions.Center);
            SetAnchored(title.rectTransform, new Vector2(0.5f, 0.84f), new Vector2(520f, 64f));

            soundSettings = window.AddComponent<SoundSettingsController>();
            var masterSlider = CreateSliderRow(window.transform, "Master", "MASTER", new Vector2(0.5f, 0.63f), out var masterValue);
            var sfxSlider = CreateSliderRow(window.transform, "Sfx", "SFX", new Vector2(0.5f, 0.48f), out var sfxValue);

            closeButton = CreateButton("CloseSettingsButton", window.transform, "CLOSE", new Vector2(0.35f, 0.2f), new Vector2(190f, 58f));
            resetButton = CreateButton("ResetSoundButton", window.transform, "RESET", new Vector2(0.65f, 0.2f), new Vector2(190f, 58f));

            var serialized = new SerializedObject(soundSettings);
            serialized.FindProperty("masterSlider").objectReferenceValue = masterSlider;
            serialized.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            serialized.FindProperty("masterValueText").objectReferenceValue = masterValue;
            serialized.FindProperty("sfxValueText").objectReferenceValue = sfxValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        private static Slider CreateSliderRow(Transform parent, string name, string label, Vector2 anchor, out TMP_Text valueText)
        {
            var row = new GameObject($"{name}VolumeRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetRect(row.GetComponent<RectTransform>(), anchor, new Vector2(500f, 82f));

            var labelText = CreateText($"{name}VolumeLabel", row.transform, label, 24, TextAlignmentOptions.Left);
            SetAnchored(labelText.rectTransform, new Vector2(0.18f, 0.72f), new Vector2(160f, 32f));

            valueText = CreateText($"{name}VolumeValue", row.transform, "100%", 22, TextAlignmentOptions.Right);
            SetAnchored(valueText.rectTransform, new Vector2(0.84f, 0.72f), new Vector2(120f, 32f));

            var sliderObject = new GameObject($"{name}VolumeSlider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(row.transform, false);
            SetRect(sliderObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.28f), new Vector2(430f, 28f));

            var background = CreatePanel("Background", sliderObject.transform, new Color(0.24f, 0.27f, 0.3f, 1f));
            Stretch(background);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObject.transform, false);
            Stretch(fillArea);
            var fill = CreatePanel("Fill", fillArea.transform, new Color(0.75f, 0.53f, 0.24f, 1f));
            Stretch(fill);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderObject.transform, false);
            Stretch(handleArea);
            var handle = CreatePanel("Handle", handleArea.transform, new Color(0.92f, 0.88f, 0.72f, 1f));
            SetRect(handle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(28f, 36f));

            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchor)
        {
            return CreateButton(name, parent, label, anchor, new Vector2(260f, 68f));
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 anchor, Vector2 size)
        {
            var buttonObject = CreatePanel(name, parent, new Color(0.2f, 0.24f, 0.27f, 1f));
            SetRect(buttonObject.GetComponent<RectTransform>(), anchor, size);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();

            var text = CreateText("Label", buttonObject.transform, label, 28, TextAlignmentOptions.Center);
            Stretch(text.gameObject);
            return button;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            var label = textObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color(0.92f, 0.9f, 0.82f, 1f);
            label.raycastTarget = false;
            return label;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(parent, false);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static void Stretch(GameObject target)
        {
            if (target != null)
                Stretch(target.GetComponent<RectTransform>());
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetAnchored(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            SetRect(rect, anchor, size);
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        private static void UpdateBuildSettings()
        {
            var startGuid = AssetDatabase.AssetPathToGUID(StartScenePath);
            var gameGuid = AssetDatabase.AssetPathToGUID(GameScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(StartScenePath, true) { guid = new GUID(startGuid) },
                new EditorBuildSettingsScene(GameScenePath, true) { guid = new GUID(gameGuid) },
            };
        }
    }
}
#endif
