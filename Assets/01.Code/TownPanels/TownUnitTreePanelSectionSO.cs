using System;
using System.Collections.Generic;
using System.Text;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.TownPanels
{
    [Serializable]
    public class TownUnitTreeNodeEntry
    {
        [field: SerializeField] public string NodeId { get; private set; }
        [field: SerializeField] public UnitDataSO UnitData { get; private set; }
        [field: SerializeField] public string Requirement { get; private set; }
        [field: SerializeField] public string DescriptionOverride { get; private set; }
        [field: SerializeField] public Vector2 CanvasPosition { get; private set; }
        [field: SerializeField] public List<string> NextNodeIds { get; private set; } = new();

        public string GetNodeId()
        {
            if (!string.IsNullOrWhiteSpace(NodeId))
            {
                return NodeId;
            }

            if (UnitData != null && !string.IsNullOrWhiteSpace(UnitData.name))
            {
                return UnitData.name;
            }

            return string.Empty;
        }

        public string GetDisplayName()
        {
            if (UnitData != null && !string.IsNullOrWhiteSpace(UnitData.Name))
            {
                return UnitData.Name;
            }

            if (!string.IsNullOrWhiteSpace(NodeId))
            {
                return NodeId;
            }

            return string.Empty;
        }

        public string GetDescription()
        {
            if (!string.IsNullOrWhiteSpace(DescriptionOverride))
            {
                return DescriptionOverride;
            }

            return UnitData != null ? UnitData.Explanation : string.Empty;
        }

        public Sprite GetIcon()
        {
            return UnitData != null ? UnitData.CardIcon : null;
        }

        public Color GetTint()
        {
            return UnitData != null ? UnitData.CardColor : Color.white;
        }
    }

    [Serializable]
    public class TownUnitTreeConnectionEntry
    {
        [field: SerializeField] public string FromNodeId { get; private set; }
        [field: SerializeField] public string ToNodeId { get; private set; }
        [field: SerializeField] public float BendOffset { get; private set; } = 120f;

        public TownUnitTreeConnectionEntry()
        {
        }

        public TownUnitTreeConnectionEntry(string fromNodeId, string toNodeId, float bendOffset)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            BendOffset = bendOffset;
        }
    }

    [CreateAssetMenu(fileName = "TownUnitTreeSection", menuName = "SO/Town/Panel Section/Unit Tree", order = 2)]
    public class TownUnitTreePanelSectionSO : TownObjectPanelSectionSO
    {
        [field: SerializeField] public string Summary { get; private set; }
        [field: SerializeField] public List<TownUnitTreeNodeEntry> Nodes { get; private set; } = new();
        [field: SerializeField] public List<TownUnitTreeConnectionEntry> Connections { get; private set; } = new();

        public override string GetBodyText()
        {
            StringBuilder builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                builder.AppendLine(Summary);
            }

            if (Nodes != null && Nodes.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("[Unit Tree]");
                for (int i = 0; i < Nodes.Count; i++)
                {
                    TownUnitTreeNodeEntry node = Nodes[i];
                    if (node == null)
                    {
                        continue;
                    }

                    string nodeName = node.GetDisplayName();
                    if (string.IsNullOrWhiteSpace(nodeName))
                    {
                        nodeName = node.GetNodeId();
                    }

                    if (string.IsNullOrWhiteSpace(nodeName))
                    {
                        nodeName = $"Node {i + 1}";
                    }

                    builder.Append("- ");
                    builder.Append(nodeName);

                    if (!string.IsNullOrWhiteSpace(node.Requirement))
                    {
                        builder.Append(" [Req: ");
                        builder.Append(node.Requirement);
                        builder.Append(']');
                    }

                    builder.AppendLine();

                    for (int nextIndex = 0; nextIndex < node.NextNodeIds.Count; nextIndex++)
                    {
                        string nextId = node.NextNodeIds[nextIndex];
                        if (string.IsNullOrWhiteSpace(nextId))
                        {
                            continue;
                        }

                        builder.Append("  -> ");
                        builder.AppendLine(nextId);
                    }
                }
            }

            return builder.ToString();
        }
    }
}
