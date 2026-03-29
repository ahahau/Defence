using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.UI
{
    public class DropdownWheelInputUI : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private TMP_Dropdown targetDropdown;

        public void OnScroll(PointerEventData eventData)
        {
            if (targetDropdown.options.Count <= 1)
            {
                return;
            }

            float scroll = eventData.scrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            int direction = scroll > 0f ? -1 : 1;
            int nextValue = Mathf.Clamp(targetDropdown.value + direction, 0, targetDropdown.options.Count - 1);
            if (nextValue == targetDropdown.value)
            {
                return;
            }

            targetDropdown.value = nextValue;
            targetDropdown.RefreshShownValue();
        }
    }
}
