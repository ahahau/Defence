using _01.Code.TownPanels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.UI
{
    public class TownUnitTreeNodeUI : MonoBehaviour,
        IUIElement<TownUnitTreeNodeEntry, TownInteriorScreenUI>,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler
    {
    //    [SerializeField] private Image background;
    //    [SerializeField] private Image iconImage;
    //    [SerializeField] private TextMeshProUGUI titleText;
    //    [SerializeField] private TextMeshProUGUI descriptionText;

        private TownInteriorScreenUI _owner;
        private TownUnitTreeNodeEntry _entry;

        public void EnableFor(TownUnitTreeNodeEntry item, TownInteriorScreenUI owner)
        {
            _entry = item;
            _owner = owner;

            EnsureReferences();

           // if (background != null)
           // {
           //     Color tint = item != null ? item.GetTint() : Color.white;
           //     background.color = new Color(
           //         Mathf.Clamp01(tint.r * 0.45f + 0.18f),
           //         Mathf.Clamp01(tint.g * 0.45f + 0.20f),
           //         Mathf.Clamp01(tint.b * 0.45f + 0.24f),
           //         0.96f);
           // }
           //
           // if (iconImage != null)
           // {
           //     Sprite icon = item != null ? item.GetIcon() : null;
           //     if (icon != null)
           //     {
           //         iconImage.sprite = icon;
           //     }
           //
           //     iconImage.enabled = iconImage.sprite != null;
           //     iconImage.color = icon != null ? Color.white : new Color(1f, 0.82f, 0.28f, 1f);
           // }
           //
           // if (titleText != null)
           // {
           //     titleText.text = item != null ? item.GetDisplayName() : string.Empty;
           //     titleText.color = Color.white;
           // }
           //
           // if (descriptionText != null)
           // {
           //     descriptionText.text = item != null ? item.GetDescription() : string.Empty;
           //     descriptionText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
           // }
        }

        public void Disable()
        {
            _entry = null;
            _owner = null;
            EnsureReferences();

            //if (iconImage != null)
            //{
            //    iconImage.sprite = null;
            //    iconImage.enabled = false;
            //}
            //
            //if (titleText != null)
            //{
            //    titleText.text = string.Empty;
            //}
            //
            //if (descriptionText != null)
            //{
            //    descriptionText.text = string.Empty;
            //}
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner?.ShowSkillTreeNodeTooltip(_entry);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.HideSkillTreeNodeTooltip();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            _owner?.ShowSkillTreeNodeTooltip(_entry);
        }

        private void EnsureReferences()
        {
            //background ??= GetComponent<Image>();
//
//            if (iconImage == null)
//            {
//                Transform iconTransform = transform.Find("Icon");
//                if (iconTransform != null)
//                {
//                    iconImage = iconTransform.GetComponent<Image>();
//                }
//            }
//
//            if (titleText == null)
//            {
//                Transform titleTransform = transform.Find("Title");
//                if (titleTransform != null)
//                {
//                    titleText = titleTransform.GetComponent<TextMeshProUGUI>();
//                }
//            }
//
//            if (descriptionText == null)
//            {
//                Transform descriptionTransform = transform.Find("Description");
//                if (descriptionTransform != null)
//                {
//                    descriptionText = descriptionTransform.GetComponent<TextMeshProUGUI>();
//                }
          //  }
        }
    }
}
