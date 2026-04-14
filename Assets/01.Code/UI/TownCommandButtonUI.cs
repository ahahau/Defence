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

        public void SetOwner(TownInteriorScreenUI owner)
        {
            _owner = owner;
        }

        public void BindContext(TownCommandContext context)
        {
            _context = context;
        }

        private void Awake()
        {
            EnsureReferences();
            Disable();
        }

        public void EnableFor(TownCommandSO item, UnityAction callback)
        {
            EnsureReferences();
            if (button == null)
            {
                return;
            }

            gameObject.SetActive(true);
            button.enabled = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
            button.interactable = true;
            _boundCommand = item;
            if (icon != null)
            {
                icon.gameObject.SetActive(true);
                icon.enabled = true;
                Sprite sprite = item != null ? item.GetIcon(_context) : null;
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (background != null)
            {
                background.gameObject.SetActive(true);
                background.enabled = true;
                background.raycastTarget = true;
                background.color = new Color(0.82f, 0.84f, 0.88f, 1f);
            }

            if (label != null)
            {
                label.gameObject.SetActive(true);
                label.enabled = true;
                string displayName = item != null ? item.GetDisplayName(_context) : string.Empty;
                if (string.IsNullOrWhiteSpace(displayName) && item != null)
                {
                    displayName = item.name;
                }

                label.text = displayName;
            }
        }

        public void Disable()
        {
            EnsureReferences();
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
                label.enabled = true;
                label.text = string.Empty;
            }

            _boundCommand = null;
        }

        public void EnsureReferences()
        {
            button ??= GetComponent<Button>();
            background ??= GetComponent<Image>();
            label ??= GetComponentInChildren<TextMeshProUGUI>(true);

            if (icon == null)
            {
                Image[] images = GetComponentsInChildren<Image>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    Image candidate = images[i];
                    if (candidate == null || candidate == background)
                    {
                        continue;
                    }

                    icon = candidate;
                    break;
                }
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_owner != null && _owner.ShouldSuppressTooltipThisFrame())
            {
                return;
            }

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
            if (_owner != null && _owner.ShouldSuppressTooltipThisFrame())
            {
                return;
            }

            if (_owner != null && _boundCommand != null)
            {
                _owner.ShowTooltip(_boundCommand, _context);
            }
        }
    }
}
