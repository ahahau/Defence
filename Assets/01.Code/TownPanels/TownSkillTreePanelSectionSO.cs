using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace _01.Code.TownPanels
{
    [Serializable]
    public class TownSkillTreeNodeEntry
    {
        [field: SerializeField] public string NodeName { get; private set; }
        [field: SerializeField] public string Requirement { get; private set; }
        [field: SerializeField] public string EffectDescription { get; private set; }
        [field: SerializeField] public Vector2 CanvasPosition { get; private set; }
        [field: SerializeField] public List<string> NextNodeNames { get; private set; } = new();
    }

    [CreateAssetMenu(fileName = "TownSkillTreeSection", menuName = "SO/Town/Panel Section/Skill Tree", order = 1)]
    public class TownSkillTreePanelSectionSO : TownObjectPanelSectionSO
    {
        [field: SerializeField] public string Summary { get; private set; }
        [field: SerializeField] public List<TownSkillTreeNodeEntry> Nodes { get; private set; } = new();

        public override string GetBodyText()
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                builder.AppendLine(Summary);
            }

            for (int i = 0; i < Nodes.Count; i++)
            {
                TownSkillTreeNodeEntry node = Nodes[i];
                if (node == null || string.IsNullOrWhiteSpace(node.NodeName))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(node.NodeName);
                if (!string.IsNullOrWhiteSpace(node.Requirement))
                {
                    builder.Append(" -> ");
                    builder.Append(node.Requirement);
                }

                if (!string.IsNullOrWhiteSpace(node.EffectDescription))
                {
                    builder.AppendLine();
                    builder.Append(node.EffectDescription);
                }
            }

            return builder.ToString();
        }
    }
}
