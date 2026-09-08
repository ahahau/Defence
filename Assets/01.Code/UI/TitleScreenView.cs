using System.Collections.Generic;
using _01.Code.Audio;
using _01.Code.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 타이틀 화면.
    ///
    /// 던전은 노드를 이어 만드는 게임이라, 첫 화면도 그 판을 그대로 보여 준다.
    /// 배경에 노드와 통로를 깔고 그 위에 버튼 넷만 올린다 — 설명하는 글은 두지 않는다.
    /// 무엇을 하는 게임인지는 판이 말한다.
    ///
    /// 씬에 배선하지 않고 실행 시점에 스스로 올라온다. <see cref="SettingsPanelView"/>가
    /// 같은 이유로 쓰는 방식이다 — 씬은 다른 작업과 함께 건드리는 파일이라 충돌하기 쉽고,
    /// 이 화면은 게임 상태에 의존하지 않아 코드만으로 세워도 잃는 게 없다.
    /// </summary>
    public sealed class TitleScreenView : MonoBehaviour
    {
        /// <summary>타이틀이 올라갈 씬. 여기가 아니면 아무것도 하지 않는다.</summary>
        public const string TitleSceneName = "Start";

        /// <summary>시작하면 넘어갈 씬.</summary>
        public const string GameSceneName = "SampleScene";

        private static readonly Color Ground = new(0.043f, 0.031f, 0.027f, 1f);
        private static readonly Color Corridor = new(0.30f, 0.21f, 0.14f, 1f);
        private static readonly Color NodeFill = new(0.13f, 0.094f, 0.068f, 1f);
        private static readonly Color NodeEdge = new(0.52f, 0.37f, 0.19f, 1f);
        private static readonly Color CoreEdge = new(0.86f, 0.62f, 0.26f, 1f);
        private static readonly Color ButtonText = new(0.95f, 0.91f, 0.83f, 1f);
        private static readonly Color ButtonTextOff = new(0.48f, 0.44f, 0.39f, 1f);

        private const float NodeSize = 86f;
        private const float CoreSize = 118f;

        private UiSkinSO skin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (SceneManager.GetActiveScene().name != TitleSceneName)
                return;

            var host = new GameObject("Title Screen");
            host.AddComponent<TitleScreenView>();
        }

        private void Start()
        {
            skin = Resources.Load<UiSkinSO>("UI/UiSkin");
            HideLegacyMenu();

            var canvas = BuildCanvas();
            BuildBackdrop(canvas.transform);
            BuildMenu(canvas.transform);
        }

        /// <summary>
        /// 씬에 예전부터 있던 메뉴를 끈다. 지우지 않고 끄기만 하는 이유는 씬 파일을 건드리지
        /// 않기 위해서다 — 켜져 있으면 새 화면 뒤로 옛 버튼과 로고가 비친다.
        /// </summary>
        private static void HideLegacyMenu()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas == null || canvas.gameObject.name != "StartCanvas")
                    continue;

                canvas.gameObject.SetActive(false);
            }
        }

        private static Canvas BuildCanvas()
        {
            var go = new GameObject("Title Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 설정 창(5000)보다는 아래. 타이틀에서도 설정을 열 수 있어야 한다.
            canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        // ── 배경: 노드와 통로 ─────────────────────────────────────────────

        /// <summary>
        /// 노드 자리. 화면 기준 비율이라 어느 해상도에서도 같은 모양으로 눕는다.
        /// 마지막 하나가 중심부(코어)이고, 나머지는 거기서 뻗어 나간 방이다.
        /// </summary>
        private static readonly Vector2[] NodeSpots =
        {
            new(-0.34f, 0.26f), new(-0.19f, -0.05f), new(-0.36f, -0.28f),
            new(0.20f, 0.30f), new(0.33f, 0.02f), new(0.17f, -0.27f),
            new(-0.02f, 0.13f),
        };

        /// <summary>이어질 노드 쌍. 마지막 노드(코어)를 중심으로 모인다.</summary>
        private static readonly (int, int)[] Corridors =
        {
            (0, 6), (1, 6), (3, 6), (4, 6),
            (1, 2), (4, 5), (0, 1), (3, 4),
        };

        private void BuildBackdrop(Transform parent)
        {
            var ground = CreateImage(parent, "Ground", Ground);
            Stretch(ground.rectTransform);

            // 통로를 먼저 깔아야 노드가 그 위에 얹힌다.
            var layer = new GameObject("Node Graph", typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            Stretch((RectTransform)layer.transform);

            var points = new List<Vector2>();
            foreach (var spot in NodeSpots)
                points.Add(new Vector2(spot.x * 1920f, spot.y * 1080f));

            foreach (var (from, to) in Corridors)
                CreateCorridor(layer.transform, points[from], points[to]);

            for (var i = 0; i < points.Count; i++)
            {
                var isCore = i == points.Count - 1;
                CreateNode(layer.transform, points[i], isCore);
            }
        }

        /// <summary>두 노드를 잇는 통로 한 줄. 회전한 얇은 사각형이면 충분하다.</summary>
        private static void CreateCorridor(Transform parent, Vector2 from, Vector2 to)
        {
            var image = CreateImage(parent, "Corridor", Corridor);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            var delta = to - from;
            rect.sizeDelta = new Vector2(delta.magnitude, 7f);
            rect.anchoredPosition = from + delta * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <summary>노드 하나. 코어는 조금 크고 테두리가 밝다.</summary>
        private void CreateNode(Transform parent, Vector2 position, bool isCore)
        {
            var size = isCore ? CoreSize : NodeSize;
            var edge = CreateImage(parent, isCore ? "Core" : "Node", isCore ? CoreEdge : NodeEdge,
                SkillZoneVisualCircle());
            var rect = edge.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;

            var inner = CreateImage(edge.transform, "Inner", NodeFill, SkillZoneVisualCircle());
            var innerRect = inner.rectTransform;
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(5f, 5f);
            innerRect.offsetMax = new Vector2(-5f, -5f);
        }

        /// <summary>지대·시전 고리가 쓰는 원 그림을 그대로 빌려 온다. 원을 또 만들 이유가 없다.</summary>
        private static Sprite SkillZoneVisualCircle() => Skills.SkillZoneVisual.CircleSprite;

        // ── 버튼 넷 ──────────────────────────────────────────────────────

        private void BuildMenu(Transform parent)
        {
            var column = new GameObject("Menu", typeof(RectTransform));
            column.transform.SetParent(parent, false);
            var rect = (RectTransform)column.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 300f);
            rect.anchoredPosition = Vector2.zero;

            var hasSave = RunSaveSystem.HasSave;

            CreateMenuButton(column.transform, "새로하기", 108f, true, StartNewRun);
            // 저장이 없으면 누를 수 없다. 버튼을 감추는 대신 흐리게 두어 자리가 흔들리지 않게 한다.
            CreateMenuButton(column.transform, "이어하기", 36f, hasSave, ContinueRun);
            CreateMenuButton(column.transform, "설정", -36f, true, () => SettingsPanelView.Open());
            CreateMenuButton(column.transform, "나가기", -108f, true, QuitGame);
        }

        private void CreateMenuButton(Transform parent, string label, float y, bool enabled,
            UnityEngine.Events.UnityAction action)
        {
            var frame = skin != null ? skin.ButtonFrame : null;
            var image = CreateImage(parent, "Button " + label,
                frame != null ? Color.white : NodeFill, frame);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(300f, 60f);
            rect.anchoredPosition = new Vector2(0f, y);
            image.type = frame != null && frame.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;

            var text = CreateLabel(image.transform, label, enabled ? ButtonText : ButtonTextOff);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            if (enabled)
                button.onClick.AddListener(() =>
                {
                    GameSfxPlayer.Play(GameSfxCue.UiClick);
                    action();
                });

            // 테두리 없는 대체 그림일 때만 눌린 느낌을 색으로 준다. 팩 프레임은 자체 명암이 있다.
            if (frame == null)
            {
                var colors = button.colors;
                colors.highlightedColor = new Color(0.20f, 0.15f, 0.10f, 1f);
                colors.pressedColor = new Color(0.10f, 0.07f, 0.05f, 1f);
                button.colors = colors;
            }

            if (!enabled)
                text.alpha = 0.75f;
        }

        // ── 동작 ────────────────────────────────────────────────────────

        /// <summary>새 판. 남아 있던 저장을 지우고 들어간다.</summary>
        private static void StartNewRun()
        {
            RunSaveSystem.DeleteSave();
            SceneManager.LoadScene(GameSceneName);
        }

        /// <summary>저장을 그대로 두고 들어간다. 복원은 게임 씬 쪽이 맡는다.</summary>
        private static void ContinueRun()
        {
            SceneManager.LoadScene(GameSceneName);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── 조각 만들기 ──────────────────────────────────────────────────

        private static Image CreateImage(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateLabel(Transform parent, string content, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = 26f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            Stretch((RectTransform)go.transform);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
