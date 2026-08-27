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
    /// <summary>
    /// 타이틀 화면을 로고 중심으로 다시 짠다.
    /// 씬을 손으로 만지면 어떤 값이 왜 그런지 남지 않으므로, 배치를 코드로 남겨 다시 돌릴 수 있게 한다.
    /// </summary>
    public static class TitleScreenBuilder
    {
        private const string ScenePath = "Assets/00.Scenes/Start.unity";
        private const string LogoPath = "Assets/02.Art/Logo.png";
        private const string FontPath = "Assets/08.Font/The Jamsil 5 Bold SDF.asset";

        // 던전 패널들과 같은 계열. 타이틀만 튀면 게임에 들어갔을 때 딴 게임처럼 보인다.
        private static readonly Color Backdrop = new(0.043f, 0.027f, 0.020f, 1f);
        private static readonly Color Card = new(0.15f, 0.085f, 0.045f, 1f);
        private static readonly Color Accent = new(0.75f, 0.48f, 0.18f, 1f);
        private static readonly Color Parchment = new(0.95f, 0.91f, 0.82f, 1f);

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = FirstComponent<Canvas>(scene);
            if (canvas == null)
                throw new System.InvalidOperationException("Start 씬에서 Canvas를 찾지 못했습니다.");

            // 캔버스가 해상도에 따라 늘어나야 로고가 어느 화면에서도 같은 비율로 보인다.
            ConfigureScaler(canvas);

            var root = canvas.transform;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);

            StyleBackdrop(Find(root, "Background"));
            var logoImage = BuildLogo(root, logo);
            StyleTagline(Find(root, "Title"), font);
            StyleButton(Find(root, "StartButton"), font, "게임 시작", 0.375f, Accent);
            StyleButton(Find(root, "QuitButton"), font, "종료", 0.275f, Card);
            BuildFooter(root, font);

            // 설정은 사운드가 돌아올 때까지 감춰 둔다. 지우면 다시 만들어야 하므로 남기되,
            // 버튼을 켜 둔 채로는 시작 버튼과 같은 자리에 겹쳐 그려진다.
            var settings = Find(root, "SettingsPanel");
            if (settings != null)
                settings.gameObject.SetActive(false);

            var settingsButton = Find(root, "SettingsButton");
            if (settingsButton != null)
                settingsButton.gameObject.SetActive(false);

            if (logoImage != null)
                logoImage.transform.SetSiblingIndex(1);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Title screen rebuilt around the logo.");
        }

        /// <summary>
        /// 로고 원본은 여백이 넓은 정사각에 가까운 이미지라, 화면 폭 기준으로 놓고
        /// preserveAspect로 비율을 지킨다. 고정 픽셀 크기로 박으면 해상도가 바뀔 때 깨진다.
        /// </summary>
        private static Image BuildLogo(Transform root, Sprite sprite)
        {
            var existing = Find(root, "Logo");
            var go = existing != null ? existing.gameObject : NewUi("Logo", root);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -90f);
            rect.sizeDelta = new Vector2(820f, 546f);

            var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;

            if (sprite == null)
                Debug.LogWarning($"{LogoPath} 스프라이트를 찾지 못했습니다. 로고 칸이 비어 있습니다.");

            return image;
        }

        private static void StyleBackdrop(Transform background)
        {
            if (background == null)
                return;

            var rect = (RectTransform)background;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = background.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = Backdrop;
            }
        }

        /// <summary>로고가 무슨 게임인지는 말해 주지만 무엇을 하는 게임인지는 말해 주지 않는다.</summary>
        private static void StyleTagline(Transform title, TMP_FontAsset font)
        {
            if (title == null)
                return;

            var text = title.GetComponent<TMP_Text>();
            if (text == null)
                return;

            var rect = (RectTransform)title;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -520f);
            rect.sizeDelta = new Vector2(900f, 60f);

            if (font != null)
                text.font = font;
            text.text = "스무 날의 침공을 버텨내라";
            text.fontSize = 30f;
            text.color = Accent;
            text.alignment = TextAlignmentOptions.Center;
        }

        private static void StyleButton(Transform button, TMP_FontAsset font, string label, float verticalAnchor, Color fill)
        {
            if (button == null)
                return;

            var rect = (RectTransform)button;
            rect.anchorMin = new Vector2(0.5f, verticalAnchor);
            rect.anchorMax = new Vector2(0.5f, verticalAnchor);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(360f, 74f);

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = fill;
            }

            var outline = button.GetComponent<Outline>() ?? button.gameObject.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(2f, -2f);

            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
                return;

            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            if (font != null)
                text.font = font;
            text.text = label;
            text.fontSize = 30f;
            text.color = Parchment;
            text.alignment = TextAlignmentOptions.Center;
        }

        private static void BuildFooter(Transform root, TMP_FontAsset font)
        {
            var existing = Find(root, "Footer");
            var go = existing != null ? existing.gameObject : NewUi("Footer", root);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
            rect.sizeDelta = new Vector2(900f, 40f);

            var text = go.GetComponent<TMP_Text>() ?? go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.text = "졸업작품 · 박연우";
            text.fontSize = 20f;
            text.color = new Color(Parchment.r, Parchment.g, Parchment.b, 0.45f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        private static void ConfigureScaler(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private static GameObject NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            return null;
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
