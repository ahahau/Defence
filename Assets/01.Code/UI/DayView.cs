using _01.Code.Events;
using _01.Code.Core;
using DG.Tweening;
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

        private void Awake()
        {
            // DAY 표시는 자원 카드와 분리된 작은 배지로 고정한다.
            if (dayText == null)
                return;

            var rect = dayText.rectTransform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-402f, -24f);
            rect.sizeDelta = new Vector2(140f, 52f);
            rect.localScale = Vector3.one;

            dayText.alignment = TextAlignmentOptions.Center;
            dayText.fontStyle |= FontStyles.Bold;
            dayText.enableAutoSizing = true;
            dayText.fontSizeMin = 16f;
            dayText.fontSizeMax = 28f;
            dayText.enableWordWrapping = false;
            dayText.overflowMode = TextOverflowModes.Ellipsis;
            dayText.raycastTarget = false;
        }

        private void OnEnable()
        {
            dayEventChannel.AddListener<DayChangedEvent>(HandleDayChanged);
            dayEventChannel.AddListener<DayPreviewChangedEvent>(HandleDayPreviewChanged);
        }

        private void OnDisable()
        {
            dayEventChannel.RemoveListener<DayChangedEvent>(HandleDayChanged);
            dayEventChannel.RemoveListener<DayPreviewChangedEvent>(HandleDayPreviewChanged);
            dayText?.transform.DOKill();
        }

        private void HandleDayChanged(DayChangedEvent evt)
        {
            _displayedDay = evt.Day;
            dayText.text = string.Format(format, evt.Day);
            PlayDayChangedFeedback();
        }

        private void HandleDayPreviewChanged(DayPreviewChangedEvent evt)
        {
            _displayedDay = evt.Day;
            dayText.text = string.Format(format, evt.Day);
        }

        private void PlayDayChangedFeedback()
        {
            if (dayText == null)
                return;

            var target = dayText.transform;
            target.DOKill();
            target.localScale = Vector3.one * 0.82f;
            target.DOScale(1f, 0.32f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetLink(dayText.gameObject);
        }

    }
}
