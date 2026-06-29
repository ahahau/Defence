using _01.Code.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    /// <summary>씬 캔버스에 항상 보이는 독립 '설치' 버튼을 만들어 NodePanelView의 설치 메뉴에 연결한다.
    /// 노드를 선택한 뒤 이 버튼을 누르면 설치 카테고리(유닛/빌딩/트랩/장식품) 패널이 열린다.</summary>
    public static class NodeInstallButtonInstaller
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string ButtonName = "NodeInstallButton";

        [MenuItem("Tools/Defence/Install Node Install Button")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var canvas = GetComponentInScene<Canvas>(scene);
            if (canvas == null)
            {
                Debug.LogError("NodeInstallButtonInstaller requires a Canvas in the scene.");
                return;
            }

            var nodePanel = GetComponentInScene<NodePanelView>(scene);
            if (nodePanel == null)
            {
                Debug.LogError("NodeInstallButtonInstaller requires a NodePanelView in the scene.");
                return;
            }

            var button = EnsureButton(canvas.transform);

            // 클릭 시 선택된 노드의 설치 카테고리 패널을 연다(영속 리스너로 씬에 저장).
            for (var i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            UnityEventTools.AddPersistentListener(button.onClick, nodePanel.ShowSelectedNodeInstallOptions);

            EditorUtility.SetDirty(button);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("NodeInstallButtonInstaller: '설치' 버튼을 캔버스에 설치하고 NodePanelView에 연결했습니다.", button);
        }

        private static Button EnsureButton(Transform canvasTransform)
        {
            var existing = GetDirectChild(canvasTransform, ButtonName);
            var root = existing != null
                ? existing
                : new GameObject(ButtonName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)).transform;
            if (existing == null)
                root.SetParent(canvasTransform, false);

            var rect = root as RectTransform;
            // 우하단 모서리에 고정.
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 40f);
            rect.sizeDelta = new Vector2(220f, 76f);
            rect.localScale = Vector3.one;

            var image = root.GetComponent<Image>();
            image.color = new Color(0.16f, 0.42f, 0.86f, 0.95f);
            image.raycastTarget = true;

            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.28f, 0.55f, 0.95f, 1f);
            colors.pressedColor = new Color(0.10f, 0.30f, 0.66f, 1f);
            button.colors = colors;

            EnsureLabel(root, "설치");

            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            return button;
        }

        private static void EnsureLabel(Transform parent, string text)
        {
            var labelTransform = GetDirectChild(parent, "Label");
            if (labelTransform == null)
            {
                labelTransform = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).transform;
                labelTransform.SetParent(parent, false);
            }

            var rect = labelTransform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var label = labelTransform.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 30f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.97f, 0.98f, 1f, 1f);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        private static Transform GetDirectChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }

            return null;
        }

        private static T GetComponentInScene<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                if (root.TryGetComponent<T>(out var rootComponent))
                    return rootComponent;

                var components = root.GetComponentsInChildren<T>(true);
                if (components != null && components.Length > 0)
                    return components[0];
            }

            return null;
        }
    }
}
