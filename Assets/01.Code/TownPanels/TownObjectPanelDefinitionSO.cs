using System.Collections.Generic;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.TownPanels
{
    [CreateAssetMenu(fileName = "TownObjectPanelDefinition", menuName = "SO/Town/Object Panel Definition", order = 3)]
    public class TownObjectPanelDefinitionSO : ScriptableObject
    {
        [field: SerializeField] public string PanelTitle { get; private set; }
        [field: SerializeField] public List<TownObjectPanelSectionSO> Sections { get; private set; } = new();

        public string GetPanelTitle(TownTileObjectDataSO data)
        {
            if (!string.IsNullOrWhiteSpace(PanelTitle))
            {
                return PanelTitle;
            }

            if (data != null && !string.IsNullOrWhiteSpace(data.DisplayName))
            {
                return data.DisplayName;
            }

            return name;
        }

        public void AddSection(TownObjectPanelSectionSO section)
        {
            if (section == null || Sections.Contains(section))
            {
                return;
            }

            Sections.Add(section);
        }
    }
}
