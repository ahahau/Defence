using _01.Code.Buildings;
using _01.Code.Units;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    public class Node : MonoBehaviour
    {
        private static readonly Dictionary<string, Node> nodesByDataId = new();
        public static IEnumerable<Node> ActiveNodes => nodesByDataId.Values.Where(node => node != null);

        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [SerializeField]
        private Sprite unlockedSprite;

        [SerializeField]
        private Sprite lockedCandidateSprite;

        [SerializeField]
        private Vector3 unlockedSpriteScale = new(1f, 1.6666667f, 1f);

        [SerializeField]
        private Vector3 lockedCandidateSpriteScale = Vector3.one;

        [field: SerializeField]
        public Collider2D ClickCollider { get; private set; }
        
        [field:SerializeField]
        public Transform UnitPosition { get; private set; }
        
        [SerializeField]
        private Transform enemyPosition;

        [SerializeField, Range(0.1f, 1f)]
        private float lockedVisualAlpha = 0.35f;

        [SerializeField, Range(0.1f, 1f)]
        private float lockedVisualScale = 0.72f;

        private Vector3 prefabScale = Vector3.one;
        private bool hasCapturedPrefabScale;
        
        public DungeonNode Data { get; private set; }
        public DungeonNode FromNode { get; private set; }
        public Vector2Int GridPosition { get; private set; }
        public Vector2Int Direction { get; private set; }
        public UnitDataSO AssignedUnit { get; private set; }
        public Unit AssignedUnitInstance { get; private set; }
        public Building AssignedBuilding { get; private set; }
        public Transform EnemyPosition => enemyPosition != null ? enemyPosition : transform;
        public bool HasAssignedUnit => AssignedUnit != null || AssignedUnitInstance != null;
        public bool HasCombatReadyUnit => AssignedUnitInstance != null && AssignedUnitInstance.CanFight;
        public bool HasAssignedBuilding => AssignedBuilding != null;
        public bool HasInstallation => HasAssignedUnit || HasAssignedBuilding;
        public int DangerLevel { get; private set; }
        
        

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
            transform.localScale = ResolvePrefabScale() * size;
            DangerLevel = 0;
            SetSprite(unlockedSprite);
            SetSpriteScale(unlockedSpriteScale);
            SetVisualAlpha(1f);
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
            transform.localScale = ResolvePrefabScale() * size * lockedVisualScale;
            SetSprite(lockedCandidateSprite);
            SetSpriteScale(lockedCandidateSpriteScale);
            SetVisualAlpha(lockedVisualAlpha);
        }

        public void ShowClickFeedback()
        {
            
        }

        public void AssignUnit(UnitDataSO unit)
        {
            AssignedUnit = unit;
            IncreaseDanger(unit != null ? unit.BaseDanger : 0);
        }

        public void AssignUnit(UnitDataSO unit, Unit unitInstance)
        {
            AssignedUnit = unit;
            AssignedUnitInstance = unitInstance;
            IncreaseDanger(unit != null ? unit.BaseDanger : 0);
        }

        public void ClearUnit()
        {
            AssignedUnit = null;
            AssignedUnitInstance = null;
        }

        public void AssignBuilding(Building building)
        {
            AssignedBuilding = building;
            IncreaseDanger(building != null ? building.DangerRating : 0);
        }

        public void ClearBuilding()
        {
            AssignedBuilding = null;
        }

        public void IncreaseDanger(int amount)
        {
            if (amount <= 0)
                return;

            DangerLevel += amount;
        }

        private void SetVisualAlpha(float alpha)
        {
            var color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        private void SetSprite(Sprite sprite)
        {
            if (spriteRenderer != null && sprite != null)
                spriteRenderer.sprite = sprite;
        }

        private void SetSpriteScale(Vector3 scale)
        {
            if (spriteRenderer != null)
                spriteRenderer.transform.localScale = scale;
        }

        private Vector3 ResolvePrefabScale()
        {
            if (hasCapturedPrefabScale)
                return prefabScale;

            prefabScale = transform.localScale;
            hasCapturedPrefabScale = true;
            return prefabScale;
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
