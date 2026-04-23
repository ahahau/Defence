using _01.Code.Events;
using _01.Code.Core;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class DayView : MonoBehaviour
    {
        [SerializeField]
        private GameEventChannelSO dayEventChannel;

        [SerializeField]
        private Text dayText;

        [SerializeField]
        private RectTransform clockHand;

        [SerializeField]
        private string format = "Day {0}";

        private void OnEnable()
        {
            dayEventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
            dayEventChannel.AddListener<DayProgressChangedEvent>(HandleDayProgressChanged);
        }

        private void OnDisable()
        {
            dayEventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
            dayEventChannel.RemoveListener<DayProgressChangedEvent>(HandleDayProgressChanged);
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            dayText.text = string.Format(format, evt.Day);
        }

        private void HandleDayProgressChanged(DayProgressChangedEvent evt)
        {
            clockHand.localRotation = Quaternion.Euler(0f, 0f, -360f * evt.Progress);
        }
    }
}
