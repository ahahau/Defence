using Code.UI.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 부하 상태 창(uGUI, A안). 누른 자리 옆에 뜨는 고정 크기 카드. 무엇을 보여 줄지와 버튼이 할 일은
    /// <see cref="UnitStatusPanelView"/>가 정하고, 여기서는 그리고 버튼만 잇는다.
    /// 특성·성격 줄에 마우스를 올리면 설명을 띄운다. 버튼·안내는 조건에 따라 보이지 않아도 자리를 지킨다.
    /// </summary>
    public sealed class UnitStatusView : MonoBehaviour
    {
        private static readonly Vector2 ScreenOffset = new(24f, 24f);

        [SerializeField] private UnitStatusPanelView source;
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text state;
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text hp;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text combat;
        [SerializeField] private TMP_Text command;
        [SerializeField] private TMP_Text traits;
        [SerializeField] private UiHoverRelay traitsHover;
        [SerializeField] private TMP_Text fatigue;
        [SerializeField] private Image fatigueFill;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private UiFrameButton recoverButton;
        [SerializeField] private UiFrameButton recallButton;
        [SerializeField] private RectTransform tooltip;
        [SerializeField] private TMP_Text tooltipText;

        private string _traitsTooltip;

        /// <summary>피로 단계. 0 쾌조·1 피로(30)·2 과로(60)·3 탈진 임박(85).</summary>
        public static int FatigueLevel(float fatigue) => fatigue switch
        {
            >= 85f => 3,
            >= 60f => 2,
            >= 30f => 1,
            _ => 0
        };

        private void OnEnable()
        {
            closeButton.Button.onClick.AddListener(HandleClose);
            recoverButton.Button.onClick.AddListener(HandleRecover);
            recallButton.Button.onClick.AddListener(HandleRecall);
            traitsHover.Entered += ShowTooltip;
            traitsHover.Exited += HideTooltip;
            if (source != null)
            {
                source.Opened += Open;
                source.ModelChanged += Render;
                source.Closed += Close;
            }

            root.SetActive(source != null && source.IsOpen);
            HideTooltip();
        }

        private void OnDisable()
        {
            closeButton.Button.onClick.RemoveListener(HandleClose);
            recoverButton.Button.onClick.RemoveListener(HandleRecover);
            recallButton.Button.onClick.RemoveListener(HandleRecall);
            traitsHover.Entered -= ShowTooltip;
            traitsHover.Exited -= HideTooltip;
            if (source != null)
            {
                source.Opened -= Open;
                source.ModelChanged -= Render;
                source.Closed -= Close;
            }
        }

        private void Open(Vector2 screenPosition)
        {
            root.SetActive(true);
            Render(source.Model);
            UiPlacement.PlaceNear(card, screenPosition, ScreenOffset);
        }

        private void Close()
        {
            HideTooltip();
            root.SetActive(false);
        }

        private void Render(UnitStatusViewModel model)
        {
            SetText(title, model.Title);
            SetText(state, model.State);
            SetText(hp, model.Hp);
            SetText(combat, model.Combat);
            SetText(command, model.Command);
            SetText(traits, model.Traits);
            _traitsTooltip = model.TraitsTooltip;
            SetText(fatigue, model.Fatigue);
            SetText(hint, model.Hint);

            hpFill.fillAmount = Mathf.Clamp01(model.HpRatio);
            fatigueFill.fillAmount = Mathf.Clamp01(model.FatigueRatio);
            fatigueFill.color = FatigueLevel(model.FatigueRatio * 100f) switch
            {
                3 => HudPalette.Danger,
                2 => HudPalette.Loss,
                1 => HudPalette.Warn,
                _ => HudPalette.Gain
            };

            if (portrait.sprite != model.Portrait)
                portrait.sprite = model.Portrait;
            portrait.enabled = model.Portrait != null;

            Apply(recoverButton, model.ShowRecover, model.RecoverEnabled, model.RecoverLabel);
            Apply(recallButton, model.ShowRecall, model.RecallEnabled, model.RecallLabel);
        }

        private void ShowTooltip()
        {
            if (string.IsNullOrWhiteSpace(_traitsTooltip))
                return;

            tooltipText.text = _traitsTooltip;
            // 특성 줄 바로 아래에 띄운다.
            if (UiScreenRect.TryGet(traits.rectTransform, out var rect))
                UiPlacement.PlaceNear(tooltip, new Vector2(rect.xMin, rect.yMin - 4f), new Vector2(0f, -tooltip.rect.height));
            tooltip.gameObject.SetActive(true);
        }

        private void HideTooltip()
        {
            if (tooltip != null)
                tooltip.gameObject.SetActive(false);
        }

        private void HandleClose() => source?.HidePanel();
        private void HandleRecover() => source?.RequestRecover();
        private void HandleRecall() => source?.RequestRecall();

        private static void Apply(UiFrameButton button, bool show, bool enabled, string label)
        {
            if (button.Group != null)
            {
                button.Group.alpha = show ? 1f : 0f;
                button.Group.blocksRaycasts = show;
            }

            button.SetInteractable(show && enabled);
            if (label != null)
                button.SetLabel(label);
        }

        private static void SetText(TMP_Text label, string value)
        {
            value ??= string.Empty;
            if (label != null && label.text != value)
                label.text = value;
        }
    }
}
