using Code.Artifacts;
using UnityEngine;

namespace Code.UI.ViewModels
{
    /// <summary>유물 줄 한 칸. 성립한 조합에 든 유물은 금색 테두리다.</summary>
    public sealed class ArtifactSlotViewModel
    {
        public ArtifactDataSO Artifact;
        public Sprite Icon;
        public Color IconColor;
        public bool InCombo;
    }
}
