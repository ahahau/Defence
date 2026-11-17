using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI.Stages
{
    /// <summary>부하 초상 한 칸: 초상·체력 막대(빨강)·궁극기 막대(파랑, 차면 금색). 쓰러지면 어둡게 "복구".</summary>
    public sealed class StageUnitPortrait : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image health;
        [SerializeField] private Image ultimate;
        [SerializeField] private GameObject ultimateTrack;
        [SerializeField] private Image shade;
        [SerializeField] private TMP_Text status;
        private bool _down;

        public bool IsValid => icon != null && health != null && ultimate != null && ultimateTrack != null && shade != null
            && status != null;

        /// <param name="ultimateCharge">궁극기가 없으면 음수(막대를 숨긴다).</param>
        public void Show(Sprite sprite, float healthRatio, float ultimateCharge, bool down)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (icon.sprite != sprite)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
            }
            health.fillAmount = Mathf.Clamp01(healthRatio);
            bool hasUltimate = ultimateCharge >= 0f;
            if (ultimateTrack.activeSelf != hasUltimate)
                ultimateTrack.SetActive(hasUltimate);
            ultimate.fillAmount = Mathf.Clamp01(ultimateCharge);
            ultimate.color = ultimateCharge >= 1f ? HudPalette.Accent : HudPalette.Info;
            if (_down != down || shade.enabled != down)
            {
                _down = down;
                shade.enabled = down;
                status.text = down ? "복구" : string.Empty;
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
