using _01.Code.MapCreateSystem;
using _01.Code.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    public static class PlayerStatusHudInstaller
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";

        [MenuItem("Tools/Defence/Install Player Status HUD")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = GetComponentInScene<Canvas>(scene);
            if (canvas == null)
            {
                Debug.LogError("PlayerStatusHudInstaller requires a Canvas in the scene.");
                return;
            }

            var hud = EnsureHud(canvas.transform);
            var controller = GetComponentInScene<DungeonGraphController>(scene);
            if (controller != null)
            {
                controller.EditorSetPlayerStatusHud(hud);
                EditorUtility.SetDirty(controller);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static PlayerStatusHudView EnsureHud(Transform canvasTransform)
        {
            var root = GetDirectChild(canvasTransform, "PlayerStatusHud");
            if (root == null)
            {
                root = new GameObject("PlayerStatusHud", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(PlayerStatusHudView)).transform;
                root.SetParent(canvasTransform, false);
            }

            var rootRect = root as RectTransform;
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 28f);
            rootRect.sizeDelta = new Vector2(680f, 88f);
            rootRect.localScale = Vector3.one;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.055f, 0.065f, 0.08f, 0.92f);
            background.raycastTarget = false;

            var healthSlider = EnsureSlider(root, "HealthBar", new Vector2(0f, 16f), new Vector2(500f, 18f), new Color(0.72f, 0.16f, 0.14f, 1f));
            var expSlider = EnsureSlider(root, "ExpBar", new Vector2(0f, 42f), new Vector2(500f, 14f), new Color(0.18f, 0.52f, 0.9f, 1f));
            var hpText = EnsureText(root, "HealthText", "HP -", new Vector2(246f, 16f), new Vector2(140f, 24f), 18f, TextAlignmentOptions.Right);
            var expText = EnsureText(root, "ExpText", "EXP -", new Vector2(246f, 42f), new Vector2(140f, 22f), 16f, TextAlignmentOptions.Right);
            var levelText = EnsureText(root, "LevelText", "Lv -", new Vector2(-272f, 22f), new Vector2(96f, 36f), 24f, TextAlignmentOptions.Center);

            var hud = root.GetComponent<PlayerStatusHudView>();
            hud.EditorConfigure(healthSlider, hpText, expSlider, expText, levelText);
            EditorUtility.SetDirty(hud);

            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            return hud;
        }

        private static Slider EnsureSlider(Transform parent, string name, Vector2 position, Vector2 size, Color fillColor)
        {
            var transform = GetDirectChild(parent, name);
            if (transform == null)
            {
                transform = new GameObject(name, typeof(RectTransform), typeof(Slider)).transform;
                transform.SetParent(parent, false);
            }

            var rect = transform as RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var background = EnsureImage(transform, "Background", new Color(0.02f, 0.025f, 0.03f, 0.95f));
            Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

            var fillArea = GetDirectChild(transform, "Fill Area");
            if (fillArea == null)
            {
                fillArea = new GameObject("Fill Area", typeof(RectTransform)).transform;
                fillArea.SetParent(transform, false);
            }
            Stretch(fillArea as RectTransform, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            var fill = EnsureImage(fillArea, "Fill", fillColor);
            Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);

            var slider = transform.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.transition = Selectable.Transition.None;
            slider.targetGraphic = null;
            slider.fillRect = fill.rectTransform;
            slider.interactable = false;
            return slider;
        }

        private static Image EnsureImage(Transform parent, string name, Color color)
        {
            var transform = GetDirectChild(parent, name);
            if (transform == null)
            {
                transform = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).transform;
                transform.SetParent(parent, false);
            }

            var image = transform.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text EnsureText(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var transform = GetDirectChild(parent, name);
            if (transform == null)
            {
                transform = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).transform;
                transform.SetParent(parent, false);
            }

            var rect = transform as RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var label = transform.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.color = new Color(0.95f, 0.96f, 0.95f, 1f);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
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
