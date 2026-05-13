using _01.Code.Dialogue;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    public class DialogueTestSceneFactory
    {
        private const string DialogueFolderPath = "Assets/03.SO/Dialogue";
        private const string SequencePath = DialogueFolderPath + "/TestDialogueSequence.asset";
        private const string ScenePath = "Assets/00.Scenes/DialogueTestScene.unity";

        [MenuItem("Tools/Defence/Create Dialogue Test Scene")]
        public static void CreateFromMenu()
        {
            CreateScene();
        }

        public static void CreateScene()
        {
            EnsureFolder(DialogueFolderPath);

            var sequence = EnsureSequence();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DialogueTestScene";

            CreateCamera();
            CreateDialogueUi(sequence);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static DialogueSequenceSO EnsureSequence()
        {
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(SequencePath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<DialogueSequenceSO>();
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }

            var serialized = new SerializedObject(sequence);
            serialized.FindProperty("displayName").stringValue = "SO Dialogue Test";

            var lines = serialized.FindProperty("lines");
            lines.arraySize = 3;
            SetLine(lines.GetArrayElementAtIndex(0), "Guide", "이 대화는 DialogueSequenceSO 에셋에서 읽어옵니다.");
            SetLine(lines.GetArrayElementAtIndex(1), "Player", "대사 내용은 씬이 아니라 SO에서 관리되니 재사용하기 좋습니다.");
            SetLine(lines.GetArrayElementAtIndex(2), "Guide", "Next 버튼을 누르면 DialogueRunner가 다음 줄로 진행합니다.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sequence);

            return sequence;
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
            camera.orthographic = true;
        }

        private static void CreateDialogueUi(DialogueSequenceSO sequence)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasObject = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelObject = CreatePanel(canvasObject.transform);
            var titleText = CreateText("TitleText", panelObject.transform, new Vector2(40f, -34f), new Vector2(720f, 48f), 30f, TextAlignmentOptions.Left, new Color(0.95f, 0.82f, 0.36f, 1f));
            var speakerText = CreateText("SpeakerText", panelObject.transform, new Vector2(40f, -92f), new Vector2(360f, 44f), 26f, TextAlignmentOptions.Left, new Color(0.48f, 0.82f, 0.88f, 1f));
            var bodyText = CreateText("BodyText", panelObject.transform, new Vector2(40f, -145f), new Vector2(850f, 118f), 24f, TextAlignmentOptions.TopLeft, Color.white);
            var progressText = CreateText("ProgressText", panelObject.transform, new Vector2(1030f, -36f), new Vector2(100f, 40f), 22f, TextAlignmentOptions.Right, new Color(0.72f, 0.76f, 0.8f, 1f));
            var nextButton = CreateButton("NextButton", panelObject.transform, new Vector2(910f, -218f), new Vector2(180f, 56f), "Next");
            var closeButton = CreateButton("CloseButton", panelObject.transform, new Vector2(910f, -284f), new Vector2(180f, 48f), "Close");

            var view = panelObject.AddComponent<DialogueView>();
            var viewObject = new SerializedObject(view);
            viewObject.FindProperty("root").objectReferenceValue = panelObject;
            viewObject.FindProperty("titleText").objectReferenceValue = titleText;
            viewObject.FindProperty("speakerText").objectReferenceValue = speakerText;
            viewObject.FindProperty("bodyText").objectReferenceValue = bodyText;
            viewObject.FindProperty("progressText").objectReferenceValue = progressText;
            viewObject.FindProperty("nextButton").objectReferenceValue = nextButton;
            viewObject.FindProperty("closeButton").objectReferenceValue = closeButton;
            viewObject.ApplyModifiedPropertiesWithoutUndo();

            var runner = new GameObject("DialogueSystem", typeof(DialogueRunner)).GetComponent<DialogueRunner>();
            var runnerObject = new SerializedObject(runner);
            runnerObject.FindProperty("initialSequence").objectReferenceValue = sequence;
            runnerObject.FindProperty("view").objectReferenceValue = view;
            runnerObject.FindProperty("playOnStart").boolValue = true;
            runnerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePanel(Transform parent)
        {
            var panelObject = new GameObject("DialoguePanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            var rect = (RectTransform)panelObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 72f);
            rect.sizeDelta = new Vector2(1180f, 330f);

            panelObject.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.96f);
            return panelObject;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            var rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, string label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            buttonObject.GetComponent<Image>().color = new Color(0.19f, 0.39f, 0.45f, 1f);

            var labelText = CreateText("Label", buttonObject.transform, Vector2.zero, Vector2.zero, 22f, TextAlignmentOptions.Center, Color.white);
            var labelRect = (RectTransform)labelText.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            labelText.text = label;

            return buttonObject.GetComponent<Button>();
        }

        private static void SetLine(SerializedProperty line, string speakerName, string text)
        {
            line.FindPropertyRelative("speakerName").stringValue = speakerName;
            line.FindPropertyRelative("text").stringValue = text;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            var folderName = path.Substring(slash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
