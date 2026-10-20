using Code.Manager;
using Code.UI.Popup;
using Code.UI.ViewModels;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 왼쪽 위 장부(uGUI): 일차, 운영 자금(크게), 부채·청산, 마력 막대, 상태 칩 4개(부활·등급·민심·위협).
    /// 오른쪽 위 배속 버튼과 일시정지 잠김 사유도 여기서 그린다.
    /// 값은 <see cref="TopHudPresenter"/>가 정하고 여기서는 그리기와 버튼 입력만 한다.
    /// 부채 줄은 파산이 걸릴 수 있으면 붉게 깜박이고, 부채 줄·민심 칩을 누르면 설명 팝업을 띄운다.
    /// </summary>
    public sealed class TopHudView : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;
        private const float BlinkInterval = 0.5f;

        [Header("Managers")]
        [SerializeField] private CostManager costManager;
        [SerializeField] private DayManager dayManager;
        [SerializeField] private MagicManager magicManager;
        [SerializeField] private GameSpeedController speedController;
        [SerializeField] private WaveManager waveManager;
        [SerializeField, Tooltip(GameScope.FieldTooltip)] private GameScope scope;

        [Header("Ledger")]
        [SerializeField] private TMP_Text dayLabel;
        [SerializeField] private TMP_Text goldValue;
        [SerializeField] private TMP_Text debtValue;
        [SerializeField] private TMP_Text debtSub;
        [SerializeField] private UnityEngine.UI.Image debtRow;
        [SerializeField] private UnityEngine.UI.Button debtButton;
        [SerializeField] private TMP_Text magicValue;
        [SerializeField] private UnityEngine.UI.Image magicFill;

        [Header("Chips")]
        [SerializeField] private TMP_Text revivalValue;
        [SerializeField] private TMP_Text gradeValue;
        [SerializeField] private TMP_Text moraleValue;
        [SerializeField] private UnityEngine.UI.Button moraleButton;
        [SerializeField] private TMP_Text threatValue;
        [SerializeField] private UnityEngine.UI.Image threatChip;

        [Header("Speed")]
        [SerializeField] private UiFrameButton pauseButton;
        [SerializeField] private UiFrameButton normalButton;
        [SerializeField] private UiFrameButton fastButton;
        [SerializeField] private TMP_Text pauseLockLabel;

        private readonly TopHudPresenter _presenter = new();
        private UnitRevivalSystem _revival;
        private DungeonGradeManager _grade;
        private MoralePolicyManager _morale;
        private TopHudViewModel _model;
        private Color _debtRowColor;
        private Color _threatChipColor;
        private float _nextRefreshAt;
        private float _nextBlinkAt;
        private bool _blinkOn;

        private void Awake()
        {
            if (debtRow != null) _debtRowColor = debtRow.color;
            if (threatChip != null) _threatChipColor = threatChip.color;
        }

        private void OnEnable()
        {
            _revival = GameScope.Resolve(scope, s => s.UnitRevival, this, nameof(scope));
            _grade = GameScope.Resolve(scope, s => s.DungeonGrade, this, nameof(scope));
            _morale = GameScope.Resolve(scope, s => s.MoralePolicy, this, nameof(scope));

            if (_morale != null) _morale.MoraleChanged += HandleMoraleChanged;
            AddClick(pauseButton, SetPaused);
            AddClick(normalButton, SetNormal);
            AddClick(fastButton, SetFast);
            if (debtButton != null) debtButton.onClick.AddListener(ShowDebtDetail);
            if (moraleButton != null) moraleButton.onClick.AddListener(ShowMoraleDetail);
            Refresh();
        }

        private void OnDisable()
        {
            if (_morale != null) _morale.MoraleChanged -= HandleMoraleChanged;
            RemoveClick(pauseButton, SetPaused);
            RemoveClick(normalButton, SetNormal);
            RemoveClick(fastButton, SetFast);
            if (debtButton != null) debtButton.onClick.RemoveListener(ShowDebtDetail);
            if (moraleButton != null) moraleButton.onClick.RemoveListener(ShowMoraleDetail);
        }

        // 위협 인원처럼 이벤트가 없는 값도 있어 짧은 주기로 다시 읽는다. 실제 시간이라 일시정지 중에도 돈다.
        private void Update()
        {
            var now = Time.unscaledTime;
            if (now >= _nextRefreshAt)
            {
                _nextRefreshAt = now + RefreshInterval;
                Refresh();
            }

            UpdateDebtBlink(now);
        }

        private void Refresh()
        {
            _model = _presenter.Build(costManager, dayManager, magicManager, _revival, _grade, _morale, waveManager, speedController);
            Render();
        }

        private void Render()
        {
            SetText(dayLabel, _model.DayText);
            SetText(goldValue, _model.Gold);
            SetText(debtValue, _model.Debt);
            SetText(debtSub, _model.DebtSub);
            if (debtValue != null)
                debtValue.color = _model.HasDebt ? HudPalette.Loss : HudPalette.TextBody;

            SetText(magicValue, _model.Magic);
            if (magicFill != null)
                magicFill.fillAmount = _model.MagicRatio;

            SetText(revivalValue, _model.Revival);
            SetText(gradeValue, _model.Grade);
            SetText(moraleValue, _model.Morale);
            SetText(threatValue, _model.Threat);
            if (threatValue != null)
                threatValue.color = _model.ThreatAlert ? HudPalette.Warn : HudPalette.TextBody;
            if (threatChip != null)
                threatChip.color = _model.ThreatAlert ? HudPalette.AlertChip : _threatChipColor;

            if (pauseButton != null) pauseButton.SetSelected(Mathf.Approximately(_model.Speed, GameSpeedController.PausedSpeed));
            if (normalButton != null) normalButton.SetSelected(Mathf.Approximately(_model.Speed, GameSpeedController.NormalSpeed));
            if (fastButton != null) fastButton.SetSelected(Mathf.Approximately(_model.Speed, GameSpeedController.FastSpeed));
            if (pauseButton != null) pauseButton.SetInteractable(!_model.PauseLocked);
            SetText(pauseLockLabel, _model.PauseLocked ? $"일시정지 잠김 · {_model.PauseLockReason}" : string.Empty);
        }

        private void UpdateDebtBlink(float now)
        {
            if (debtRow == null)
                return;

            if (!_model.DebtDanger)
            {
                _blinkOn = false;
                debtRow.color = _debtRowColor;
                return;
            }

            if (now < _nextBlinkAt)
                return;

            _nextBlinkAt = now + BlinkInterval;
            _blinkOn = !_blinkOn;
            debtRow.color = _blinkOn ? HudPalette.DangerSurface : _debtRowColor;
        }

        private void HandleMoraleChanged(int morale, int delta, string reason)
        {
            _presenter.RecordMoraleChange(delta, reason);
            Refresh();
        }

        private void ShowDebtDetail()
        {
            if (costManager == null)
                return;

            var day = dayManager != null ? dayManager.CurrentDay : 0;
            PopupController.Show(PopupRequest.Notice(
                _presenter.DebtDetailTitle(costManager, day),
                _presenter.BuildDebtDetail(costManager, day)));
        }

        private void ShowMoraleDetail()
        {
            if (_morale != null)
                PopupController.Show(PopupRequest.Notice("민심 현황", _presenter.BuildMoraleDetail(_morale)));
        }

        private void SetPaused() => SetSpeed(GameSpeedController.PausedSpeed);
        private void SetNormal() => SetSpeed(GameSpeedController.NormalSpeed);
        private void SetFast() => SetSpeed(GameSpeedController.FastSpeed);

        private void SetSpeed(float speed)
        {
            if (speedController == null)
                return;

            speedController.SetSetting(speed);
            Refresh();
        }

        private static void AddClick(UiFrameButton button, UnityEngine.Events.UnityAction action)
        {
            if (button != null && button.Button != null)
                button.Button.onClick.AddListener(action);
        }

        private static void RemoveClick(UiFrameButton button, UnityEngine.Events.UnityAction action)
        {
            if (button != null && button.Button != null)
                button.Button.onClick.RemoveListener(action);
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != value)
                label.text = value ?? string.Empty;
        }
    }
}
