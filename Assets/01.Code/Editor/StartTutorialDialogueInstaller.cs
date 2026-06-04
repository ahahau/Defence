using System;
using System.IO;
using _01.Code.Core;
using _01.Code.Dialogue;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    public static class StartTutorialDialogueInstaller
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string SequencePath = "Assets/03.SO/Dialogue/Sequences/StartTutorialDialogue.asset";
        private const string ValueTablePath = "Assets/03.SO/Dialogue/ValueTables/Example.asset";
        private const string CostChannelPath = "Assets/03.SO/Event/CostGameEventChannel.asset";
        private const string ChoiceButtonPath = "Assets/04.Prefab/UI/Dialogue/Elements/DialogueChoiceButton.prefab";

        [MenuItem("Tools/Defence/Install Start Tutorial Dialogue")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var sequence = EnsureTutorialSequence();
            var canvas = EnsureCanvas(scene);
            EnsureEventSystem(scene);

            var overlay = EnsureObject("StartTutorialDialogueOverlay", canvas.transform, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(DialogueView));
            ConfigureOverlay(overlay);

            var panel = EnsureChild(overlay.transform, "DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ConfigurePanel(panel);

            var title = EnsureText(panel.transform, "Title", "튜토리얼", 22f, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -24f), new Vector2(220f, 34f), TextAlignmentOptions.Left);
            var speaker = EnsureText(panel.transform, "Speaker", "관리자", 25f, FontStyles.Bold, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -70f), new Vector2(220f, 38f), TextAlignmentOptions.Left);
            var body = EnsureText(panel.transform, "Body", string.Empty, 28f, FontStyles.Normal, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(32f, 86f), new Vector2(-244f, -124f), TextAlignmentOptions.TopLeft);
            var progress = EnsureText(panel.transform, "Progress", "1/1", 19f, FontStyles.Normal, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-180f, 30f), new Vector2(78f, 30f), TextAlignmentOptions.Center);
            var skipButton = EnsureButton(panel.transform, "SkipButton", "스킵", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-134f, -24f), new Vector2(96f, 38f), true);
            var nextButton = EnsureButton(panel.transform, "NextButton", "다음", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-54f, 30f), new Vector2(112f, 46f));

            var choiceRoot = EnsureChild(panel.transform, "ChoiceRoot", typeof(RectTransform));
            ConfigureChoiceRoot(choiceRoot.GetComponent<RectTransform>());

            var view = overlay.GetComponent<DialogueView>();
            var choiceButton = AssetDatabase.LoadAssetAtPath<Button>(ChoiceButtonPath);
            view.Bind(overlay, title, speaker, body, progress, nextButton, skipButton, choiceRoot.GetComponent<RectTransform>(), choiceButton);

            var runnerObject = EnsureObject("StartTutorialDialogueSystem", null, typeof(DialogueRunner));
            ConfigureRunner(runnerObject.GetComponent<DialogueRunner>(), sequence, view);

            overlay.SetActive(false);
            overlay.transform.SetAsLastSibling();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static DialogueSequenceSO EnsureTutorialSequence()
        {
            Directory.CreateDirectory("Assets/03.SO/Dialogue/Sequences");

            var sequence = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(SequencePath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<DialogueSequenceSO>();
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }

            sequence.Configure(
                new DialogueLine("관리자", "자물쇠 타일을 눌러 던전을 확장하세요. 비용은 자물쇠 아래에 표시됩니다."),
                new DialogueLine("관리자", "빈 타일을 클릭하면 설치 패널이 열립니다. 빌딩, 유닛, 트랩, 장식품 카드 중 하나를 고르세요."),
                new DialogueLine("관리자", "포탈을 설치하면 웨이브 시작 버튼이 활성화됩니다. 준비가 끝났을 때 웨이브를 시작하세요."),
                new DialogueLine("관리자", "웨이브를 클리어하면 보상창이 뜹니다. 보상을 받고 닫으면 다시 건설 단계로 돌아옵니다."),
                new DialogueLine("관리자", "전투 중에는 적 정보에서 상태이상, 공포, 욕심을 보고 함정과 유닛 배치를 조정하세요.", new DialogueChoice("시작하기", -1)));

            EditorUtility.SetDirty(sequence);
            return sequence;
        }

        private static Canvas EnsureCanvas(UnityEngine.SceneManagement.Scene scene)
        {
            var canvas = GetComponentInScene<Canvas>(scene);
            if (canvas != null)
                return canvas;

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            return canvas;
        }

        private static void EnsureEventSystem(UnityEngine.SceneManagement.Scene scene)
        {
            if (GetComponentInScene<EventSystem>(scene) != null)
                return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void ConfigureOverlay(GameObject overlay)
        {
            var rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            var image = overlay.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.46f);
            image.raycastTarget = true;

            var canvasGroup = overlay.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        private static void ConfigurePanel(GameObject panel)
        {
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 58f);
            rect.sizeDelta = new Vector2(980f, 260f);
            rect.localScale = Vector3.one;

            var image = panel.GetComponent<Image>();
            image.color = new Color(0.055f, 0.063f, 0.078f, 0.98f);
            image.raycastTarget = true;
        }

        private static void ConfigureChoiceRoot(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 22f);
            rect.sizeDelta = new Vector2(-64f, 72f);
        }

        private static void ConfigureRunner(DialogueRunner runner, DialogueSequenceSO sequence, DialogueView view)
        {
            var serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("initialSequence").objectReferenceValue = sequence;
            serializedRunner.FindProperty("valueTable").objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueValueTableSO>(ValueTablePath);
            serializedRunner.FindProperty("costEventChannel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameEventChannelSO>(CostChannelPath);
            serializedRunner.FindProperty("view").objectReferenceValue = view;
            serializedRunner.FindProperty("playOnStart").boolValue = false;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runner);
        }

        private static GameObject EnsureObject(string name, Transform parent, params Type[] components)
        {
            var objectTransform = GetObjectTransform(name, parent);

            var gameObject = objectTransform != null ? objectTransform.gameObject : new GameObject(name, components);
            if (parent != null)
                gameObject.transform.SetParent(parent, false);

            EnsureComponents(gameObject, components);
            return gameObject;
        }

        private static Transform GetObjectTransform(string name, Transform parent)
        {
            if (parent != null)
            {
                for (var i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);
                    if (child.name == name)
                        return child;
                }

                return null;
            }

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root.name == name)
                    return root.transform;
            }

            return null;
        }

        private static T GetComponentInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }

            return null;
        }

        private static GameObject EnsureChild(Transform parent, string name, params Type[] components)
        {
            return EnsureObject(name, parent, components);
        }

        private static void EnsureComponents(GameObject gameObject, params Type[] components)
        {
            foreach (var component in components)
            {
                if (gameObject.GetComponent(component) == null)
                    gameObject.AddComponent(component);
            }
        }

        private static TMP_Text EnsureText(Transform parent, string name, string text, float fontSize, FontStyles fontStyle, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, TextAlignmentOptions alignment)
        {
            var gameObject = EnsureChild(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var label = gameObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.alignment = alignment;
            label.color = new Color(0.94f, 0.96f, 1f, 1f);
            label.enableWordWrapping = true;
            return label;
        }

        private static Button EnsureButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta, bool quiet = false)
        {
            var gameObject = EnsureChild(parent, name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            var image = gameObject.GetComponent<Image>();
            image.color = quiet ? new Color(0.12f, 0.13f, 0.15f, 0.92f) : new Color(0.18f, 0.38f, 0.45f, 1f);
            image.raycastTarget = true;

            var button = gameObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = true;

            var text = EnsureText(gameObject.transform, "Label", label, quiet ? 18f : 22f, FontStyles.Bold, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
            text.color = quiet ? new Color(0.86f, 0.88f, 0.92f, 1f) : Color.white;
            return button;
        }
    }
}
