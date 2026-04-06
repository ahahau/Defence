using UnityEngine;

namespace _01.Code.UI
{
    public class BuildSectionUIController : UISectionController
    {
        [SerializeField] private InventoryTabsUI inventoryTabsUI;

        protected override void CacheReferences()
        {
            inventoryTabsUI = GetComponent<InventoryTabsUI>();
        }

        protected override void OnInitializeSection()
        {
            gameObject.SetActive(false);
        }
    }
}
