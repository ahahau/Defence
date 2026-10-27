using TMPro;
using UnityEngine;

namespace Code.UI.Popup
{
    /// <summary>
    /// 범용 팝업 화면(uGUI, A안). <see cref="PopupController"/>가 들고 있는 요청을 그리고, 버튼을 누르면
    /// <see cref="PopupController.Choose"/>로 돌려준다. 대기열·시간 정지·ESC는 컨트롤러가 맡는다.
    /// 뒤 덮개가 화면 전체 클릭을 막는다. 비용 줄·경고 줄은 비어도 자리를 지킨다(ui-rules 5번).
    /// </summary>
    public sealed class PopupView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text cost;
        [SerializeField] private TMP_Text warning;
        [SerializeField] private UiFrameButton[] buttons;
        [SerializeField, Tooltip("버튼 사이 간격 포함 한 칸 폭")] private float buttonStride = 168f;
        [SerializeField] private float buttonRightMargin = 26f;

        private readonly Vector3[] _corners = new Vector3[4];
        private UnityEngine.Events.UnityAction[] _handlers;

        private void EnsureRuntimeState()
        {
            if (_handlers != null)
                return;

            _handlers = new UnityEngine.Events.UnityAction[buttons != null ? buttons.Length : 0];
            for (var i = 0; i < _handlers.Length; i++)
            {
                var index = i;
                _handlers[i] = () => PopupController.Choose(index);
            }
        }

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
            for (var i = 0; i < _handlers.Length; i++)
                if (buttons[i] != null && buttons[i].Button != null)
                    buttons[i].Button.onClick.AddListener(_handlers[i]);

            PopupController.Changed += Render;
            PopupController.SetConfirmButtonRectSource(TryGetConfirmButtonScreenRect);
            Render();
        }

        private void OnDisable()
        {
            for (var i = 0; _handlers != null && i < _handlers.Length; i++)
                if (buttons[i] != null && buttons[i].Button != null)
                    buttons[i].Button.onClick.RemoveListener(_handlers[i]);

            PopupController.Changed -= Render;
            PopupController.SetConfirmButtonRectSource(null);
        }

        private void Render()
        {
            var request = PopupController.Current;
            if (root != null)
                root.SetActive(request != null);
            if (request == null)
                return;

            SetText(title, request.Title);
            SetText(message, request.Message);
            SetText(cost, request.CostText);
            SetText(warning, request.WarningText);

            for (var i = 0; i < buttons.Length; i++)
            {
                var slot = buttons[i];
                if (slot == null)
                    continue;

                var used = i < request.Buttons.Count;
                slot.SetVisible(used);
                if (!used)
                    continue;

                // 요청의 버튼 순서(취소, 확인)가 왼쪽→오른쪽이고, 묶음 전체는 오른쪽에 붙인다.
                var rt = (RectTransform)slot.transform;
                rt.anchoredPosition = new Vector2(-buttonRightMargin - (request.Buttons.Count - 1 - i) * buttonStride, rt.anchoredPosition.y);

                var spec = request.Buttons[i];
                slot.SetLabel(spec.Label);
                // 잠긴 버튼도 누를 수 있게 둔다. 누르면 컨트롤러가 실패음을 낸다(조용히 막히면 고장으로 읽힌다).
                slot.SetInteractable(true);
                ApplyStyle(slot, spec.Style, spec.Interactable);
            }
        }

        private static void ApplyStyle(UiFrameButton slot, PopupButtonStyle style, bool interactable)
        {
            slot.SetFace(style switch
            {
                PopupButtonStyle.Primary when interactable => UiFrameButton.FaceRole.Primary,
                PopupButtonStyle.Danger => UiFrameButton.FaceRole.Danger,
                _ => UiFrameButton.FaceRole.Normal
            });
            switch (style)
            {
                case PopupButtonStyle.Primary:
                    slot.SetStyle(interactable ? HudPalette.Primary : HudPalette.Control,
                        interactable ? HudPalette.BorderAccent : HudPalette.BorderQuiet,
                        interactable ? HudPalette.Text : HudPalette.TextMuted, true);
                    break;
                case PopupButtonStyle.Danger:
                    slot.SetStyle(HudPalette.DangerSurface, HudPalette.BorderDanger, HudPalette.Loss, true);
                    break;
                default:
                    slot.SetStyle(HudPalette.Control, HudPalette.BorderQuiet, HudPalette.TextBody, false);
                    break;
            }
        }

        private bool TryGetConfirmButtonScreenRect(out Rect rect)
        {
            rect = default;
            var request = PopupController.Current;
            if (request == null || request.ConfirmButtonIndex < 0 || request.ConfirmButtonIndex >= buttons.Length)
                return false;

            var slot = buttons[request.ConfirmButtonIndex];
            if (slot == null || !slot.gameObject.activeInHierarchy)
                return false;

            ((RectTransform)slot.transform).GetWorldCorners(_corners);
            rect = Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
            return rect.width > 0f && rect.height > 0f;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != (value ?? string.Empty))
                label.text = value ?? string.Empty;
        }
    }
}
