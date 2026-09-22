using Code.Core;
using Code.Events;
using Code.Manager;
using UnityEngine;

namespace Code.Buildings
{
    public class Mine : Building
    {
        [SerializeField] private GameEventChannelSO dayEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField, Min(0)] private int goldPerDay = 10;

        private void OnEnable()
        {
            dayEventChannel?.AddListener<DayChangedEvent>(HandleDayChanged);
        }

        private void OnDisable()
        {
            dayEventChannel?.RemoveListener<DayChangedEvent>(HandleDayChanged);
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            // 닫아 둔 광산은 캐지 않는다.
            if (goldPerDay <= 0 || !IsOperating)
                return;

            costEventChannel?.RaiseEvent(new GoldEarnedEvent(
                FacilityEconomyRules.ScaleIncome(this, goldPerDay), GoldChangeSource.Mine));
        }
    }
}
