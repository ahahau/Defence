using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Combat
{
    public class CombatBarsView : MonoBehaviour
    {
        [SerializeField] private Image healthFill;
        [SerializeField] private Image attackFill;
        [SerializeField] private Image skillFill;

        public void SetHealthRatio(float ratio)
        {
            SetFillScale(healthFill, ratio);
        }

        public void SetAttackRatio(float ratio)
        {
            SetFillScale(attackFill, ratio);
        }

        /// <summary>스킬 충전(쿨다운) 비율. 0=방금 사용, 1=사용 가능. 공속 바 아래에 표시.</summary>
        public void SetSkillRatio(float ratio)
        {
            SetFillScale(skillFill, ratio);
        }

        private void SetFillScale(Image fill, float ratio)
        {
            if (fill == null)
                return;

            var rectTransform = fill.rectTransform;
            var scale = rectTransform.localScale;
            scale.x = Mathf.Clamp01(ratio);
            rectTransform.localScale = scale;
        }
    }
}
