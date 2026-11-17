using Code.Artifacts;
using Code.UI.ViewModels;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Code.UI
{
    /// <summary>유물 줄 한 칸(그리기만). 마우스가 올라오고 떠나는 것만 알린다 — 설명 카드는 <see cref="ArtifactRowView"/>가 띄운다.</summary>
    public sealed class ArtifactSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private UnityEngine.UI.Image back;
        [SerializeField] private UnityEngine.UI.Image outline;
        [SerializeField] private UnityEngine.UI.Image icon;

        public ArtifactDataSO Artifact { get; private set; }

        public event System.Action<ArtifactSlotView> PointerEntered;
        public event System.Action<ArtifactSlotView> PointerExited;

        public void Bind(ArtifactSlotViewModel model)
        {
            Artifact = model?.Artifact;
            var filled = model != null;
            icon.enabled = filled && model.Icon != null;
            icon.sprite = filled ? model.Icon : null;
            icon.color = filled ? model.IconColor : Color.white;
            back.color = filled ? HudPalette.Chip : HudPalette.Deep;
            outline.color = !filled ? HudPalette.Track : model.InCombo ? HudPalette.BorderAccent : HudPalette.BorderQuiet;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Artifact != null)
                PointerEntered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData) => PointerExited?.Invoke(this);
    }
}
