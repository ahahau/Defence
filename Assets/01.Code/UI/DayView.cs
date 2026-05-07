using _01.Code.Events;
using _01.Code.Core;
using TMPro;
using UnityEngine;

namespace _01.Code.UI
{
    public class DayView : MonoBehaviour
    {
        [SerializeField]
        private GameEventChannelSO dayEventChannel;

        [SerializeField]
        private TMP_Text dayText;

        [SerializeField]
        private string format = "Day {0}";

        private int _displayedDay = 1;

        private void OnEnable()
        {
            dayEventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
            dayEventChannel.AddListener<DayPreviewChangedEvent>(HandleDayPreviewChanged);
        }

        private void OnDisable()
        {
            dayEventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
            dayEventChannel.RemoveListener<DayPreviewChangedEvent>(HandleDayPreviewChanged);
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            _displayedDay = evt.Day;
            dayText.text = string.Format(format, evt.Day);
        }

        private void HandleDayPreviewChanged(DayPreviewChangedEvent evt)
        {
            _displayedDay = evt.Day;
            dayText.text = string.Format(format, evt.Day);
        }

    }
}
