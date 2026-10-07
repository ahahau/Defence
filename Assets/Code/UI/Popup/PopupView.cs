using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Popup
{
    /// <summary>
    /// Popup.uxml에 값을 채우기만 하는 뷰. 대기열·일시정지·사운드는 <see cref="PopupController"/>가 맡는다.
    /// </summary>
    public sealed class PopupView
    {
        private readonly VisualElement _root;
        private readonly Label _title;
        private readonly Label _message;
        private readonly Label _cost;
        private readonly Label _warning;
        private readonly VisualElement _actions;

        public PopupView(VisualElement documentRoot)
        {
            _root = documentRoot.Q<VisualElement>(PopupUiNames.Root);
            _title = documentRoot.Q<Label>(PopupUiNames.Title);
            _message = documentRoot.Q<Label>(PopupUiNames.Message);
            _cost = documentRoot.Q<Label>(PopupUiNames.Cost);
            _warning = documentRoot.Q<Label>(PopupUiNames.Warning);
            _actions = documentRoot.Q<VisualElement>(PopupUiNames.Actions);
        }

        /// <summary>UXML에서 필요한 요소를 모두 찾았는가.</summary>
        public bool IsValid => _root != null && _title != null && _message != null
                               && _cost != null && _warning != null && _actions != null;

        /// <summary>요청을 그린다. 버튼을 누르면 그 버튼의 순서가 <paramref name="onButton"/>으로 전달된다.</summary>
        public void Show(PopupRequest request, Action<int> onButton)
        {
            _title.text = request.Title;
            _title.tooltip = request.Title;
            _message.text = request.Message;
            SetReservedText(_cost, request.CostText);
            SetReservedText(_warning, request.WarningText);

            _actions.Clear();
            for (int i = 0; i < request.Buttons.Count; i++)
            {
                PopupButtonSpec spec = request.Buttons[i];
                int index = i;
                var button = new Button(() => onButton(index)) { text = spec.Label, tooltip = spec.Label };
                button.AddToClassList(PopupUiNames.ButtonClass);
                button.AddToClassList(PopupUiNames.ButtonSizeClass);
                button.AddToClassList(ToStyleClass(spec.Style));
                button.SetEnabled(spec.Interactable);
                _actions.Add(button);
            }

            _root.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// 버튼 하나의 화면 좌표(픽셀, 왼쪽 아래 원점 — uGUI의 RectTransformUtility와 같은 계)를 구한다.
        /// 레이아웃이 아직 계산되지 않았으면(띄운 첫 프레임) false.
        /// </summary>
        public bool TryGetButtonScreenRect(int index, out Rect rect)
        {
            rect = default;
            if (_root == null || index < 0 || index >= _actions.childCount)
                return false;

            Rect panel = _root.panel.visualTree.worldBound;
            Rect bound = _actions[index].worldBound;
            if (panel.width <= 1f || panel.height <= 1f || float.IsNaN(bound.width) || bound.width <= 1f)
                return false;

            // 패널 좌표는 왼쪽 위 원점이고 화면 크기에 맞춰 늘어난다. 비율로 화면 픽셀에 옮긴다.
            float scaleX = Screen.width / panel.width;
            float scaleY = Screen.height / panel.height;
            rect = new Rect(bound.x * scaleX, Screen.height - bound.yMax * scaleY, bound.width * scaleX, bound.height * scaleY);
            return true;
        }

        public void Hide()
        {
            if (_root != null)
                _root.style.display = DisplayStyle.None;
        }

        // 조건부 요소는 display 대신 visibility로 숨겨 자리를 지킨다. 그래야 경고가 떠도 버튼 줄이 움직이지 않는다.
        private static void SetReservedText(Label label, string text)
        {
            bool visible = !string.IsNullOrEmpty(text);
            label.text = visible ? text : " ";
            label.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        private static string ToStyleClass(PopupButtonStyle style) => style switch
        {
            PopupButtonStyle.Primary => PopupUiNames.ButtonPrimaryClass,
            PopupButtonStyle.Danger => PopupUiNames.ButtonDangerClass,
            _ => PopupUiNames.ButtonSecondaryClass
        };
    }
}
