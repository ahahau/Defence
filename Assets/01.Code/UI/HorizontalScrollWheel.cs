using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.UI
{
    [RequireComponent(typeof(ScrollRect))]
    public class HorizontalScrollWheel : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private float scrollSensitivity = 0.1f;
        [SerializeField] private ScrollRect scrollRect;

        public void OnScroll(PointerEventData eventData)
        {
            if (scrollRect == null)
                return;

            scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                scrollRect.horizontalNormalizedPosition - eventData.scrollDelta.y * scrollSensitivity);
        }
    }
}
