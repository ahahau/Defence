using _01.Code.Buildings;
using _01.Code.Units;
using System.Collections.Generic;
using System.Linq;
using TMPro;
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
        private GameObject lockedRoot;

        [SerializeField]
        private SpriteRenderer lockedOverlayRenderer;

        [SerializeField]
        private TextMeshPro lockedCostText;

        [SerializeField]
        private Sprite unlockedSprite;

        [SerializeField]
        private Sprite lockedCandidateSprite;

        [SerializeField]
        private Vector3 unlockedSpriteScale = new(1f, 1.6666667f, 1f);

        [SerializeField]
        private Color unlockedVisualColor = Color.white;

        [SerializeField]
        private Color lockedVisualColor = new(0.45f, 0.45f, 0.45f, 1f);

        [SerializeField]
        private NodeTrapGrid trapGrid;

        /// <summary>트랩 노드 내부 격자(없으면 null). 여러 트랩을 셀에 자유 배치.</summary>
        public NodeTrapGrid TrapGrid => trapGrid != null ? trapGrid : (trapGrid = GetComponent<NodeTrapGrid>());

        [field: SerializeField]
        public Collider2D ClickCollider { get; private set; }
        
        [field:SerializeField]
        public Transform UnitPosition { get; private set; }
        
        [SerializeField]
        private Transform enemyPosition;

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
        
        

        private void Awake()
        {
            // 모든 노드가 배치 그리드를 갖도록 보장 → 트랩/건물이 셀에 분산된다(가운데 겹침 방지).
            // 프리팹에 NodeTrapGrid가 없어 그동안 단일 슬롯(정중앙) 경로로만 설치되던 문제를 해소.
            if (trapGrid == null)
                trapGrid = GetComponent<NodeTrapGrid>();
            if (trapGrid == null)
                trapGrid = gameObject.AddComponent<NodeTrapGrid>();
        }

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
            SetVisualColor(unlockedVisualColor);
            SetLockedOverlayVisible(false);
            SetLockedCostVisible(false);
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
            SetSprite(unlockedSprite);
            SetSpriteScale(unlockedSpriteScale);
            SetVisualColor(lockedVisualColor);
            SetLockedOverlayVisible(true);
            SetLockedCostVisible(false);
        }

        public void SetBuildCost(int goldCost)
        {
            var costText = ResolveLockedCostText();
            if (costText == null)
                return;

            costText.text = $"{goldCost}G";
            SetLockedCostVisible(true);
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

        public bool TryAssignUnit(UnitDataSO unit, Unit unitInstance)
        {
            if (unit == null || unitInstance == null || HasInstallation)
                return false;

            AssignUnit(unit, unitInstance);
            return true;
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

        private void SetVisualColor(Color color)
        {
            if (spriteRenderer == null)
                return;

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

        private void SetLockedOverlayVisible(bool visible)
        {
            SetLockedRootVisible(visible);

            if (lockedOverlayRenderer == null)
                return;

            lockedOverlayRenderer.gameObject.SetActive(visible);
            lockedOverlayRenderer.enabled = visible;
        }

        private void SetLockedCostVisible(bool visible)
        {
            if (lockedCostText == null)
                return;

            lockedCostText.gameObject.SetActive(visible);
        }

        private void SetLockedRootVisible(bool visible)
        {
            if (lockedRoot != null)
                lockedRoot.SetActive(visible);
        }

        private TextMeshPro ResolveLockedCostText()
        {
            if (lockedCostText != null)
                return lockedCostText;

            Debug.LogError($"{nameof(Node)} requires a locked cost text assigned in the node prefab.", this);
            return null;
        }

        private Vector3 ResolvePrefabScale()
        {
            if (hasCapturedPrefabScale)
                return prefabScale;

            prefabScale = transform.localScale;
            hasCapturedPrefabScale = true;
            return prefabScale;
        }

        private void OnDestroy()
        {
            if (Data != null)
                nodesByDataId.Remove(Data.Id);
        }
    }
}
