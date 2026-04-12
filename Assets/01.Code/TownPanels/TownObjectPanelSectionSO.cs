using UnityEngine;

namespace _01.Code.TownPanels
{
    public abstract class TownObjectPanelSectionSO : ScriptableObject
    {
        [field: SerializeField] public string SectionTitle { get; private set; }
        [field: SerializeField] public Sprite SectionIcon { get; private set; }

        public virtual string GetSectionTitle()
        {
            return string.IsNullOrWhiteSpace(SectionTitle) ? name : SectionTitle;
        }

        public virtual Sprite GetSectionIcon()
        {
            return SectionIcon;
        }

        public abstract string GetBodyText();
    }
}
