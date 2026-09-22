using Code.Manager;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>운영 금화·빚·날짜와 배속을 보여 주고 배속 선택을 전달한다.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameplayHudToolkitView : MonoBehaviour
    {
        private Label _goldLabel;
        private Label _debtLabel;
        private Label _dayLabel;
        private Button _pauseButton;
        private Button _normalButton;
        private Button _fastButton;
        private CostManager _costManager;
        private DayManager _dayManager;
        private GameSpeedController _speedController;

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _goldLabel = root.Q<Label>("gold-label");
            _debtLabel = root.Q<Label>("debt-label");
            _dayLabel = root.Q<Label>("day-label");
            _pauseButton = root.Q<Button>("pause-button");
            _normalButton = root.Q<Button>("normal-button");
            _fastButton = root.Q<Button>("fast-button");

            if (_pauseButton != null)
                _pauseButton.clicked += SetPaused;
            if (_normalButton != null)
                _normalButton.clicked += SetNormal;
            if (_fastButton != null)
                _fastButton.clicked += SetFast;

            BindManagers();
            Refresh();
        }

        private void OnDisable()
        {
            if (_pauseButton != null)
                _pauseButton.clicked -= SetPaused;
            if (_normalButton != null)
                _normalButton.clicked -= SetNormal;
            if (_fastButton != null)
                _fastButton.clicked -= SetFast;
            UnbindManagers();
        }

        private void BindManagers()
        {
            _costManager = CostManager.Current;
            _dayManager = DayManager.Current;
            _speedController = GameSpeedController.Current;

            if (_costManager != null)
                _costManager.StateChanged += Refresh;
            if (_dayManager != null)
            {
                _dayManager.DayChanged += HandleDayChanged;
                _dayManager.DayPreviewChanged += HandleDayPreviewChanged;
            }
            if (_speedController != null)
                _speedController.SettingChanged += HandleSpeedChanged;
        }

        private void UnbindManagers()
        {
            if (_costManager != null)
                _costManager.StateChanged -= Refresh;
            if (_dayManager != null)
            {
                _dayManager.DayChanged -= HandleDayChanged;
                _dayManager.DayPreviewChanged -= HandleDayPreviewChanged;
            }
            if (_speedController != null)
                _speedController.SettingChanged -= HandleSpeedChanged;
        }

        private void HandleDayChanged(int _) => Refresh();
        private void HandleDayPreviewChanged(int _) => Refresh();
        private void HandleSpeedChanged(float _) => RefreshSpeedButtons();

        private void Refresh()
        {
            _costManager ??= CostManager.Current;
            _dayManager ??= DayManager.Current;
            _speedController ??= GameSpeedController.Current;

            if (_goldLabel != null)
                _goldLabel.text = $"운영 자금 {_costManager?.CurrentGold ?? 0}G";

            if (_debtLabel != null)
                _debtLabel.text = BuildDebtText();

            if (_dayLabel != null)
            {
                var day = _dayManager != null ? _dayManager.CurrentDay : 0;
                var nextDay = _dayManager != null ? _dayManager.NextWaveDay : 1;
                _dayLabel.text = day > 0 ? $"DAY {day} · 다음 영업 {nextDay}일차" : "DAY 0 · 영업 준비";
            }

            RefreshSpeedButtons();
        }

        private string BuildDebtText()
        {
            if (_costManager == null || _costManager.CurrentDebt <= 0)
                return "빚 없음";

            var day = _dayManager != null ? _dayManager.CurrentDay : 0;
            var daysLeft = DayManager.DaysUntilSettlementFrom(day);
            var due = _costManager.WeeklyDue;
            return daysLeft <= 0
                ? $"빚 {_costManager.CurrentDebt}G · 오늘 최소 {due}G"
                : $"빚 {_costManager.CurrentDebt}G · {daysLeft}일 뒤 최소 {due}G";
        }

        private void RefreshSpeedButtons()
        {
            var speed = _speedController != null ? _speedController.Setting : GameSpeedController.NormalSpeed;
            SetSelected(_pauseButton, Mathf.Approximately(speed, GameSpeedController.PausedSpeed));
            SetSelected(_normalButton, Mathf.Approximately(speed, GameSpeedController.NormalSpeed));
            SetSelected(_fastButton, Mathf.Approximately(speed, GameSpeedController.FastSpeed));
        }

        private static void SetSelected(Button button, bool selected)
        {
            if (button == null)
                return;

            button.EnableInClassList("is-selected", selected);
        }

        private void SetPaused() => _speedController?.SetSetting(GameSpeedController.PausedSpeed);
        private void SetNormal() => _speedController?.SetSetting(GameSpeedController.NormalSpeed);
        private void SetFast() => _speedController?.SetSetting(GameSpeedController.FastSpeed);
    }
}
