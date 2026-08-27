#if UNITY_EDITOR
using _01.Code.Core;
using _01.Code.Manager;
using _01.Code.Progression;
using _01.Code.Skills;
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
    /// 권능 시스템과 그 조작판을 게임 씬에 설치한다.
    /// 손으로 배선하면 어떤 값이 왜 그런지 남지 않으므로 코드로 남겨 다시 돌릴 수 있게 한다.
    /// </summary>
    public static class DungeonPowerHudBuilder
    {
        private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";
        private const string PowerFolder = "Assets/03.SO/Skills/Powers";
        private const string FontPath = "Assets/08.Font/The Jamsil 5 Bold SDF.asset";

        private static readonly Color Panel = new(0.055f, 0.034f, 0.025f, 0.92f);
        private static readonly Color Accent = new(0.75f, 0.48f, 0.18f, 1f);
        private static readonly Color Parchment = new(0.95f, 0.91f, 0.82f, 1f);

        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            InstallRunStateSystems(scene);
            InstallRoutePreview(scene);
            var system = InstallSystem(scene);
            var canvas = FindHudCanvas(scene);
            if (canvas == null)
                throw new System.InvalidOperationException("씬에서 UI Canvas를 찾지 못했습니다.");

            var hud = BuildHud(canvas.transform, system);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Dungeon power HUD installed with {system.Powers.Count} powers.", hud);
        }

        /// <summary>
        /// 한 판 동안만 사는 상태들. 씬에 컴포넌트로 두면 판이 끝날 때 함께 사라지므로
        /// 지난 판의 장악도나 전과가 다음 판으로 새지 않는다.
        /// </summary>
        private static void InstallRunStateSystems(Scene scene)
        {
            var existing = FirstComponent<VillageConquestSystem>(scene);
            var host = existing != null
                ? existing.gameObject
                : FirstComponent<RunSummarySystem>(scene)?.gameObject ?? new GameObject("RunStateSystems");

            if (host.GetComponent<VillageConquestSystem>() == null)
                host.AddComponent<VillageConquestSystem>();
            if (host.GetComponent<RunSummarySystem>() == null)
                host.AddComponent<RunSummarySystem>();
        }

        /// <summary>
        /// 침입 경로 표시. 채널은 이미 씬에서 쓰이는 것을 그대로 빌려 온다 —
        /// 새 채널을 만들면 아무도 그 이벤트를 보내지 않는다.
        /// </summary>
        private static void InstallRoutePreview(Scene scene)
        {
            var existing = FirstComponent<IntrusionRoutePreview>(scene);
            var go = existing != null ? existing.gameObject : new GameObject("IntrusionRoutePreview");
            var preview = existing != null ? existing : go.AddComponent<IntrusionRoutePreview>();

            var wave = FirstComponent<WaveManager>(scene);
            if (wave == null)
                return;

            var waveSerialized = new SerializedObject(wave);
            var serialized = new SerializedObject(preview);
            serialized.FindProperty("nodeEventChannel").objectReferenceValue =
                waveSerialized.FindProperty("nodeEventChannel").objectReferenceValue;
            serialized.FindProperty("waveEventChannel").objectReferenceValue =
                waveSerialized.FindProperty("waveEventChannel").objectReferenceValue;
            serialized.FindProperty("dayEventChannel").objectReferenceValue =
                waveSerialized.FindProperty("dayEventChannel").objectReferenceValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DungeonPowerSystem InstallSystem(Scene scene)
        {
            var existing = FirstComponent<DungeonPowerSystem>(scene);
            var go = existing != null ? existing.gameObject : new GameObject("DungeonPowerSystem");
            var system = existing != null ? existing : go.AddComponent<DungeonPowerSystem>();

            var serialized = new SerializedObject(system);
            // 웨이브 채널은 이미 씬에 있는 것을 그대로 쓴다. 새로 만들면 아무도 듣지 않는다.
            var wave = FirstComponent<WaveManager>(scene);
            if (wave != null)
            {
                var waveChannel = new SerializedObject(wave).FindProperty("waveEventChannel").objectReferenceValue;
                serialized.FindProperty("waveEventChannel").objectReferenceValue = waveChannel;
            }

            var powers = serialized.FindProperty("powers");
            var guids = AssetDatabase.FindAssets("t:DungeonPowerSO", new[] { PowerFolder });
            powers.arraySize = guids.Length;
            for (var i = 0; i < guids.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<DungeonPowerSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                powers.GetArrayElementAtIndex(i).objectReferenceValue = asset;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return system;
        }

        private static DungeonPowerHudView BuildHud(Transform canvasRoot, DungeonPowerSystem system)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

            var existing = Find(canvasRoot, "DungeonPowerHud");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            // 화면 하단 가운데. 전투를 가리지 않으면서 손이 닿는 자리다.
            // 피벗을 아래로 두지 않으면 절반이 화면 밖으로 나간다.
            var root = Ui("DungeonPowerHud", canvasRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(720f, 150f));
            ((RectTransform)root.transform).pivot = new Vector2(0.5f, 0f);
            root.transform.SetAsLastSibling();
            var view = root.AddComponent<DungeonPowerHudView>();

            var panel = Ui("Panel", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panel.AddComponent<Image>().color = Panel;
            var outline = panel.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(2f, -2f);

            var barBack = Ui("PowerBarBack", panel.transform, new Vector2(0.04f, 0.72f), new Vector2(0.96f, 0.90f), Vector2.zero, Vector2.zero);
            barBack.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 1f);
            var barFill = Ui("PowerBarFill", barBack.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var fillImage = barFill.AddComponent<Image>();
            fillImage.color = new Color(0.55f, 0.35f, 0.9f, 1f);
            // Filled 타입은 스프라이트가 있어야 fillAmount가 먹는다. 비워 두면 늘 가득 찬 것처럼 보인다.
            fillImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;

            var powerLabel = Text(panel.transform, "PowerText", "권능 0 / 100", new Vector2(0.04f, 0.90f), new Vector2(0.60f, 1f), 17, font);
            var hint = Text(panel.transform, "HintText", string.Empty, new Vector2(0.40f, 0.90f), new Vector2(0.96f, 1f), 16, font);
            hint.alignment = TextAlignmentOptions.Right;
            hint.color = Accent;

            var buttonRoot = Ui("PowerButtons", panel.transform, new Vector2(0.03f, 0.06f), new Vector2(0.97f, 0.66f), Vector2.zero, Vector2.zero);
            var layout = buttonRoot.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var template = Button(buttonRoot.transform, "PowerButtonTemplate", "권능", new Color(0.20f, 0.13f, 0.07f, 1f), font);
            template.gameObject.SetActive(false);

            var serialized = new SerializedObject(view);
            serialized.FindProperty("powerSystem").objectReferenceValue = system;
            serialized.FindProperty("panelRoot").objectReferenceValue = panel;
            serialized.FindProperty("powerFill").objectReferenceValue = fillImage;
            serialized.FindProperty("powerText").objectReferenceValue = powerLabel;
            serialized.FindProperty("hintText").objectReferenceValue = hint;
            serialized.FindProperty("powerButtonTemplate").objectReferenceValue = template;
            serialized.FindProperty("powerButtonRoot").objectReferenceValue = buttonRoot.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
            return view;
        }

        /// <summary>
        /// 다른 패널들이 얹혀 있는 씬 UI 캔버스를 고른다.
        /// 이 씬의 메인 캔버스는 루트가 아니라 UI 묶음 오브젝트 아래에 있고, 모달들이 저마다
        /// 정렬용 캔버스를 따로 달고 있다. 그래서 자식이 가장 많은 오버레이 캔버스를 고른다.
        /// </summary>
        private static Canvas FindHudCanvas(Scene scene)
        {
            Canvas best = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                {
                    if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        continue;

                    if (best == null || canvas.transform.childCount > best.transform.childCount)
                        best = canvas;
                }
            }

            return best;
        }

        private static GameObject Ui(string name, Transform parent, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return go;
        }

        private static Button Button(Transform parent, string name, string label, Color color, TMP_FontAsset font)
        {
            var go = Ui(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = go.AddComponent<Image>();
            image.color = color;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Text(go.transform, "Label", label, Vector2.zero, Vector2.one, 18, font);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TMP_Text Text(Transform parent, string name, string value, Vector2 min, Vector2 max, float size, TMP_FontAsset font)
        {
            var go = Ui(name, parent, min, max, Vector2.zero, Vector2.zero);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = Parchment;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
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
