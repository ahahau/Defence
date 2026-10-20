using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Code.UI
{
    /// <summary>마우스가 올라오고 나가는 것을 알린다(툴팁용). 붙은 그래픽의 Raycast Target이 켜져 있어야 한다.</summary>
    public sealed class UiHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action Entered;
        public event Action Exited;

        public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

        public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();
    }
}
