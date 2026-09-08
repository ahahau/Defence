using _01.Code.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Editor
{
    /// <summary>
    /// 타이틀 화면을 씬에 실제 오브젝트로 세운다.
    ///
    /// 처음에는 실행 시점에 코드로 세웠다. 씬 파일이 다른 작업과 겹치는 걸 피하려던 것인데,
    /// 정작 에디터에서 아무것도 안 보여서 열어 보고 고칠 수가 없었다. 그래서 한 번 돌려
    /// 씬에 박아 넣고, 그다음부터는 인스펙터에서 손보는 쪽으로 바꾼다.
    ///
    /// 여러 번 돌려도 괜찮다 — 만들기 전에 같은 이름의 옛 것을 지운다.
    /// </summary>
    public static class TitleScreenBuilder
    {
        private const string ScenePath = "Assets/00.Scenes/Start.unity";
        private const string RootName = "Title Screen";
        private const string NodeSpritePath = "Assets/05.Graphs/Node/Node.png";

        private static readonly Color Ground = new(0.035f, 0.027f, 0.024f, 1f);
        private static readonly Color Corridor = new(0.34f, 0.24f, 0.15f, 1f);
        private static readonly Color RoomTint = new(0.78f, 0.72f, 0.66f, 1f);
        private static readonly Color CoreTint = new(1f, 0.92f, 0.74f, 1f);
        private static readonly Color ButtonText = new(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color ButtonTextOff = new(0.46f, 0.42f, 0.37f, 1f);

        /// <summary>노드 자리(화면 비율). 마지막이 가운데 큰 방이고 버튼이 그 위에 앉는다.</summary>
        private static readonly Vector2[] Spots =
        {
            new(-0.355f, 0.255f), new(-0.235f, -0.10f), new(-0.395f, -0.30f),
            new(0.245f, 0.275f), new(0.395f, -0.02f), new(0.215f, -0.315f),
            new(0f, 0f),
        };

        private static readonly (int, int)[] Corridors =
        {
            (0, 6), (1, 6), (3, 6), (4, 6),
            (0, 1), (1, 2), (3, 4), (4, 5),
        };

        [MenuItem("Tools/Defence/타이틀 화면 씬에 세우기", priority = 200)]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 예전 것을 먼저 걷는다. 두 번 돌리면 두 겹으로 쌓인다.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == RootName)
                    Object.DestroyImmediate(root);

            // 씬에 남아 있던 옛 메뉴는 끈다. 지우지 않는 건 되돌릴 여지를 두기 위해서다.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "StartCanvas")
                    root.SetActive(false);

            EnsureEventSystem(scene);

            var canvas = CreateCanvas();
            var skin = AssetDatabase.LoadAssetAtPath<UiSkinSO>("Assets/Resources/UI/UiSkin.asset");
            var nodeSprite = LoadNodeSprite();

            BuildBackdrop(canvas.transform, nodeSprite);
            BuildMenu(canvas.transform, skin);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[타이틀] 씬에 세우고 저장했습니다: " + ScenePath);
        }

        /// <summary>Multiple 모드라 경로만으로는 못 집는다. 서브 에셋에서 스프라이트를 골라낸다.</summary>
        private static Sprite LoadNodeSprite()
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(NodeSpritePath))
                if (asset is Sprite sprite)
                    return sprite;

            Debug.LogWarning("[타이틀] 노드 그림을 찾지 못했습니다: " + NodeSpritePath);
            return null;
        }

        private static void EnsureEventSystem(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<UnityEngine.EventSystems.EventSystem>(true) != null)
                    return;

            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 설정 창(5000)보다 아래. 타이틀에서도 설정을 열 수 있어야 한다.
            canvas.sortingOrder = 100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        // ── 배경 ────────────────────────────────────────────────────────

        private static void BuildBackdrop(Transform parent, Sprite nodeSprite)
        {
            var ground = CreateImage(parent, "Ground", Ground);
            Stretch(ground.rectTransform);

            var layer = new GameObject("Node Graph", typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            Stretch((RectTransform)layer.transform);

            var points = new Vector2[Spots.Length];
            for (var i = 0; i < Spots.Length; i++)
                points[i] = new Vector2(Spots[i].x * 1920f, Spots[i].y * 1080f);

            // 통로를 먼저 깔아야 방이 그 위에 얹힌다.
            foreach (var (from, to) in Corridors)
                CreateCorridor(layer.transform, points[from], points[to]);

            for (var i = 0; i < points.Length; i++)
            {
                var isCore = i == points.Length - 1;
                CreateRoom(layer.transform, points[i], isCore ? 470f : 210f, isCore, nodeSprite);
            }
        }

        private static void CreateCorridor(Transform parent, Vector2 from, Vector2 to)
        {
            var image = CreateImage(parent, "Corridor", Corridor);
            var rect = image.rectTransform;
            Center(rect);

            var delta = to - from;
            rect.sizeDelta = new Vector2(delta.magnitude, 9f);
            rect.anchoredPosition = from + delta * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <summary>방 하나. 게임이 노드에 쓰는 그림을 그대로 쓴다 — 첫 화면이 곧 판이 된다.</summary>
        private static void CreateRoom(Transform parent, Vector2 position, float size, bool isCore, Sprite sprite)
        {
            var image = CreateImage(parent, isCore ? "Core Room" : "Room", isCore ? CoreTint : RoomTint, sprite);
            var rect = image.rectTransform;
            Center(rect);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;
            image.preserveAspect = true;
        }

        // ── 버튼 넷 ─────────────────────────────────────────────────────

        private static void BuildMenu(Transform parent, UiSkinSO skin)
        {
            var column = new GameObject("Menu", typeof(RectTransform));
            column.transform.SetParent(parent, false);
            var rect = (RectTransform)column.transform;
            Center(rect);
            rect.sizeDelta = new Vector2(320f, 300f);

            CreateMenuButton(column.transform, skin, "새로하기", 105f, "NewRun");
            CreateMenuButton(column.transform, skin, "이어하기", 35f, "Continue");
            CreateMenuButton(column.transform, skin, "설정", -35f, "Settings");
            CreateMenuButton(column.transform, skin, "나가기", -105f, "Quit");
        }

        private static void CreateMenuButton(Transform parent, UiSkinSO skin, string label, float y, string action)
        {
            var frame = skin != null ? skin.ButtonFrame : null;
            var image = CreateImage(parent, "Button " + action,
                frame != null ? Color.white : new Color(0.16f, 0.12f, 0.08f, 1f), frame);
            var rect = image.rectTransform;
            Center(rect);
            rect.sizeDelta = new Vector2(300f, 58f);
            rect.anchoredPosition = new Vector2(0f, y);
            image.type = frame != null && frame.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;

            // 배경 조각은 레이캐스트를 꺼서 만든다. 버튼 바탕만은 켜야 클릭이 닿는다.
            image.raycastTarget = true;

            var text = CreateLabel(image.transform, label, ButtonText);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            // 눌렀을 때 무엇을 할지는 TitleMenuActions 가 안다. 씬에 배선해 두면
            // 인스펙터에서 무엇이 걸려 있는지 보이고 바꿀 수도 있다.
            var actions = EnsureActions(parent);
            switch (action)
            {
                case "NewRun":
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, actions.StartNewRun);
                    break;
                case "Continue":
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, actions.ContinueRun);
                    // 저장이 없으면 잠긴다. 실제 상태는 실행할 때 TitleMenuActions 가 다시 본다.
                    text.color = ButtonTextOff;
                    break;
                case "Settings":
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, actions.OpenSettings);
                    break;
                case "Quit":
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, actions.QuitGame);
                    break;
            }

            if (action == "Continue")
                actions.RegisterContinueButton(button, text);
        }

        private static TitleMenuActions EnsureActions(Transform menu)
        {
            var root = menu.parent != null ? menu.parent.gameObject : menu.gameObject;
            var found = root.GetComponent<TitleMenuActions>();
            return found != null ? found : root.AddComponent<TitleMenuActions>();
        }

        // ── 조각 ────────────────────────────────────────────────────────

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

        private static void Center(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
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
