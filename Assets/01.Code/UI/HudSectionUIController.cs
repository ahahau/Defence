using UnityEngine;

namespace _01.Code.UI
{
    public class HudSectionUIController : UISectionController
    {
        [SerializeField] private DefaultCostBarUI costBarUI;
        [SerializeField] private ClockPanelUI clockPanelUI;

        protected override void CacheReferences()
        {
            costBarUI ??= GetComponentInChildren<DefaultCostBarUI>(true);
            clockPanelUI ??= GetComponentInChildren<ClockPanelUI>(true);
        }

        protected override void OnInitializeSection()
        {
            if (costBarUI != null)
            {
                costBarUI.enabled = true;
            }

            if (clockPanelUI != null)
            {
                clockPanelUI.enabled = true;
            }
        }
    }
}
