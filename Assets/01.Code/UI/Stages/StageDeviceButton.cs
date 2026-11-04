using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Stages
{
    /// <summary>수동 장치 버튼 한 칸: 이름·단축키(F는 고른 장치에만)·재사용 막대와 남은 초. 남은 초 글은 바뀔 때만 쓴다.</summary>
    public sealed class StageDeviceButton : MonoBehaviour
    {
        [SerializeField] private UiFrameButton button;
        [SerializeField] private TMP_Text key;
        [SerializeField] private Image cooldown;
        [SerializeField] private TMP_Text seconds;
        private int _shownSeconds = -1;
        private string _shownName;

        public bool IsValid => button != null && key != null && cooldown != null && seconds != null;
        public Button Button => button.Button;

        /// <param name="ready">재사용 대기가 끝났고 발동 중이 아니다.</param>
        /// <param name="readyRatio">재사용 진행(0~1, 1이면 준비).</param>
        public void Show(string name, bool selected, bool ready, float readyRatio, float secondsLeft)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (_shownName != name)
            {
                _shownName = name;
                button.SetLabel(name);
            }
            button.SetSelected(selected);
            key.enabled = selected;
            cooldown.fillAmount = Mathf.Clamp01(readyRatio);
            cooldown.color = ready ? HudPalette.Accent : HudPalette.Info;
            int whole = ready ? 0 : Mathf.CeilToInt(secondsLeft);
            if (whole != _shownSeconds)
            {
                _shownSeconds = whole;
                seconds.text = whole > 0 ? whole + "초" : string.Empty;
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
