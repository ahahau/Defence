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

        private void Awake()
        {
            // HUD 왼쪽 위 일차 글(UI Toolkit)이 같은 값을 보여 준다. 하단 바 위에 겹쳐 그려서 끈다.
            // 씬에서 이 오브젝트를 지우면 이 스크립트도 함께 지운다(Toolkit 전환 1단계).
            gameObject.SetActive(false);
        }

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
