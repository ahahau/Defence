using System.Text;
using Code.Manager;
using Code.Manager.Economy;
using Code.UI.Popup;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI.Toolkit
{
    /// <summary>
    /// HUD 상단 부채 칸. 파산이 걸릴 수 있으면 붉게 깜박이고, 누르면 사유와 신용 규칙을 팝업으로 보여 준다.
    ///
    /// 예전에는 사유를 <c>tooltip</c>에 넣었는데 런타임 UI Toolkit은 그것을 띄우지 않아 볼 길이 없었다.
    /// </summary>
    public sealed class DebtAlertView
    {
        private const string DangerClass = "is-danger";
        private const string BlinkClass = "is-blink";
        private const long BlinkMilliseconds = 500;

        private readonly VisualElement _chip;
        private readonly StringBuilder _builder = new();
        private IVisualElementScheduledItem _blink;
        private BankruptcyRisk _risk;
        private CostManager _costManager;
        private int _currentDay;

        public DebtAlertView(VisualElement chip)
        {
            _chip = chip;
            if (_chip == null)
                return;

            _chip.RegisterCallback<ClickEvent>(HandleClicked);
            // HUD 위에 있어도 지도 클릭이 뚫고 지나가지 않게 한다.
            ToolkitPointerBlockers.Register(_chip);
        }

        public void Dispose()
        {
            if (_chip == null)
                return;

            _chip.UnregisterCallback<ClickEvent>(HandleClicked);
            ToolkitPointerBlockers.Unregister(_chip);
            _blink?.Pause();
        }

        public void Refresh(CostManager costManager, int currentDay)
        {
            _costManager = costManager;
            _currentDay = currentDay;
            _risk = costManager == null
                ? BankruptcyRisk.None
                : BankruptcyRiskRules.Evaluate(
                    costManager.CurrentDebt,
                    costManager.CurrentGold,
                    costManager.IsOverCreditLimit,
                    costManager.CreditWarnings,
                    DayManager.DaysUntilSettlementFrom(currentDay),
                    costManager.WeeklyDue);

            if (_chip == null)
                return;

            var danger = _risk != BankruptcyRisk.None;
            _chip.EnableInClassList(DangerClass, danger);
            if (danger)
            {
                // 패널 스케줄러는 실제 시간으로 돈다. 일시정지 중에도 깜박여야 경고가 보인다.
                _blink ??= _chip.schedule.Execute(() => _chip.ToggleInClassList(BlinkClass)).Every(BlinkMilliseconds);
                _blink.Resume();
            }
            else
            {
                _blink?.Pause();
                _chip.RemoveFromClassList(BlinkClass);
            }
        }

        private void HandleClicked(ClickEvent evt)
        {
            if (_costManager == null)
                return;

            var title = _risk != BankruptcyRisk.None ? "파산 위험" : "부채와 신용";
            PopupController.Show(PopupRequest.Notice(title, BuildDetail()));
        }

        private string BuildDetail()
        {
            _builder.Clear();
            AppendRiskReasons();

            var max = CreditWarningRules.MaxWarnings;
            var interest = Mathf.RoundToInt(CreditWarningRules.InterestPerWarning * 100f);
            var minimum = Mathf.RoundToInt(CreditWarningRules.MinimumPrincipalPerWarning * 100f);
            _builder.Append($"빚 {GoldText.Amount(_costManager.CurrentDebt)} / 한도 {GoldText.Amount(_costManager.CreditLimit)}\n");
            _builder.Append($"신용 경고 {_costManager.CreditWarnings}/{max} (단계마다 이자 +{interest}%p, 최소 상환 +{minimum}%p · 빚을 다 갚으면 초기화)\n");
            _builder.Append($"한도를 넘었다가 영업 종료 전에 돌아오면 경고가 쌓이고, 경고 {max}단계에서 또 넘으면 파산합니다.");
            return _builder.ToString();
        }

        private void AppendRiskReasons()
        {
            if ((_risk & BankruptcyRisk.OverCreditLimit) != 0)
                _builder.Append($"• 부채가 한도를 {GoldText.Amount(_costManager.CurrentDebt - _costManager.CreditLimit)} 넘었습니다. "
                                + "영업이 끝날 때 금화로 초과분을 긴급 상환하고, 그래도 넘으면 파산합니다.\n");

            if ((_risk & BankruptcyRisk.FinalWarning) != 0)
                _builder.Append($"• 신용 경고가 마지막 단계({CreditWarningRules.MaxWarnings})입니다. 한도를 한 번 더 넘으면 그날 영업이 끝난 뒤 파산합니다.\n");

            if ((_risk & BankruptcyRisk.SettlementShortfall) != 0)
            {
                var when = DayManager.DaysUntilSettlementFrom(_currentDay) == 0 ? "오늘" : "내일";
                _builder.Append($"• {when} 청산일에 최소 {GoldText.Amount(_costManager.WeeklyDue)}를 내야 하는데 "
                                + $"운영 자금이 {GoldText.Amount(_costManager.CurrentGold)}입니다. 못 내면 파산합니다.\n");
            }

            if (_risk != BankruptcyRisk.None)
                _builder.Append('\n');
        }
    }
}
