using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 테두리 있는 버튼 하나. 평소 테두리 색을 기억해 두고, 고름·강조 상태를 금색 테두리로 보인다.
    /// 누름·올림·잠김 색은 <see cref="UnityEngine.UI.Button"/> 전환이 테두리 위에 곱한다.
    /// 화면 코드는 <see cref="Button"/>으로 클릭을 받고 <see cref="SetSelected"/>로 상태만 바꾼다.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class UiFrameButton : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private UnityEngine.UI.Image outline;
        [SerializeField] private Color normalBorder = Color.white;
        [SerializeField] private Color selectedBorder = new Color32(242, 194, 90, 255);
        [SerializeField] private TMPro.TMP_Text label;
        [SerializeField, Tooltip("자리를 지킨 채 숨길 때 쓰는 투명도(선택). 비우면 숨김은 꺼짐으로 한다.")]
        private CanvasGroup group;

        private bool _selected;

        public UnityEngine.UI.Button Button => button;
        public TMPro.TMP_Text Label => label;
        public CanvasGroup Group => group;

        /// <summary>골라진 상태(예: 지금 배속)나 튜토리얼 강조를 금색 테두리로 보인다.</summary>
        public void SetSelected(bool selected)
        {
            if (_selected == selected && outline != null && outline.color == (selected ? selectedBorder : normalBorder))
                return;

            _selected = selected;
            if (outline != null)
                outline.color = selected ? selectedBorder : normalBorder;
        }

        public void SetLabel(string text)
        {
            if (label != null && label.text != text)
                label.text = text ?? string.Empty;
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null && button.interactable != interactable)
                button.interactable = interactable;
            // 바탕 색 전환만으로는 잠긴 금색 버튼이 잘 구분되지 않아 글자도 흐린다.
            if (label != null)
                label.alpha = interactable ? 1f : 0.45f;
        }

        /// <summary>바탕·테두리·글자 색을 바꾼다(팝업처럼 요청마다 버튼 역할이 다를 때).</summary>
        public void SetStyle(Color fill, Color border, Color text, bool bold)
        {
            if (button != null && button.targetGraphic != null)
                button.targetGraphic.color = fill;

            normalBorder = border;
            if (outline != null && !_selected)
                outline.color = border;

            if (label != null)
            {
                label.color = text;
                label.fontStyle = bold ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            }
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }
    }
}
