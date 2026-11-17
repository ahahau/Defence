using System;

namespace Code.UI.Popup
{
    /// <summary>팝업 버튼 한 개. 누르면 팝업이 닫힌 뒤 <see cref="OnClick"/>이 불린다.</summary>
    public readonly struct PopupButtonSpec
    {
        public PopupButtonSpec(string label, PopupButtonStyle style, Action onClick, bool interactable = true)
        {
            Label = label ?? string.Empty;
            Style = style;
            OnClick = onClick;
            Interactable = interactable;
        }

        public string Label { get; }
        public PopupButtonStyle Style { get; }
        public Action OnClick { get; }

        /// <summary>false면 보이되 누를 수 없다(예: 금화 부족).</summary>
        public bool Interactable { get; }
    }
}
