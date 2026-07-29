using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.UI
{
    public enum UnitDetailTooltipKind
    {
        Trait,
        Personality
    }

    /// <summary>유닛 상세의 성격 줄에서 기본 성격 효과를 설명한다.</summary>
    public sealed class UnitPersonalityTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private UnitStatusPanelView owner;
        [SerializeField] private UnitDetailTooltipKind kind;

        private void Awake() => owner ??= GetComponentInParent<UnitStatusPanelView>(true);

        public void Bind(UnitStatusPanelView panel, UnitDetailTooltipKind tooltipKind)
        {
            owner = panel;
            kind = tooltipKind;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (owner != null && owner.SelectedUnit != null)
                PersonalityTooltipView.ShowFor(
                    owner.SelectedUnit,
                    kind,
                    eventData.position,
                    owner.GetComponentInParent<Canvas>(true));
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PersonalityTooltipView.HideCurrent();
        }

        private void OnDisable() => PersonalityTooltipView.HideCurrent();
    }
}
