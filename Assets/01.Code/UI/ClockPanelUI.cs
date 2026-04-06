using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class ClockPanelUI : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private TextMeshProUGUI dayLabel;
        [SerializeField] private Image cardImage;
        [SerializeField] private Image faceImage;
        [SerializeField] private Image handImage;

        private bool _canSkipDay = true;

        public GameEventChannelSO UiEventChannel => uiEventChannel;

        private void OnEnable()
        {
            uiEventChannel.AddListener<UiClockStateChangedEvent>(HandleClockStateChanged);
            SyncClockState();
        }

        private void OnDisable()
        {
            uiEventChannel.RemoveListener<UiClockStateChangedEvent>(HandleClockStateChanged);
        }

        private void HandleClockStateChanged(UiClockStateChangedEvent evt)
        {
            dayLabel.text = $"{evt.Day}일차";
            handImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, evt.IsDay ? 20f : -145f);
            _canSkipDay = evt.IsDay;
        }


        public void OnPointerClick(PointerEventData eventData)
        {
            SyncClockState();
            if (!_canSkipDay || eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            HandleSkipDayClicked();
        }

        private void HandleSkipDayClicked()
        {
            uiEventChannel.RaiseEvent(UIEvents.UiSkipDayRequestedEvent);
        }

        private void SyncClockState()
        {
            UiClockStateQueryEvent query = UIEvents.UiClockStateQueryEvent.Initializer();
            uiEventChannel.RaiseEvent(query);
            HandleClockStateChanged(UIEvents.UiClockStateChangedEvent.Initializer(query.Day, query.IsDay));
        }
    }
}
