using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.Units
{
    public class UnitLevelView : MonoBehaviour
    {
        [SerializeField] private Text levelText;
        [SerializeField] private Image experienceFill;

        public void SetLevel(int level)
        {
            levelText.text = $"Lv {level}";
        }

        public void SetExperienceRatio(float ratio)
        {
            var rectTransform = experienceFill.rectTransform;
            var scale = rectTransform.localScale;
            scale.x = Mathf.Clamp01(ratio);
            rectTransform.localScale = scale;
        }
    }
}
