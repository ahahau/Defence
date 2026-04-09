using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.UI
{
    public class UIPointerHoverTracker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool IsPointerInside { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsPointerInside = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsPointerInside = false;
        }

        private void OnDisable()
        {
            IsPointerInside = false;
        }
    }
}
