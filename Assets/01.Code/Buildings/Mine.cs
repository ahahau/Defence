using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Buildings
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
            if (goldPerDay <= 0)
                return;

            costEventChannel?.RaiseEvent(new GoldEarnedEvent(goldPerDay));
        }
    }
}
