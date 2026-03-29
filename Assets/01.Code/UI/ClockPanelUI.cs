using _01.Code.Core;
using _01.Code.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class ClockPanelUI : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private TextMeshProUGUI dayLabel;
        [SerializeField] private TextMeshProUGUI actionLabel;
        [SerializeField] private Image cardImage;
        [SerializeField] private Image faceImage;
        [SerializeField] private Image handImage;
        [SerializeField] private Button button;

        private void Awake()
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => uiEventChannel.RaiseEvent(UIEvents.UiSkipDayRequestedEvent));
        }

        private void OnEnable()
        {
            uiEventChannel.AddListener<UiClockStateChangedEvent>(HandleClockStateChanged);
        }

        private void OnDisable()
        {
            uiEventChannel.RemoveListener<UiClockStateChangedEvent>(HandleClockStateChanged);
        }

        private void HandleClockStateChanged(UiClockStateChangedEvent evt)
        {
            dayLabel.text = $"{evt.Day}일차"; 
            handImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, evt.IsDay ? 20f : -145f);
            button.interactable = evt.IsDay;
        }
    }
}
