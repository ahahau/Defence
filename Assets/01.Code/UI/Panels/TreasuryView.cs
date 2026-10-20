using Code.Buildings;
using Code.Manager;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    /// <summary>
    /// 금고 관리 창(uGUI, A안, 방 상세 왼쪽). 금고 칸을 누르면 뜨고(<see cref="NodePanelView.TreasuryInspectRequested"/>),
    /// 맡기기·꺼내기와 이자·약탈 위험을 보여 준다. 금화 이동은 <see cref="Treasury"/>의 공개 함수가 한다.
    /// </summary>
    public sealed class TreasuryView : MonoBehaviour
    {
        private const int QuickAmount = 25;
        private const float RefreshInterval = 0.25f;

        [SerializeField] private NodePanelView panel;
        [SerializeField] private CostManager costManager;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private GameObject root;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text storage;
        [SerializeField] private TMP_Text interest;
        [SerializeField] private TMP_Text risk;
        [SerializeField] private TMP_Text funds;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private UiFrameButton closeButton;
        [SerializeField] private UiFrameButton depositButton;
        [SerializeField] private UiFrameButton depositAllButton;
        [SerializeField] private UiFrameButton withdrawButton;
        [SerializeField] private UiFrameButton withdrawAllButton;

        private readonly TreasuryPresenter _presenter = new();
        private Treasury _target;
        private float _refreshTimer;

        private void OnEnable()
        {
            closeButton.Button.onClick.AddListener(Hide);
            depositButton.Button.onClick.AddListener(DepositQuick);
            depositAllButton.Button.onClick.AddListener(DepositAll);
            withdrawButton.Button.onClick.AddListener(WithdrawQuick);
            withdrawAllButton.Button.onClick.AddListener(WithdrawAll);
            if (panel != null)
            {
                panel.TreasuryInspectRequested += Show;
                panel.TreasuryInspectClosed += Hide;
            }

            var sprite = DungeonHudIcon.Skin != null ? DungeonHudIcon.Skin.TreasuryIcon : null;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            root.SetActive(false);
        }

        private void OnDisable()
        {
            closeButton.Button.onClick.RemoveListener(Hide);
            depositButton.Button.onClick.RemoveListener(DepositQuick);
            depositAllButton.Button.onClick.RemoveListener(DepositAll);
            withdrawButton.Button.onClick.RemoveListener(WithdrawQuick);
            withdrawAllButton.Button.onClick.RemoveListener(WithdrawAll);
            if (panel != null)
            {
                panel.TreasuryInspectRequested -= Show;
                panel.TreasuryInspectClosed -= Hide;
            }
        }

        // 이자·운영 자금은 영업 중에도 바뀐다. 매 프레임 글자를 새로 만들지 않게 간격을 둔다.
        private void Update()
        {
            if (_target == null)
                return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f)
                return;

            _refreshTimer = RefreshInterval;
            Refresh();
        }

        private void Show(Treasury treasury)
        {
            if (treasury == null)
                return;

            _target = treasury;
            root.SetActive(true);
            Refresh();
        }

        private void Hide()
        {
            _target = null;
            root.SetActive(false);
        }

        private void DepositQuick()
        {
            _target?.DepositFromOperatingFunds(QuickAmount);
            Refresh();
        }

        private void DepositAll()
        {
            _target?.DepositFromOperatingFunds(OperatingFunds);
            Refresh();
        }

        private void WithdrawQuick()
        {
            _target?.WithdrawToOperatingFunds(QuickAmount);
            Refresh();
        }

        private void WithdrawAll()
        {
            _target?.WithdrawToOperatingFunds(_target != null ? _target.StoredGold : 0);
            Refresh();
        }

        private int OperatingFunds => costManager != null ? costManager.CurrentGold : 0;

        private void Refresh()
        {
            if (_target == null || _target.IsDestroyed)
            {
                Hide();
                return;
            }

            var days = dayManager != null ? dayManager.DaysUntilSettlement : DayManager.WeekLength;
            var model = _presenter.Build(_target, OperatingFunds, days);
            storage.text = model.Storage;
            funds.text = model.Funds;
            interest.text = model.Interest;
            interest.color = model.InterestPositive ? HudPalette.Gain : HudPalette.TextDim;
            risk.text = model.Risk;
            risk.color = model.RiskDanger ? HudPalette.Loss : HudPalette.TextDim;
            hint.text = model.Hint;
            depositButton.SetInteractable(model.CanDeposit);
            depositAllButton.SetInteractable(model.CanDeposit);
            withdrawButton.SetInteractable(model.CanWithdraw);
            withdrawAllButton.SetInteractable(model.CanWithdraw);
        }
    }
}
