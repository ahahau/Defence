using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Combat
{
    public class CombatBarsView : MonoBehaviour
    {
        [SerializeField] private Image healthFill;
        [SerializeField] private Image attackFill;

        public void SetHealthRatio(float ratio)
        {
            SetFillScale(healthFill, ratio);
        }

        public void SetAttackRatio(float ratio)
        {
            SetFillScale(attackFill, ratio);
        }

        private static void SetFillScale(Image fill, float ratio)
        {
            var rectTransform = fill.rectTransform;
            var scale = rectTransform.localScale;
            scale.x = Mathf.Clamp01(ratio);
            rectTransform.localScale = scale;
        }
    }
}
