using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.UI
{
    public class DraggableWindow : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        [SerializeField] private RectTransform targetWindow;
        [SerializeField] private Canvas targetCanvas;

        private Vector2 _pointerOffset;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetWindow,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                _pointerOffset = targetWindow.anchoredPosition - localPoint;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 canvasPoint))
            {
                targetWindow.anchoredPosition = canvasPoint + _pointerOffset;
            }
        }
    }
}
