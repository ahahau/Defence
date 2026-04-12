using _01.Code.TownCommands;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class TownCommandButtonUI : MonoBehaviour, IUIElement<TownCommandSO, UnityAction>, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI label;
        private TownCommandContext _context;
        private TownCommandSO _boundCommand;
        private TownInteriorScreenUI _owner;

        public void Configure(Image iconImage, Button targetButton, Image backgroundImage, TextMeshProUGUI labelText, TownInteriorScreenUI owner)
        {
            icon = iconImage;
            button = targetButton;
            background = backgroundImage;
            label = labelText;
            _owner = owner;
        }

        public void BindContext(TownCommandContext context)
        {
            _context = context;
        }

        private void Awake()
        {
            Disable();
        }

        public void EnableFor(TownCommandSO item, UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
            button.interactable = true;
            _boundCommand = item;
            if (icon != null)
            {
                Sprite sprite = item != null ? item.GetIcon(_context) : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (background != null)
            {
                background.color = new Color(0.82f, 0.84f, 0.88f, 1f);
            }

            if (label != null)
            {
                label.text = item != null ? item.GetDisplayName(_context) : string.Empty;
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

            if (background != null)
            {
                background.color = new Color(0.45f, 0.47f, 0.50f, 0.85f);
            }

            if (label != null)
            {
                label.text = string.Empty;
            }

            _boundCommand = null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_owner != null && _boundCommand != null)
            {
                _owner.ShowTooltip(_boundCommand, _context);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.HideTooltip();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (_owner != null && _boundCommand != null)
            {
                _owner.ShowTooltip(_boundCommand, _context);
            }
        }
    }
}
