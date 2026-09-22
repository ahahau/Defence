using Code.Artifacts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Code.UI
{
    public class ArtifactEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;

        private ArtifactDataSO artifact;
        private ArtifactPanelView panelView;

        public void Initialize(ArtifactDataSO artifactData, ArtifactPanelView owner)
        {
            artifact = artifactData;
            panelView = owner;

            iconImage.sprite = artifact.Icon;
            iconImage.preserveAspect = true;
            iconImage.color = artifact.Icon != null ? Color.white : artifact.IconColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            panelView.ShowTooltip(artifact, eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            panelView.HideTooltip();
        }
    }
}
