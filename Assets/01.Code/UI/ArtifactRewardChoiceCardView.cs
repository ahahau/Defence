using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class ArtifactRewardChoiceCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text descriptionText;

        public void Initialize(string displayName, Sprite icon, Color iconColor, string description)
        {
            if (nameText != null)
                nameText.text = displayName;

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.color = icon != null ? Color.white : iconColor;
            }

            if (descriptionText != null)
                descriptionText.text = description;
        }
    }
}
