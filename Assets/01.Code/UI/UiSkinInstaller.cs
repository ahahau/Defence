using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 그림이 안 들어간 패널과 버튼에 UI 팩의 창틀을 입힌다.
    ///
    /// 프로젝트에 Action_RPG_GUI 팩이 들어와 있고 <see cref="UiSkinSO"/>에 창틀·버튼틀이
    /// 배선까지 돼 있는데, 정작 쓰는 곳이 설정 창 하나뿐이었다. 나머지 스무 남짓한 패널은
    /// 색만 칠한 사각형으로 나온다.
    ///
    /// 프리팹을 스무 개 고치는 대신 실행 시점에 입힌다 — 씬과 프리팹은 다른 작업과 함께
    /// 건드리는 파일이라 충돌하기 쉽고, 이 일은 게임 상태에 전혀 의존하지 않는다.
    /// <c>UiSfxInstaller</c>가 소리를 붙이는 것과 같은 방식이다.
    ///
    /// 이미 그림이 있는 것은 건드리지 않는다. 아이콘·초상화·체력바가 덮이면 안 된다.
    /// </summary>
    public sealed class UiSkinInstaller : MonoBehaviour
    {
        private const string SkinResourcePath = "UI/UiSkin";
        private const float ScanInterval = 1.0f;

        /// <summary>이 이름이 들어간 것만 창틀을 입힌다. 규칙에 기대는 방식이라 놓쳐도 조용할 뿐이다.</summary>
        private static readonly string[] PanelNameHints = { "Panel", "Window", "Popup", "Dialog" };

        /// <summary>덮으면 안 되는 것들. 이름에 이게 들어가면 넘어간다.</summary>
        private static readonly string[] SkipNameHints =
        {
            "Icon", "Portrait", "Bar", "Fill", "Mask", "Viewport", "Content",
            "Background", "Overlay", "Shadow", "Divider", "Rule", "Band",
        };

        private readonly HashSet<Graphic> seen = new();
        private UiSkinSO skin;
        private float nextScanTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("UI Skin Installer");
            host.AddComponent<UiSkinInstaller>();
            DontDestroyOnLoad(host);
        }

        private void Awake()
        {
            skin = Resources.Load<UiSkinSO>(SkinResourcePath);
            if (skin == null)
                Debug.LogWarning($"UI 표를 찾지 못했습니다: Resources/{SkinResourcePath}. 패널이 민무늬로 나옵니다.");
        }

        private void Update()
        {
            if (skin == null || Time.unscaledTime < nextScanTime)
                return;

            nextScanTime = Time.unscaledTime + ScanInterval;
            Scan();
        }

        private void Scan()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            foreach (var image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (!seen.Add(image))
                    continue;

                // 이미 자기 그림이 있으면 그대로 둔다.
                if (image.sprite != null)
                    continue;

                // 오른쪽 위 상태 카드는 건드리지 않는다. 그쪽은 DungeonHudStyle 이 일부러 그림을
                // 비워 두는 자리다 — 가로로 긴 띠라 창틀을 늘려 넣으면 글자 뒤로 조각이 남는다.
                if (IsTopRightCard(image))
                    continue;

                // 화면을 통째로 덮는 것은 창이 아니라 뒤를 가리는 막이다. 창틀을 늘려 씌우면
                // 모서리 장식이 화면 네 귀퉁이에 붙는다.
                if (CoversWholeScreen(image))
                    continue;

                if (image.TryGetComponent<Button>(out _))
                {
                    Apply(image, skin.ButtonFrame);
                    continue;
                }

                if (LooksLikePanel(image.gameObject.name))
                    Apply(image, skin.WindowFrame);
            }

            // 파괴된 것이 계속 쌓이지 않게 가끔 비운다. 다시 훑으면 살아 있는 것만 도로 채워진다.
            if (seen.Count > 1024)
                seen.Clear();
        }

        /// <summary>캔버스를 거의 다 덮으면 창이 아니라 가림막으로 본다.</summary>
        private static bool CoversWholeScreen(Graphic graphic)
        {
            var canvas = graphic.canvas;
            if (canvas == null || canvas.transform is not RectTransform canvasRect)
                return false;

            var size = graphic.rectTransform.rect.size;
            var full = canvasRect.rect.size;
            if (full.x <= 0f || full.y <= 0f)
                return false;

            return size.x >= full.x * 0.85f && size.y >= full.y * 0.85f;
        }

        /// <summary>
        /// 오른쪽 위 상태 카드인지 본다. 이름이 아니라 자리로 가린다 —
        /// DungeonHudStyle.ApplyTopRightCard 가 우상단 고정에 350x60 으로 못박는다.
        /// </summary>
        private static bool IsTopRightCard(Graphic graphic)
        {
            if (graphic.rectTransform is not { } rect)
                return false;

            return rect.anchorMin == Vector2.one
                   && rect.anchorMax == Vector2.one
                   && rect.pivot == Vector2.one
                   && Mathf.Approximately(rect.sizeDelta.x, 350f)
                   && Mathf.Approximately(rect.sizeDelta.y, 60f);
        }

        private static bool LooksLikePanel(string name)
        {
            foreach (var skip in SkipNameHints)
                if (name.Contains(skip, System.StringComparison.OrdinalIgnoreCase))
                    return false;

            foreach (var hint in PanelNameHints)
                if (name.Contains(hint, System.StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private static void Apply(Image image, Sprite frame)
        {
            if (frame == null)
                return;

            image.sprite = frame;

            // 테두리가 잡힌 그림만 늘려 쓸 수 있다. 없는 것을 Sliced 로 두면 유니티가 경고를 뱉고
            // 그림이 뭉개진다.
            image.type = frame.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;

            // 색은 흰색으로 돌린다. 민무늬 시절 칠해 둔 색이 남아 있으면 창틀 위에 겹쳐 물든다.
            // 투명도는 건드리지 않는다 — 반투명으로 띄우던 창이 있다.
            var color = image.color;
            image.color = new Color(1f, 1f, 1f, color.a);
        }
    }
}
