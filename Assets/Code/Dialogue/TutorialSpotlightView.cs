using UnityEngine;

namespace Code.Dialogue
{
    public sealed class TutorialSpotlightView : MonoBehaviour
    {
        [SerializeField] private RectTransform[] dimPanels = new RectTransform[4];

        public RectTransform[] DimPanels => dimPanels;
    }
}
