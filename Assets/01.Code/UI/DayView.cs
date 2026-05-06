using _01.Code.Events;
using _01.Code.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        private void OnEnable()
        {
            dayEventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
            if (dayText == null)
                dayText = GetComponent<TMP_Text>();
        }

        private void OnDisable()
        {
            dayEventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            dayText.text = string.Format(format, evt.Day);
        }

    }
}
