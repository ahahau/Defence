using _01.Code.Buildings;
using _01.Code.BT;
using _01.Code.Units;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    [RequireComponent(typeof(NodeTrapGrid), typeof(NodeBattlefield))]
    public class Node : MonoBehaviour
    {
        private static readonly Dictionary<string, Node> nodesByDataId = new();
        private static readonly HashSet<Node> allInstances = new();
        public static IEnumerable<Node> ActiveNodes => nodesByDataId.Values.Where(node => node != null);
        public static IEnumerable<Node> AllInstances => allInstances.Where(node => node != null);

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

        [Header("Unit Capacity Label")]
        [SerializeField] private TextMeshPro unitCapacityText;
        [SerializeField] private string unitCapacityFormat = "{0}/{1}";
        [SerializeField] private Color availableCapacityColor = new(0.78f, 0.96f, 1f, 1f);
        [SerializeField] private Color fullCapacityColor = new(1f, 0.72f, 0.24f, 1f);

        /// <summary>트랩 노드 내부 격자(없으면 null). 여러 트랩을 셀에 자유 배치.</summary>
        public NodeTrapGrid TrapGrid => trapGrid != null ? trapGrid : (trapGrid = GetComponent<NodeTrapGrid>());

        /// <summary>
        /// 이 노드에 선 금고를 훑는다. 금고는 칸 건물이라 한 노드에 여럿 설 수 있다 —
        /// 정산·약탈·경로 판정이 저마다 중앙 슬롯만 보다가 금고 기능이 통째로 죽어 있었다.
        /// </summary>
        public IEnumerable<Treasury> EnumerateTreasuries()
        {
            var grid = TrapGrid;
            if (grid == null)
                yield break;

            var placed = grid.PlacedBuildings;
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i] is Treasury treasury && !treasury.IsDestroyed)
                    yield return treasury;
            }
        }

        /// <summary>보관 금화가 남은 금고 하나. 경로 판정이 노드마다 부르므로 할당 없이 돈다.</summary>
        public Treasury FindTreasuryWithGold()
        {
            var grid = TrapGrid;
            if (grid == null)
                return null;

            var placed = grid.PlacedBuildings;
            for (var i = 0; i < placed.Count; i++)
            {
                if (placed[i] is Treasury treasury && !treasury.IsDestroyed && treasury.StoredGold > 0)
                    return treasury;
            }

            return null;
        }

        /// <summary>권능으로 걸린 한시적 봉쇄가 끝나는 시각. 타이머를 돌리지 않으려고 시각으로 들고 있다.</summary>
        private float _blockedUntil;

        /// <summary>권능으로 지금 막혀 있는가.</summary>
        public bool IsTemporarilyBlocked => _blockedUntil > Time.time;

        /// <summary>한시적 봉쇄가 풀리기까지 남은 시간(초).</summary>
        public float BlockRemaining => Mathf.Max(0f, _blockedUntil - Time.time);

        /// <summary>이 노드를 정해진 시간 동안 통행 불가로 만든다. 이미 걸려 있으면 더 긴 쪽이 남는다.</summary>
        public void BlockTemporarily(float duration)
        {
            if (duration <= 0f)
                return;

            _blockedUntil = Mathf.Max(_blockedUntil, Time.time + duration);
        }

        /// <summary>벽이 세워져 있는가. 벽은 칸 건물이라 격자만 본다.</summary>
        public bool HasWall
        {
            get
            {
                var grid = TrapGrid;
                if (grid == null)
                    return false;

                var placed = grid.PlacedBuildings;
                for (var i = 0; i < placed.Count; i++)
                {
                    if (placed[i] is Wall)
                        return true;
                }

                return false;
            }
        }

        /// <summary>벽이 설치되었거나 권능으로 막혀 적이 지나갈 수 없는 노드인지.
        /// A*와 랜덤 배회, 침입 경로 예측이 모두 이 값을 본다.</summary>
        public bool IsPassBlocked => IsTemporarilyBlocked || HasWall;

        /// <summary>데이터 ID로 활성 노드를 찾는다(경로 탐색용). 없으면 null.</summary>
        public static Node FindByDataId(string dataId)
        {
            if (string.IsNullOrEmpty(dataId))
                return null;

            return nodesByDataId.TryGetValue(dataId, out var node) && node != null ? node : null;
        }

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
        private NodeBattlefield battlefield;
        private int lastDisplayedUnitCount = -1;
        private int lastDisplayedUnitCapacity = -1;
        private readonly List<UnitPlacement> unitPlacements = new();

        public sealed class UnitPlacement
        {
            public UnitPlacement(UnitDataSO data, Unit instance, int column, int row)
            {
                Data = data;
                Instance = instance;
                Column = column;
                Row = row;
            }

            public UnitDataSO Data { get; }
            public Unit Instance { get; }
            public int Column { get; set; }
            public int Row { get; set; }
        }
        
        public DungeonNode Data { get; private set; }
        public DungeonNode FromNode { get; private set; }
        public Vector2Int GridPosition { get; private set; }
        public Vector2Int Direction { get; private set; }
        public UnitDataSO AssignedUnit { get; private set; }
        public Unit AssignedUnitInstance { get; private set; }
        public IReadOnlyList<UnitPlacement> UnitPlacements => unitPlacements;
        public int AssignedUnitCount => unitPlacements.Count;
        public Building AssignedBuilding { get; private set; }
        public Transform EnemyPosition => enemyPosition != null ? enemyPosition : transform;
        public bool HasAssignedUnit => AssignedUnit != null || AssignedUnitInstance != null;
        public bool HasCombatReadyUnit
        {
            get
            {
                for (var i = 0; i < unitPlacements.Count; i++)
                {
                    if (unitPlacements[i]?.Instance != null && unitPlacements[i].Instance.CanFight)
                        return true;
                }

                return false;
            }
        }

        public Unit FirstCombatReadyUnit
        {
            get
            {
                for (var i = 0; i < unitPlacements.Count; i++)
                {
                    if (unitPlacements[i]?.Instance != null && unitPlacements[i].Instance.CanFight)
                        return unitPlacements[i].Instance;
                }

                return null;
            }
        }
        public bool HasAssignedBuilding => AssignedBuilding != null;
        public bool HasInstallation => HasAssignedUnit || HasAssignedBuilding;
        /// <summary>
        /// 침입자가 쏟아져 나오는 방인가. 포탈이 선 노드가 곧 스폰 지점이다.
        /// </summary>
        public bool IsEnemySpawnNode => AssignedBuilding is Portal;

        /// <summary>
        /// 스폰 방에는 수비대를 세울 수 없다. 거기서 막아 세우면 적이 통로를 한 번도 지나지 않아
        /// 통로 함정이 통째로 죽고, 함정을 지을 이유가 사라진다.
        /// </summary>
        public bool CanAcceptAdditionalUnit => !IsEnemySpawnNode && AssignedUnitCount < UnitCapacity;
        public int UnitCapacity => battlefield != null ? battlefield.MaxPerTeam : 1;
        /// <summary>이 구역에서 벌어진 일로 쌓인 악명(전투·함정 발동). 설치물의 위험도는 포함하지 않는다.</summary>
        public int DangerLevel { get; private set; }
        
        

        private void Awake()
        {
            trapGrid ??= GetComponent<NodeTrapGrid>();
            battlefield = GetComponent<NodeBattlefield>();
            if (trapGrid == null || battlefield == null)
            {
                Debug.LogError($"{nameof(Node)} prefab requires {nameof(NodeTrapGrid)} and {nameof(NodeBattlefield)} components.", this);
                enabled = false;
                return;
            }

            RefreshUnitCapacityLabel(true);
        }

        private void OnEnable()
        {
            allInstances.Add(this);
        }

        private void OnDisable()
        {
            allInstances.Remove(this);
        }

        private void Update()
        {
            RefreshUnitCapacityLabel();
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
            SetUnitCapacityVisible(true);
            RefreshUnitCapacityLabel(true);
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
            SetUnitCapacityVisible(false);
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
        }

        public void AssignUnit(UnitDataSO unit, Unit unitInstance)
        {
            if (unitInstance != null && TryFindFirstFreeUnitCell(out var column, out var row))
            {
                TryAssignUnitToCell(unit, unitInstance, column, row);
                return;
            }

            AssignedUnit = unit;
            AssignedUnitInstance = unitInstance;
        }

        public bool TryAssignUnit(UnitDataSO unit, Unit unitInstance)
        {
            if (unit == null || unitInstance == null || !TryFindFirstFreeUnitCell(out var column, out var row))
                return false;

            return TryAssignUnitToCell(unit, unitInstance, column, row);
        }

        public bool TryAssignUnitToCell(UnitDataSO unit, Unit unitInstance, int column, int row)
        {
            if (unitInstance == null || !CanAcceptAdditionalUnit || !IsUnitCellAvailable(column, row))
                return false;

            unitPlacements.Add(new UnitPlacement(unit, unitInstance, column, row));
            unitInstance.transform.position = TrapGrid.CellWorldPosition(column, row);
            RefreshPrimaryUnit();
            return true;
        }

        public bool IsUnitCellAvailable(int column, int row)
        {
            var grid = TrapGrid;
            if (grid == null || !grid.IsValidCell(column, row))
                return false;

            if (grid.IsCentralBuildingSlotCell(column, row))
                return false;

            for (var i = 0; i < unitPlacements.Count; i++)
            {
                var placement = unitPlacements[i];
                if (placement != null && placement.Column == column && placement.Row == row && placement.Instance != null)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 유닛 배치는 격자 입력을 요구하지 않는다. 노드가 내부적으로 사용할 빈 슬롯만 반환한다.
        /// 건물/함정 셀 점유와는 독립적이며, 노드 정원과 기존 유닛만 검사한다.
        /// </summary>
        public bool TryGetFirstFreeUnitSlot(out int column, out int row)
        {
            return TryFindFirstFreeUnitCell(out column, out row);
        }

        public bool TryGetUnitAtCell(int column, int row, out UnitPlacement result)
        {
            for (var i = 0; i < unitPlacements.Count; i++)
            {
                var placement = unitPlacements[i];
                if (placement != null && placement.Column == column && placement.Row == row && placement.Instance != null)
                {
                    result = placement;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public bool TryMoveUnitToCell(Unit unitInstance, int column, int row)
        {
            if (unitInstance == null || !IsUnitCellAvailable(column, row))
                return false;

            for (var i = 0; i < unitPlacements.Count; i++)
            {
                var placement = unitPlacements[i];
                if (placement?.Instance != unitInstance)
                    continue;

                placement.Column = column;
                placement.Row = row;
                unitInstance.transform.position = TrapGrid.CellWorldPosition(column, row);
                return true;
            }

            return false;
        }

        public bool RemoveUnit(Unit unitInstance)
        {
            for (var i = unitPlacements.Count - 1; i >= 0; i--)
            {
                if (unitPlacements[i]?.Instance != unitInstance)
                    continue;

                unitPlacements.RemoveAt(i);
                RefreshPrimaryUnit();
                return true;
            }

            return false;
        }

        public bool TryGetPlacement(Unit unitInstance, out UnitPlacement result)
        {
            for (var i = 0; i < unitPlacements.Count; i++)
            {
                var placement = unitPlacements[i];
                if (placement?.Instance != unitInstance)
                    continue;

                result = placement;
                return true;
            }

            result = null;
            return false;
        }

        public static bool TryFindUnit(Unit unitInstance, out Node node, out UnitPlacement placement)
        {
            if (unitInstance != null)
            {
                foreach (var candidate in ActiveNodes)
                {
                    if (candidate != null && candidate.TryGetPlacement(unitInstance, out placement))
                    {
                        node = candidate;
                        return true;
                    }
                }
            }

            node = null;
            placement = null;
            return false;
        }

        public void ClearUnit()
        {
            if (AssignedUnitInstance != null && RemoveUnit(AssignedUnitInstance))
                return;

            AssignedUnit = null;
            AssignedUnitInstance = null;
        }

        private bool TryFindFirstFreeUnitCell(out int column, out int row)
        {
            for (var r = 0; r < TrapGrid.Rows; r++)
            for (var c = 0; c < TrapGrid.Columns; c++)
            {
                if (!IsUnitCellAvailable(c, r))
                    continue;

                column = c;
                row = r;
                return true;
            }

            column = -1;
            row = -1;
            return false;
        }

        private void RefreshPrimaryUnit()
        {
            for (var i = unitPlacements.Count - 1; i >= 0; i--)
            {
                if (unitPlacements[i]?.Instance == null)
                    unitPlacements.RemoveAt(i);
            }

            var primary = unitPlacements.Count > 0 ? unitPlacements[0] : null;
            AssignedUnit = primary?.Data;
            AssignedUnitInstance = primary?.Instance;
        }

        public void AssignBuilding(Building building)
        {
            AssignedBuilding = building;
        }

        public void ClearBuilding()
        {
            AssignedBuilding = null;
        }

        public bool DamageAssignedBuilding(int damage)
        {
            if (AssignedBuilding == null || !AssignedBuilding.IsDestructible)
                return false;

            var building = AssignedBuilding;
            var destroyed = building.TakeBuildingDamage(damage);
            if (destroyed && AssignedBuilding == building)
                ClearBuilding();

            return true;
        }

        /// <summary>
        /// 이 구역에서 실제로 벌어진 일로 악명을 쌓는다 — 함정이 터지고 부하가 맞붙을 때만 오른다.
        /// 설치한 것들의 위험도는 악명 패널이 재고를 훑어 따로 세므로 여기에 더하면 이중 계산이 되고,
        /// 유닛을 이 노드 저 노드로 옮기는 것만으로 악명을 불릴 수 있게 된다.
        /// </summary>
        public void IncreaseDanger(int amount)
        {
            if (amount <= 0)
                return;

            DangerLevel += amount;
        }

        public void RestoreDanger(int amount)
        {
            DangerLevel = Mathf.Max(0, amount);
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

        private void SetUnitCapacityVisible(bool visible)
        {
            if (unitCapacityText != null)
                unitCapacityText.gameObject.SetActive(visible);
        }

        private void RefreshUnitCapacityLabel(bool force = false)
        {
            if (unitCapacityText == null)
                return;

            battlefield ??= GetComponent<NodeBattlefield>();
            var deployed = AssignedUnitCount;
            var capacity = battlefield != null ? battlefield.MaxPerTeam : 1;
            if (!force && deployed == lastDisplayedUnitCount && capacity == lastDisplayedUnitCapacity)
                return;

            lastDisplayedUnitCount = deployed;
            lastDisplayedUnitCapacity = capacity;
            unitCapacityText.text = string.Format(unitCapacityFormat, deployed, capacity);
            unitCapacityText.color = deployed >= capacity ? fullCapacityColor : availableCapacityColor;
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
            allInstances.Remove(this);
            if (Data != null)
                nodesByDataId.Remove(Data.Id);
        }
    }
}
