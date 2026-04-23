using _01.Code.Buildings;
using _01.Code.Units;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class Node : MonoBehaviour
    {
        private static readonly Dictionary<string, Node> nodesByDataId = new();

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [field: SerializeField]
        public Collider2D ClickCollider { get; private set; }
        
        [field:SerializeField]
        public Transform UnitPosition { get; private set; }
        
        [SerializeField]
        private Transform enemyPosition;
        
        public DungeonNode Data { get; private set; }
        public DungeonNode FromNode { get; private set; }
        public Vector2Int GridPosition { get; private set; }
        public Vector2Int Direction { get; private set; }
        public UnitDataSO AssignedUnit { get; private set; }
        public Unit AssignedUnitInstance { get; private set; }
        public Building AssignedBuilding { get; private set; }
        public Transform EnemyPosition => enemyPosition != null ? enemyPosition : transform;
        public bool HasAssignedUnit => AssignedUnit != null;
        public bool HasAssignedBuilding => AssignedBuilding != null;
        
        

        public void Initialize(DungeonNode data, float size)
        {
            Unlock(data, size);
        }

        public void Unlock(DungeonNode data, float size)
        {
            Data = data;
            FromNode = data;
            GridPosition = data.GridPosition;
            name = $"Node_{data.Type}_{data.GridPosition.x}_{data.GridPosition.y}";
            transform.localScale *= size;
            spriteRenderer.color = Color.white;
            nodesByDataId[data.Id] = this;
        }

        public void InitializeBuildCandidate(
            DungeonNode fromNode,
            Vector2Int gridPosition,
            Vector2Int direction,
            float size)
        {
            FromNode = fromNode;
            GridPosition = gridPosition;
            Direction = direction;

            name = $"LockedNode_{gridPosition.x}_{gridPosition.y}";
            transform.localScale *= size;
        }

        public void ShowClickFeedback()
        {
            
        }

        public void AssignUnit(UnitDataSO unit)
        {
            AssignedUnit = unit;
        }

        public void AssignUnit(UnitDataSO unit, Unit unitInstance)
        {
            AssignedUnit = unit;
            AssignedUnitInstance = unitInstance;
        }

        public void ClearUnit()
        {
            AssignedUnit = null;
            AssignedUnitInstance = null;
        }

        public void AssignBuilding(Building building)
        {
            AssignedBuilding = building;
        }

        public void ClearBuilding()
        {
            AssignedBuilding = null;
        }

        public static bool TryGetByDataId(string dataId, out Node node)
        {
            return nodesByDataId.TryGetValue(dataId, out node);
        }

        private void OnDestroy()
        {
            if (Data != null)
                nodesByDataId.Remove(Data.Id);
        }
    }
}
