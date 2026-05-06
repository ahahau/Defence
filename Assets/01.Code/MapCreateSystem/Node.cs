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
        private SpriteRenderer lockedOverlayRenderer;

        [SerializeField]
        private Sprite unlockedSprite;

        [SerializeField]
        private Sprite lockedCandidateSprite;

        [SerializeField]
        private Vector3 unlockedSpriteScale = new(1f, 1.6666667f, 1f);

        [SerializeField]
        private Vector3 lockedOverlayLocalScale = new(1f, 1.25f, 1f);

        [SerializeField]
        private Color unlockedVisualColor = Color.white;

        [SerializeField]
        private Color lockedVisualColor = new(0.45f, 0.45f, 0.45f, 1f);

        [field: SerializeField]
        public Collider2D ClickCollider { get; private set; }
        
        [field:SerializeField]
        public Transform UnitPosition { get; private set; }
        
        [SerializeField]
        private Transform enemyPosition;

        [SerializeField, Range(0.1f, 1f)]
        private float lockedVisualAlpha = 1f;

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
            SetVisualColor(unlockedVisualColor);
            SetLockedOverlayVisible(false);
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
            var overlay = ResolveLockedOverlayRenderer();
            if (overlay == null)
                return;

            overlay.enabled = visible && lockedCandidateSprite != null;
            overlay.sprite = lockedCandidateSprite;
            overlay.color = new Color(1f, 1f, 1f, lockedVisualAlpha);
            overlay.transform.localScale = lockedOverlayLocalScale;
        }

        private SpriteRenderer ResolveLockedOverlayRenderer()
        {
            if (lockedOverlayRenderer != null)
                return lockedOverlayRenderer;

            var overlayObject = new GameObject("LockedOverlay");
            overlayObject.transform.SetParent(transform);
            overlayObject.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;

            lockedOverlayRenderer = overlayObject.AddComponent<SpriteRenderer>();
            lockedOverlayRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            lockedOverlayRenderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 1;
            return lockedOverlayRenderer;
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
