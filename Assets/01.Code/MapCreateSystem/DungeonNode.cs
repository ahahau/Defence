using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    [Serializable]
    public class DungeonNode
    {
        [field: SerializeField]
        public string Id { get; private set; }

        [field: SerializeField]
        public DungeonNodeType Type { get; private set; }

        [field: SerializeField]
        public Vector2Int GridPosition { get; private set; }

        [field: SerializeField]
        public int MaxConnections { get; private set; }

        [field: SerializeField]
        public List<string> ConnectedNodeIds { get; private set; } = new();

        public int FreePorts => Mathf.Max(0, MaxConnections - ConnectedNodeIds.Count);

        public DungeonNode(DungeonNodeType type, Vector2Int gridPosition, int maxConnections)
        {
            Id = Guid.NewGuid().ToString("N");
            Type = type;
            GridPosition = gridPosition;
            MaxConnections = maxConnections;
        }

        public bool Connect(DungeonNode other)
        {
            if (other.Id == Id || ConnectedNodeIds.Contains(other.Id))
                return false;

            ConnectedNodeIds.Add(other.Id);
            return true;
        }
    }
}
