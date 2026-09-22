using Code.Core;
using Code.Events;
using Code.Manager;
using Blade.Core;
using UnityEngine;

namespace Code.UI
{
    /// <summary>도메인 이벤트를 골드 HUD 표시 모델로 변환한다.</summary>
    public class GoldHudPresenter : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GoldCostView view;

        private int _gold;
        private int _debt;
        private int _pendingNet;
        private int _weeklyDue;
        private int _day;

        private void OnEnable()
        {
            costEventChannel.AddListener<GoldChangedEvent>(HandleGoldChanged);
            costEventChannel.AddListener<SettlementPreviewChangedEvent>(HandleSettlementPreview);
            costEventChannel.AddListener<DebtChangedEvent>(HandleDebtChanged);
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
        }

        private void OnDisable()
        {
            costEventChannel.RemoveListener<GoldChangedEvent>(HandleGoldChanged);
            costEventChannel.RemoveListener<SettlementPreviewChangedEvent>(HandleSettlementPreview);
            costEventChannel.RemoveListener<DebtChangedEvent>(HandleDebtChanged);
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
        }

        private void HandleGoldChanged(GoldChangedEvent evt) { _gold = evt.CurrentGold; Render(); }
        private void HandleSettlementPreview(SettlementPreviewChangedEvent evt) { _pendingNet = evt.PendingNet; Render(); }
        private void HandleDebtChanged(DebtChangedEvent evt) { _debt = evt.CurrentDebt; _weeklyDue = evt.WeeklyDue; Render(); }
        private void HandleDayChanged(DayChangedEvent evt) { _day = evt.Day; Render(); }

        private void Render() => view?.Render(new GoldHudState(_gold, _debt, _pendingNet, _weeklyDue,
            DayManager.DaysUntilSettlementFrom(_day)));
    }
}
