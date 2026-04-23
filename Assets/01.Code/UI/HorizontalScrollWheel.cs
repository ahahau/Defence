using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _01.Code.UI
{
    [RequireComponent(typeof(ScrollRect))]
    public class HorizontalScrollWheel : MonoBehaviour, IScrollHandler
    {
        [SerializeField] private float scrollSensitivity = 0.1f;

        private ScrollRect _scrollRect;

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_scrollRect == null)
                _scrollRect = GetComponent<ScrollRect>();

            _scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(
                _scrollRect.horizontalNormalizedPosition - eventData.scrollDelta.y * scrollSensitivity);
        }
    }
}
