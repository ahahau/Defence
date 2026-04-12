using _01.Code.TownPanels;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class TownPanelSectionButtonUI : MonoBehaviour, IUIElement<TownObjectPanelSectionSO, UnityAction, bool>
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI label;

        public void Configure(Button targetButton, Image backgroundImage, Image iconImage, TextMeshProUGUI labelText)
        {
            button = targetButton;
            background = backgroundImage;
            icon = iconImage;
            label = labelText;
        }

        public void EnableFor(TownObjectPanelSectionSO item, UnityAction callback, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = item != null;
            button.onClick.RemoveAllListeners();
            if (item != null && callback != null)
            {
                button.onClick.AddListener(callback);
            }

            if (label != null)
            {
                label.text = item != null ? item.GetSectionTitle() : string.Empty;
            }

            if (icon != null)
            {
                Sprite sprite = item != null ? item.GetSectionIcon() : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (background != null)
            {
                background.color = selected
                    ? new Color(0.80f, 0.70f, 0.32f, 0.98f)
                    : new Color(0.43f, 0.46f, 0.50f, 0.98f);
            }
        }

        public void Disable()
        {
            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }

            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (label != null)
            {
                label.text = string.Empty;
            }

            if (background != null)
            {
                background.color = new Color(0.28f, 0.30f, 0.34f, 0.92f);
            }
        }
    }
}
