using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// 첫날 안내 덮개(UI Toolkit). 어느 칸인지·어디를 뚫을지는 <c>PlayTutorialView</c>가 정하고,
    /// 이 화면은 네 조각 어둠과 안내 카드를 그리고 건너뛰기 입력만 올려 보낸다.
    ///
    /// 팝업 패널의 팝업(900)보다 낮은 순서로 뜬다. uGUI와 다른 Toolkit 화면은 덮어 구멍으로만 눌리고,
    /// 방 확장 팝업은 덮개 위에 남아 눌린다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class TutorialOverlayToolkitView : MonoBehaviour
    {
        public const string ScreenResourcePath = "UI/TutorialOverlayScreen";
        public const string RootName = "TutorialOverlayToolkitUi";

        private const string ShownClass = "is-shown";

        private VisualElement _root;
        private VisualElement _dims;
        private readonly VisualElement[] _dimPieces = new VisualElement[4];
        private VisualElement _hint;
        private Label _header;
        private Label _body;
        private Button _skip;

        public event Action SkipClicked;

        public void SetHint(string header, string body)
        {
            if (_header == null)
                return;

            _header.text = header ?? string.Empty;
            _body.text = body ?? string.Empty;
        }

        public void SetHintVisible(bool visible) => _hint?.EnableInClassList(ShownClass, visible);

        public void SetDimVisible(bool visible) => _dims?.EnableInClassList(ShownClass, visible);

        /// <summary>화면 사각형(왼쪽 아래 원점 픽셀) 둘레에 여유를 두고 그 바깥을 덮는다.</summary>
        public void SetHole(Rect screenRect, float padding)
        {
            if (_root == null || _root.panel == null)
                return;

            var panel = _root.panel.visualTree.worldBound;
            if (panel.width <= 1f || panel.height <= 1f)
                return;

            var scaleX = panel.width / Screen.width;
            var scaleY = panel.height / Screen.height;
            var left = Mathf.Clamp((screenRect.xMin - padding) * scaleX, 0f, panel.width);
            var right = Mathf.Clamp((screenRect.xMax + padding) * scaleX, 0f, panel.width);
            var top = Mathf.Clamp((Screen.height - screenRect.yMax - padding) * scaleY, 0f, panel.height);
            var bottom = Mathf.Clamp((Screen.height - screenRect.yMin + padding) * scaleY, 0f, panel.height);

            Place(_dimPieces[0], 0f, 0f, panel.width, top);
            Place(_dimPieces[1], 0f, bottom, panel.width, panel.height - bottom);
            Place(_dimPieces[2], 0f, top, left, bottom - top);
            Place(_dimPieces[3], right, top, panel.width - right, bottom - top);
            SetDimVisible(true);
        }

        private void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("tutorial-root");
            if (_root == null)
                return;

            _dims = _root.Q<VisualElement>("tutorial-dims");
            _dimPieces[0] = _root.Q<VisualElement>("tutorial-dim-top");
            _dimPieces[1] = _root.Q<VisualElement>("tutorial-dim-bottom");
            _dimPieces[2] = _root.Q<VisualElement>("tutorial-dim-left");
            _dimPieces[3] = _root.Q<VisualElement>("tutorial-dim-right");
            _hint = _root.Q<VisualElement>("tutorial-hint");
            _header = _root.Q<Label>("tutorial-hint-header");
            _body = _root.Q<Label>("tutorial-hint-body");
            _skip = _root.Q<Button>("tutorial-skip");

            _skip.clicked += HandleSkip;
            // 지도 입력은 uGUI만 걸러서, 등록하지 않으면 어둠 아래 방이 눌린다. 보일 때만 막는다.
            ToolkitPointerBlockers.Register(_hint);
            foreach (var piece in _dimPieces)
                ToolkitPointerBlockers.Register(piece);
        }

        private void OnDisable()
        {
            if (_root == null)
                return;

            _skip.clicked -= HandleSkip;
            ToolkitPointerBlockers.Unregister(_hint);
            foreach (var piece in _dimPieces)
                ToolkitPointerBlockers.Unregister(piece);
        }

        private void HandleSkip() => SkipClicked?.Invoke();

        private static void Place(VisualElement element, float x, float y, float width, float height)
        {
            element.style.left = x;
            element.style.top = y;
            element.style.width = Mathf.Max(0f, width);
            element.style.height = Mathf.Max(0f, height);
        }
    }
}
