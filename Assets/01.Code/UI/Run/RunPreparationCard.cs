using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Code.UI.Run
{
    /// <summary>배치 카드의 클릭·끌기 입력만 전달한다. 놓을 방 판단은 디렉터가 한다.</summary>
    public class RunPreparationCard : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private UnityEngine.UI.Image affinity;
        [SerializeField] private UnityEngine.UI.Image outline;
        public event Action Clicked;
        public event Action<Vector2> Dropped;
        public event Action<Vector2> Dragged;
        public event Action DragStarted;

        public void Show(string text, Color color, bool selected)
        {
            if (label != null)
            {
                label.SetText(text);
                label.color = selected ? HudPalette.Text : HudPalette.TextDim;
            }
            if (affinity != null) affinity.color = color;
            if (outline != null) outline.color = selected ? HudPalette.BorderAccent : HudPalette.BorderQuiet;
        }

        public void OnPointerClick(PointerEventData data) => Clicked?.Invoke();
        public void OnBeginDrag(PointerEventData data) => DragStarted?.Invoke();
        public void OnDrag(PointerEventData data) => Dragged?.Invoke(data.position);
        public void OnEndDrag(PointerEventData data) => Dropped?.Invoke(data.position);
    }
}
