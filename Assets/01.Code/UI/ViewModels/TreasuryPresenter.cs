using Code.Buildings;
using Code.Manager;
using Code.MapCreateSystem;

namespace Code.UI.ViewModels
{
    /// <summary>
    /// 금고 한 칸의 상태를 창 값으로 옮긴다. 문구는 지운 Toolkit 금고 창과 같다.
    /// 길이 막힌 금고는 안전하지만 이자도 없다. 맡긴 금화는 청산일에만 꺼낼 수 있다.
    /// </summary>
    public sealed class TreasuryPresenter
    {
        public TreasuryViewModel Build(Treasury treasury, int operatingFunds, int daysUntilSettlement)
        {
            var model = new TreasuryViewModel
            {
                Storage = $"보관 금화  {treasury.StoredGold:N0} / {treasury.Capacity:N0}G",
                Funds = $"운영 자금  {operatingFunds:N0}G",
            };

            var reachable = IntrusionThreat.CanIntrudersReach(treasury.GetComponentInParent<Node>());
            var interest = treasury.ProjectedInterest;
            if (reachable)
            {
                model.Interest = interest > 0
                    ? $"정산 이자 {treasury.InterestPerSettlement:P0}  ·  다음 정산 +{interest}G"
                    : $"정산 이자 {treasury.InterestPerSettlement:P0}";
                model.InterestPositive = interest > 0;
                model.Risk = "침입자가 노리는 곳";
                model.RiskDanger = true;
            }
            else
            {
                model.Interest = "이자 없음";
                model.Risk = "길이 막혀 안전함";
            }

            // 맡기면 청산일까지 묶인다는 것을 넣기 전에 알아야 한다.
            var canWithdrawToday = Treasury.CanWithdrawToday;
            model.Hint = canWithdrawToday
                ? "청산일입니다. 오늘은 금고를 헐 수 있습니다."
                : $"맡긴 금화는 청산일에만 꺼낼 수 있습니다. (남은 {daysUntilSettlement}일)";
            model.CanDeposit = operatingFunds > 0 && treasury.FreeSpace > 0;
            model.CanWithdraw = canWithdrawToday && treasury.StoredGold > 0;
            return model;
        }
    }
}
