using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace _01.Code.TownPanels
{
    [Serializable]
    public class TownUpgradePanelEntry
    {
        [field: SerializeField] public string Title { get; private set; }
        [field: SerializeField] public string Description { get; private set; }
        [field: SerializeField] public string CostLabel { get; private set; }
    }

    [CreateAssetMenu(fileName = "TownUpgradeSection", menuName = "SO/Town/Panel Section/Upgrade", order = 0)]
    public class TownUpgradePanelSectionSO : TownObjectPanelSectionSO
    {
        [field: SerializeField] public string Summary { get; private set; }
        [field: SerializeField] public List<TownUpgradePanelEntry> Upgrades { get; private set; } = new();

        public override string GetBodyText()
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                builder.AppendLine(Summary);
            }

            for (int i = 0; i < Upgrades.Count; i++)
            {
                TownUpgradePanelEntry upgrade = Upgrades[i];
                if (upgrade == null || string.IsNullOrWhiteSpace(upgrade.Title))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(upgrade.Title);
                if (!string.IsNullOrWhiteSpace(upgrade.CostLabel))
                {
                    builder.Append(" (");
                    builder.Append(upgrade.CostLabel);
                    builder.Append(')');
                }

                if (!string.IsNullOrWhiteSpace(upgrade.Description))
                {
                    builder.AppendLine();
                    builder.Append(upgrade.Description);
                }
            }

            return builder.ToString();
        }
    }
}
