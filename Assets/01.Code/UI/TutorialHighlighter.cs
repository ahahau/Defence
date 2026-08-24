using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    /// <summary>
    /// 튜토리얼이 가리키는 버튼 하나에 강조 색을 입히고, 옮겨갈 때 원래 색으로 되돌린다.
    /// 어떤 버튼을 가리킬지는 화면 쪽에서 정하고, 여기서는 칠하고 되돌리는 일만 맡는다.
    /// </summary>
    public sealed class TutorialHighlighter
    {
        private static readonly Color HighlightColor = new(1f, 0.82f, 0.22f, 1f);

        private readonly Dictionary<Graphic, Color> _defaultColors = new();
        private Graphic _current;

        public bool IsActive { get; private set; }

        public void Activate() => IsActive = true;

        /// <summary>강조를 끄고 칠해 둔 색을 되돌린다.</summary>
        public void Deactivate()
        {
            IsActive = false;
            Restore();
        }

        /// <summary>버튼 하나를 강조한다. 이미 그 버튼이면 아무것도 하지 않는다.</summary>
        public void Highlight(Button button)
        {
            var graphic = button != null ? button.targetGraphic : null;
            if (graphic == null || _current == graphic)
                return;

            Restore();

            if (!_defaultColors.ContainsKey(graphic))
                _defaultColors[graphic] = graphic.color;

            graphic.color = HighlightColor;
            _current = graphic;
        }

        /// <summary>지금 강조 중인 버튼을 원래 색으로 되돌린다.</summary>
        public void Restore()
        {
            if (_current != null && _defaultColors.TryGetValue(_current, out var defaultColor))
                _current.color = defaultColor;

            _current = null;
        }
    }
}
