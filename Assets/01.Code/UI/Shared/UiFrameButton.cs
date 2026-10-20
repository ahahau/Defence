using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 테두리 있는 버튼 하나. 평소 테두리 색을 기억해 두고, 고름·강조 상태를 금색 테두리로 보인다.
    /// 누름·올림·잠김 색은 <see cref="UnityEngine.UI.Button"/> 전환이 테두리 위에 곱한다.
    /// 화면 코드는 <see cref="Button"/>으로 클릭을 받고 <see cref="SetSelected"/>로 상태만 바꾼다.
    ///
    /// 글자 하나뿐인 버튼은 픽셀 UI 팩의 버튼 그림(면)을 색 그대로 쓴다(<see cref="UsesFace"/>): 보통 회색·주요 살구색·위험 붉은색,
    /// 고르면 살구색(주요 버튼은 초록) 면으로 바뀐다. 면이 밝아 글자는 어두운 색으로 바꿔 그린다(<see cref="SetLabelColor"/>).
    /// </summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class UiFrameButton : MonoBehaviour
    {
        /// <summary>팩 버튼 면의 역할. <see cref="None"/>이면 예전처럼 칠한 바탕 + 테두리다.</summary>
        public enum FaceRole
        {
            None,
            Normal,
            Primary,
            Danger
        }

        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private UnityEngine.UI.Image outline;
        [SerializeField] private Color normalBorder = Color.white;
        [SerializeField] private Color selectedBorder = new Color32(242, 194, 90, 255);
        [SerializeField] private TMPro.TMP_Text label;
        [SerializeField, Tooltip("자리를 지킨 채 숨길 때 쓰는 투명도(선택). 비우면 숨김은 꺼짐으로 한다.")]
        private CanvasGroup group;

        [Header("Pack Face")]
        [SerializeField] private FaceRole faceRole = FaceRole.None;
        [SerializeField] private Sprite normalFace;
        [SerializeField] private Sprite primaryFace;
        [SerializeField] private Sprite dangerFace;
        [SerializeField] private Sprite highlightFace;
        [SerializeField, Tooltip("밝은 면 위 글자 색.")]
        private Color faceText = new Color32(40, 34, 52, 255);

        private bool _selected;

        public UnityEngine.UI.Button Button => button;
        public TMPro.TMP_Text Label => label;
        public CanvasGroup Group => group;

        /// <summary>팩 버튼 면을 쓰는가(밝은 면 + 어두운 글자).</summary>
        public bool UsesFace => faceRole != FaceRole.None && normalFace != null;

        /// <summary>골라진 상태(예: 지금 배속)나 튜토리얼 강조를 금색 테두리(팩 버튼은 살구색 면)로 보인다.</summary>
        public void SetSelected(bool selected)
        {
            if (UsesFace)
            {
                _selected = selected;
                ApplyFace();
                return;
            }

            if (_selected == selected && outline != null && outline.color == (selected ? selectedBorder : normalBorder))
                return;

            _selected = selected;
            if (outline != null)
                outline.color = selected ? selectedBorder : normalBorder;
        }

        /// <summary>팩 버튼 면의 역할을 바꾼다(팝업처럼 요청마다 역할이 다를 때). 팩 면이 없는 버튼에서는 아무 일도 없다.</summary>
        public void SetFace(FaceRole role)
        {
            if (!UsesFace || role == FaceRole.None)
                return;

            faceRole = role;
            ApplyFace();
        }

        /// <summary>
        /// 글자 색. 팩 버튼 면 위에서는 화면이 준 밝은 색을 어두운 글자색 쪽으로 당겨, 색의 뜻(강조·흐림)은 조금 남기고 읽히게 한다.
        /// </summary>
        public void SetLabelColor(Color color)
        {
            if (label == null)
                return;

            label.color = UsesFace ? FaceLabelColor(color) : color;
        }

        private Color FaceLabelColor(Color color)
        {
            var mixed = Color.Lerp(color, faceText, 0.85f);
            mixed.a = color.a;
            return mixed;
        }

        private void ApplyFace()
        {
            if (button == null || button.targetGraphic is not UnityEngine.UI.Image face)
                return;

            var sprite = faceRole switch
            {
                FaceRole.Primary => primaryFace,
                FaceRole.Danger => dangerFace,
                _ => normalFace
            };
            if (_selected)
                sprite = faceRole == FaceRole.Primary ? highlightFace : primaryFace;
            if (sprite != null && face.sprite != sprite)
                face.sprite = sprite;
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
            // 팩 버튼 면은 잠기면 면이 회색으로 어두워지므로 글자를 덜 흐려야 읽힌다.
            if (label != null)
                label.alpha = interactable ? 1f : UsesFace ? 0.7f : 0.45f;
        }

        /// <summary>바탕·테두리·글자 색을 바꾼다(팝업처럼 요청마다 버튼 역할이 다를 때).</summary>
        /// <remarks>팩 버튼 면은 칠하지 않는다(면 그림이 색을 가진다). 역할은 <see cref="SetFace"/>로 바꾼다.</remarks>
        public void SetStyle(Color fill, Color border, Color text, bool bold)
        {
            if (button != null && button.targetGraphic != null && !UsesFace)
                button.targetGraphic.color = fill;

            normalBorder = border;
            if (outline != null && !_selected)
                outline.color = border;

            if (label != null)
            {
                SetLabelColor(text);
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
