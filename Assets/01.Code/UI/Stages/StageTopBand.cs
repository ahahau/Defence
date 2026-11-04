using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Stages
{
    /// <summary>
    /// 함정 본편 위 한 띠: 코어 막대·금화·웨이브 진행 막대·남은 적. 칸 폭은 고정이고 값은 StageHudView가 넘긴다.
    /// 배속·설정 버튼은 띠 오른쪽 끝 위에 따로 놓인다.
    /// </summary>
    public sealed class StageTopBand : MonoBehaviour
    {
        [SerializeField] private Image coreFill;
        [SerializeField] private TMP_Text core;
        [SerializeField] private TMP_Text gold;
        [SerializeField] private TMP_Text wave;
        [SerializeField] private Image waveFill;
        [SerializeField] private TMP_Text remaining;

        public bool IsValid => coreFill != null && core != null && gold != null && wave != null && waveFill != null
            && remaining != null;

        /// <param name="danger">코어가 30% 이하이거나 적이 코어 가까이 있으면 막대와 숫자를 위험 색으로.</param>
        /// <param name="waveProgress">이번 웨이브에서 처리한 적 비율(0~1). 준비 단계는 0.</param>
        public void Show(int coreHealth, int maxCoreHealth, bool danger, int goldAmount, string waveText, float waveProgress,
            string remainingText)
        {
            coreFill.fillAmount = maxCoreHealth > 0 ? Mathf.Clamp01((float)coreHealth / maxCoreHealth) : 0f;
            // 체력은 빨강 계열(게임 UI 색 규칙). 위험하면 더 짙은 경고 빨강.
            coreFill.color = danger ? HudPalette.Danger : HudPalette.Loss;
            core.text = $"{coreHealth}/{maxCoreHealth}";
            core.color = danger ? HudPalette.Danger : HudPalette.Text;
            gold.text = goldAmount.ToString();
            wave.text = waveText;
            waveFill.fillAmount = Mathf.Clamp01(waveProgress);
            remaining.text = remainingText;
        }
    }
}
