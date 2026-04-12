using _01.Code.TownPanels;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.UI
{
    public class TownSkillTreeNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        private TownInteriorScreenUI _owner;
        private TownSkillTreeNodeEntry _node;

        public void Configure(TownInteriorScreenUI owner, TownSkillTreeNodeEntry node, TextMeshProUGUI titleLabel, TextMeshProUGUI descriptionLabel)
        {
            _owner = owner;
            _node = node;
            titleText = titleLabel;
            descriptionText = descriptionLabel;

            if (titleText != null)
            {
                titleText.text = node != null ? node.NodeName : string.Empty;
                titleText.raycastTarget = false;
            }

            if (descriptionText != null)
            {
                descriptionText.text = node != null ? node.EffectDescription : string.Empty;
                descriptionText.raycastTarget = false;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _owner?.ShowSkillTreeNodeTooltip(_node);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.HideSkillTreeNodeTooltip();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            _owner?.ShowSkillTreeNodeTooltip(_node);
        }
    }
}
