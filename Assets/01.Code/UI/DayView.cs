using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class DayView : MonoBehaviour
    {
        [field: SerializeField]
        public GameEventChannelSO EventChannel { get; private set; }

        [field: SerializeField]
        public Text DayText { get; private set; }

        [field: SerializeField]
        public RectTransform ClockHand { get; private set; }

        [field: SerializeField]
        public string Format { get; private set; } = "Day {0}";

        private void OnEnable()
        {
            EventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
            EventChannel.AddListener<DayProgressChangedEvent>(HandleDayProgressChanged);
        }

        private void OnDisable()
        {
            EventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
            EventChannel.RemoveListener<DayProgressChangedEvent>(HandleDayProgressChanged);
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            DayText.text = string.Format(Format, evt.Day);
        }

        private void HandleDayProgressChanged(DayProgressChangedEvent evt)
        {
            ClockHand.localRotation = Quaternion.Euler(0f, 0f, -360f * evt.Progress);
        }
    }
}
