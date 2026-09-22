using Code.Manager;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    public class DayView : MonoBehaviour
    {
        [SerializeField]
        private DayManager dayManager;

        [SerializeField]
        private TMP_Text dayText;

        [SerializeField]
        private string format = "Day {0}";

        private int _displayedDay = 1;

        private void OnEnable()
        {
            if (dayManager == null) return;
            dayManager.DayChanged += HandleDayChanged;
            dayManager.DayPreviewChanged += HandleDayPreviewChanged;
            HandleDayPreviewChanged(dayManager.NextWaveDay);
        }

        private void OnDisable()
        {
            if (dayManager != null)
            {
                dayManager.DayChanged -= HandleDayChanged;
                dayManager.DayPreviewChanged -= HandleDayPreviewChanged;
            }
            dayText?.transform.DOKill();
        }

        private void HandleDayChanged(int day)
        {
            _displayedDay = day;
            dayText.text = string.Format(format, day);
            PlayDayChangedFeedback();
        }

        private void HandleDayPreviewChanged(int day)
        {
            _displayedDay = day;
            dayText.text = string.Format(format, day);
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
