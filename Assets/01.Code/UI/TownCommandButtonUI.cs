using _01.Code.TownCommands;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class TownCommandButtonUI : MonoBehaviour, IUIElement<TownCommandSO, UnityAction>
    {
        [SerializeField] private Image icon;
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image background;

        public void Configure(Image iconImage, Button targetButton, TextMeshProUGUI labelText, Image backgroundImage)
        {
            icon = iconImage;
            button = targetButton;
            label = labelText;
            background = backgroundImage;
        }

        private void Awake()
        {
            Disable();
        }

        public void EnableFor(TownCommandSO item, UnityAction callback)
        {
            if (button == null || label == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
            button.interactable = true;
            label.text = item != null ? item.GetDisplayName(null) : string.Empty;
            if (icon != null)
            {
                Sprite sprite = item != null ? item.GetIcon(null) : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (background != null)
            {
                background.color = new Color(0.18f, 0.20f, 0.22f, 1f);
            }
        }

        public void Disable()
        {
            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }

            if (label != null)
            {
                label.text = string.Empty;
            }

            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (background != null)
            {
                background.color = new Color(0.10f, 0.11f, 0.12f, 0.65f);
            }
        }
    }
}
