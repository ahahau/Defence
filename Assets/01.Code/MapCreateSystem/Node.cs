using Code.Buildings;
using Code.BT;
using Code.Units;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Code.MapCreateSystem
{
    [RequireComponent(typeof(NodeTrapGrid), typeof(NodeBattlefield))]
    public class Node : MonoBehaviour
    {
        private static readonly Dictionary<string, Node> nodesByDataId = new();
        private static readonly HashSet<Node> allInstances = new();
        public static IEnumerable<Node> ActiveNodes => nodesByDataId.Values.Where(node => node != null);
        public static IEnumerable<Node> AllInstances => allInstances.Where(node => node != null);

        [SerializeField] private NodeTileRenderer tileRenderer;

        [SerializeField]
        private GameObject lockedRoot;

        [SerializeField]
        private TextMeshPro lockedCostText;

        [SerializeField]
        private Color unlockedVisualColor = Color.white;

        [SerializeField]
        private Color lockedVisualColor = new(0.45f, 0.45f, 0.45f, 1f);

        [SerializeField]
        private NodeTrapGrid trapGrid;

        [SerializeField] private Transform entryDoorSpawnPoint;
        public Transform EntryDoorSpawnPoint => entryDoorSpawnPoint;

        [Header("Unit Capacity Label")]
        [SerializeField] private TextMeshPro unitCapacityText;
        [SerializeField] private string unitCapacityFormat = "{0}/{1}";
        [SerializeField] private Color availableCapacityColor = new(0.78f, 0.96f, 1f, 1f);
        [SerializeField] private Color fullCapacityColor = new(1f, 0.72f, 0.24f, 1f);

        /// <summary>트랩 노드 내부 격자(없으면 null). 여러 트랩을 셀에 자유 배치.</summary>
        public NodeTrapGrid TrapGrid => trapGrid != null ? trapGrid : (trapGrid = GetComponent<NodeTrapGrid>());

        /// <summary>
        /// 이 노드의 중앙과 개별 칸에 있는 금고를 모두 훑는다.
        /// </summary>
        public IEnumerable<Treasury> EnumerateTreasuries()
        {
            if (AssignedBuilding is Treasury central && !central.IsDestroyed)
                yield return central;

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
            if (AssignedBuilding is Treasury central && !central.IsDestroyed && central.StoredGold > 0)
                return central;

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

        /// <summary>벽이 설치되었거나 권능으로 막혔거나 공사 중이라 적이 지나갈 수 없는 노드인지.
        /// A*와 랜덤 배회, 침입 경로 예측이 모두 이 값을 본다.</summary>
        public bool IsPassBlocked => IsTemporarilyBlocked || HasWall || IsUnderConstruction;

        private float _constructionRemaining;

        /// <summary>
        /// 공사 중인가. 공사 중인 방은 없는 길처럼 다룬다 — 아군도 파티도 지나가지 못하고,
        /// 유닛·건물을 둘 수 없다. 파티는 기존 길찾기(<see cref="IsPassBlocked"/>)로 돌아간다.
        /// </summary>
        public bool IsUnderConstruction => _constructionRemaining > 0f;

        /// <summary>완공까지 남은 게임 시간(초).</summary>
        public float ConstructionRemaining => _constructionRemaining;

        /// <summary>이 공사에 실제로 낸 금화. 취소 환불의 기준이다.</summary>
        public int ConstructionPaidGold { get; private set; }

        /// <summary>공사가 끝나 방을 쓸 수 있게 되었을 때.</summary>
        public event Action<Node> ConstructionCompleted;

        /// <summary>공사를 시작한다. 게임 시간으로 흐르므로 일시정지 중에는 멈춘다.</summary>
        public void BeginConstruction(float seconds, int paidGold)
        {
            if (seconds <= 0f)
                return;

            _constructionRemaining = seconds;
            ConstructionPaidGold = Mathf.Max(0, paidGold);
            SetVisualColor(lockedVisualColor);
            SetUnitCapacityVisible(false);
            RefreshConstructionLabel();
        }

        private void TickConstruction()
        {
            if (_constructionRemaining <= 0f)
                return;

            _constructionRemaining = Mathf.Max(0f, _constructionRemaining - Time.deltaTime);
            if (_constructionRemaining > 0f)
            {
                RefreshConstructionLabel();
                return;
            }

            ConstructionPaidGold = 0;
            SetVisualColor(unlockedVisualColor);
            SetLockedCostVisible(false);
            SetUnitCapacityVisible(true);
            RefreshUnitCapacityLabel(true);
            ConstructionCompleted?.Invoke(this);
        }

        private void RefreshConstructionLabel()
        {
            var label = ResolveLockedCostText();
            if (label == null)
                return;

            label.text = $"공사 중 {Mathf.CeilToInt(_constructionRemaining)}초";
            SetLockedCostVisible(true);
        }

        private float _cleanupRemaining;

        /// <summary>
        /// 시설을 허문 뒤 정리 중인가. 정리가 끝나야 중앙 시설을 새로 지을 수 있다.
        /// 허물고 바로 다른 시설을 올리면 철거 환불이 사실상 공짜 교체가 된다.
        /// </summary>
        public bool IsClearingRubble => _cleanupRemaining > 0f;

        /// <summary>정리가 끝날 때까지 남은 게임 시간(초).</summary>
        public float CleanupRemaining => _cleanupRemaining;

        /// <summary>철거 뒤 정리를 시작한다. 게임 시간으로 흐른다.</summary>
        public void BeginRubbleCleanup(float seconds)
        {
            if (seconds > 0f)
                _cleanupRemaining = seconds;
        }

        private void TickCleanup()
        {
            if (_cleanupRemaining > 0f)
                _cleanupRemaining = Mathf.Max(0f, _cleanupRemaining - Time.deltaTime);
        }

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
        /// 오른쪽 바깥문을 통해 침입자가 들어오는 시작 방인가.
        /// </summary>
        public bool IsEnemySpawnNode => Data != null && Data.Type == DungeonNodeType.Entrance;

        /// <summary>
        /// 스폰 방에는 유닛을 세울 수 없다. 거기서 막아 세우면 적이 통로를 한 번도 지나지 않아
        /// 통로 함정이 통째로 죽고, 함정을 지을 이유가 사라진다.
        ///
        /// 건물이 있는 방에도 세울 수 없다. 방 하나는 건물이 쓰거나 경비가 쓰거나 둘 중 하나다 —
        /// 이것이 경영의 선택이다. 상점을 지으면 그 방은 지킬 수 없게 되고, 그 상점이
        /// 등급을 올려 다음 날 더 많이 온다. 벌수록 막을 자리가 줄어든다.
        ///
        /// 함정은 건물이 아니다. 유닛과 한 방을 나눠 쓴다 — 그래야 건물로 채운 방도
        /// 함정으로는 지킬 수 있고, 유닛을 세운 방도 함정으로 두텁게 할 수 있다.
        ///
        /// 금고도 예외다. 금고는 벌어들이는 곳이 아니라 지켜야 할 곳이라, 거기에 유닛을
        /// 못 세우면 지킬 방법 자체가 없어진다.
        /// </summary>
        public bool CanAcceptAdditionalUnit =>
            !IsEnemySpawnNode && !IsUnderConstruction && !HasInstalledBuilding && AssignedUnitCount < UnitCapacity;

        /// <summary>함정이 아닌 건물이 이 방에 있는가. 중앙 슬롯과 격자 칸을 모두 본다.</summary>
        public bool HasInstalledBuilding
        {
            get
            {
                if (IsRoomBuilding(AssignedBuilding))
                    return true;

                var grid = TrapGrid;
                if (grid == null)
                    return false;

                var placed = grid.PlacedBuildings;
                for (var i = 0; i < placed.Count; i++)
                    if (IsRoomBuilding(placed[i]))
                        return true;

                return false;
            }
        }

        // 영업 여부로 판단하지 않는다. 그러면 문 닫은 상점 자리에 유닛을 세워 두고
        // 다시 열 수 있어, 한 방이 벌이와 경비를 동시에 하게 된다.
        private static bool IsRoomBuilding(Building building) =>
            building != null && !building.IsDestroyed && building is not Treasury && building.Data != null
            && building.Data.Category == InstallCategory.Building;
        public int UnitCapacity => battlefield != null ? battlefield.MaxPerTeam : 1;
        /// <summary>이 구역에서 벌어진 일로 쌓인 악명(전투·함정 발동). 설치물의 위험도는 포함하지 않는다.</summary>
        public int DangerLevel { get; private set; }

        /// <summary>모험가 사이에 도는 이 방의 소문. 길 판단에 쓴다(<see cref="RoomRumor"/>).</summary>
        public RoomRumor Rumor { get; private set; }

        

        private void Awake()
        {
            tileRenderer ??= GetComponent<NodeTileRenderer>();

            trapGrid ??= GetComponent<NodeTrapGrid>();
            battlefield = GetComponent<NodeBattlefield>();
            if (trapGrid == null || battlefield == null)
            {
                Debug.LogError($"{nameof(Node)} prefab requires {nameof(NodeTrapGrid)} and {nameof(NodeBattlefield)} components.", this);
                enabled = false;
                return;
            }

            trapGrid.PartitionsChanged += HandlePartitionsChanged;
            RefreshUnitCapacityLabel(true);
        }

        private void OnEnable()
        {
            allInstances.Add(this);
        }

        private void OnDisable()
        {
            allInstances.Remove(this);

            // 공사 취소처럼 방이 꺼지면 던전 타일맵에서도 지운다.
            if (_dungeonTilemap != null)
                _dungeonTilemap.MarkDirty();
        }

        private void Update()
        {
            TickConstruction();
            TickCleanup();
            if (!IsUnderConstruction)
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
            FitColliderToSpacing();
            DangerLevel = 0;
            Rumor = default;
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
            // 잠긴 후보도 방 타일은 이웃과 맞닿게 두고, 자물쇠 장식만 기존 크기로 유지한다.
            transform.localScale = ResolvePrefabScale() * size;
            FitColliderToSpacing();
            if (lockedRoot != null)
                lockedRoot.transform.localScale = Vector3.one * lockedVisualScale;
            SetVisualColor(lockedVisualColor);
            SetLockedOverlayVisible(true);
            SetLockedCostVisible(false);
            SetUnitCapacityVisible(false);
        }

        public void SetBuildCost(int goldCost)
        {
            SetBuildCandidateLabel($"{goldCost}G");
        }

        /// <summary>잠긴 후보 위에 크기·비용·공사 시간(또는 필요한 등급)을 적는다.</summary>
        public void SetBuildCandidateLabel(string text)
        {
            var costText = ResolveLockedCostText();
            if (costText == null)
                return;

            costText.text = text;
            SetLockedCostVisible(true);
        }

        /// <summary>방 크기. 확장 후보에는 열었을 때의 크기, 열린 방에는 실제 크기. 처음 방들은 중형이다.</summary>
        public RoomSize Size { get; private set; } = RoomSize.Medium;

        /// <summary>크기를 정한다. 열린 방이면 팀당 정원도 크기에 맞춘다.</summary>
        public void SetRoomSize(RoomSize size)
        {
            Size = size;
            battlefield ??= GetComponent<NodeBattlefield>();
            if (battlefield != null)
                battlefield.SetMaxPerTeam(RoomSizeRules.TeamCapacity(size));
            RefreshUnitCapacityLabel(true);
        }

        /// <summary>방 특수 효과. 후보에는 열었을 때의 효과, 열린 방에는 실제 효과.</summary>
        public RoomFeature Feature { get; private set; } = RoomFeature.None;

        /// <summary>특수 효과를 정한다. 열린 방이면 정원 표시 앞에 효과 이름을 붙인다.</summary>
        public void SetRoomFeature(RoomFeature feature)
        {
            Feature = feature;
            RefreshUnitCapacityLabel(true);
            RefreshRoomVisual();
        }

        /// <summary>이 방의 모양(생성한 방 또는 특별 방 프리팹). 모양 없이 만든 노드(테스트)는 null이다.</summary>
        public RoomShape Layout { get; private set; }

        /// <summary>문이 난 변. 모양이 없으면 네 변 모두 열린 것으로 본다(예전 방과 같다).</summary>
        public RoomDoors Doors => Layout != null
            ? Layout.Doors
            : RoomDoors.Up | RoomDoors.Right | RoomDoors.Down | RoomDoors.Left;

        public bool HasDoor(RoomDoors side) => RoomDoorRules.Has(Doors, side);

        /// <summary>복도가 실제로 이어진 문.</summary>
        public RoomDoors ConnectedDoors { get; private set; }

        private int _spacingTiles = 24;
        private Color _visualColor = Color.white;
        private GameObject _decor;

        /// <summary>열린 방을 함께 그리는 던전 타일맵. 없으면(잠긴 후보·테스트) 자기 타일맵에 그린다.</summary>
        private DungeonTilemap _dungeonTilemap;

        /// <summary>
        /// 방 모양을 입힌다. 칸 수·막힌 칸·아레나 크기가 모양을 따르고, 특별 방 프리팹이면 그 자식(장식)이 방 위에 놓인다.
        /// 건물·유닛을 놓기 전에 부른다.
        /// </summary>
        public void ApplyLayout(RoomShape layout, float gridSpacing)
        {
            Layout = layout;
            _spacingWorld = gridSpacing;
            _spacingTiles = Mathf.Max(2, Mathf.RoundToInt(gridSpacing / RoomLayoutRules.TileWorldSize));
            FitColliderToSpacing();
            if (layout != null)
            {
                TrapGrid?.ConfigureCells(layout, RoomLayoutRules.CellWorldSize);
                battlefield ??= GetComponent<NodeBattlefield>();
                battlefield?.SetArenaRadius(Mathf.Max(layout.Columns, layout.Rows) * RoomLayoutRules.CellWorldSize * 0.5f);
            }

            ReplaceDecor(layout != null ? layout.Prefab : null);
            RefreshRoomVisual();
        }

        private float _spacingWorld;
        private BoxCollider2D _roomCollider;

        /// <summary>
        /// 누르기·전투 입장 판정 칸을 방 간격(월드)에 맞춘다. 노드 배율을 키우면 이름표·자물쇠까지 커지므로 칸만 늘린다.
        /// 배율이 정해진 뒤(열기·후보 만들기)에도 다시 부른다.
        /// </summary>
        private void FitColliderToSpacing()
        {
            if (_spacingWorld <= 0f)
                return;

            _roomCollider ??= GetComponent<BoxCollider2D>();
            if (_roomCollider == null)
                return;

            var scale = transform.lossyScale;
            _roomCollider.size = new Vector2(
                _spacingWorld / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                _spacingWorld / Mathf.Max(0.0001f, Mathf.Abs(scale.y)));
        }

        /// <summary>열린 방은 던전 타일맵 한 장에 함께 그린다. 자기 타일맵은 비운다.</summary>
        public void UseDungeonTilemap(DungeonTilemap tilemap)
        {
            _dungeonTilemap = tilemap;
            tileRenderer ??= GetComponent<NodeTileRenderer>();
            tileRenderer?.Clear();
            if (_dungeonTilemap != null)
                _dungeonTilemap.Register(this);
            RefreshDoorViews();
        }

        /// <summary>복도가 이어진 문을 정하고 다시 그린다. 연결이 생기거나 끊길 때 그래프 쪽이 부른다.</summary>
        public void SetConnectedDoors(RoomDoors doors)
        {
            if (ConnectedDoors == doors)
                return;

            ConnectedDoors = doors;
            RefreshRoomVisual();
        }

        /// <summary>던전 타일맵이 칠할 이 방의 몫: 바닥·기둥, 타일 좌표 오프셋, 색조.</summary>
        public bool TryBuildPaint(out RoomTilePainter.Room room)
        {
            room = default;
            if (Layout == null)
                return false;

            var position = transform.position;
            var offset = new Vector2Int(
                Mathf.RoundToInt(position.x / RoomLayoutRules.TileWorldSize),
                Mathf.RoundToInt(position.y / RoomLayoutRules.TileWorldSize));
            room = new RoomTilePainter.Room(BuildPlan(), offset, _visualColor * RoomFeatureRules.Tint(Feature));
            return true;
        }

        private RoomTilePlan BuildPlan()
        {
            var turning = Layout.IsEntrance ? RoomDoorRules.EntranceTurningDoors : RoomDoors.None;
            _partitionCells.Clear();
            TrapGrid?.CollectPartitionCells(_partitionCells);
            return RoomTilePlan.Build(Layout, ConnectedDoors, turning, _spacingTiles, _partitionCells);
        }

        /// <summary>그림을 그릴 때 칸막이 벽 칸을 모으는 임시 목록. 매번 새로 만들지 않는다.</summary>
        private readonly List<Vector2Int> _partitionCells = new();

        private void HandlePartitionsChanged() => RefreshRoomVisual();

        private void RefreshRoomVisual()
        {
            if (Layout == null)
                return;

            RefreshDoorViews();
            if (_dungeonTilemap != null)
            {
                _dungeonTilemap.MarkDirty();
                return;
            }

            tileRenderer?.Render(BuildPlan(), _visualColor * RoomFeatureRules.Tint(Feature));
        }

        /// <summary>복도가 이어진 위쪽 문에 선 문(열린 방만). 옆·아래 문은 3/4 시점에서 문 면이 보이지 않는다.</summary>
        private RoomDoorView _upDoor;

        /// <summary>그 변의 문을 연다. 문 그림이 없는 변이면 아무 일도 없다.</summary>
        public void OpenDoor(RoomDoors side)
        {
            if (side == RoomDoors.Up && _upDoor != null)
                _upDoor.Open();
        }

        private void RefreshDoorViews()
        {
            tileRenderer ??= GetComponent<NodeTileRenderer>();
            var frames = tileRenderer != null && tileRenderer.TileSet != null ? tileRenderer.TileSet.DoorFrames : null;
            var wanted = _dungeonTilemap != null && Layout != null && frames != null && frames.Length > 0
                         && Layout.HasDoor(RoomDoors.Up) && RoomDoorRules.Has(ConnectedDoors, RoomDoors.Up);
            if (!wanted)
            {
                if (_upDoor != null)
                {
                    if (Application.isPlaying)
                        Destroy(_upDoor.gameObject);
                    else
                        DestroyImmediate(_upDoor.gameObject);
                    _upDoor = null;
                }

                return;
            }

            if (_upDoor == null)
            {
                var door = new GameObject("UpDoor");
                door.transform.SetParent(transform, false);
                _upDoor = door.AddComponent<RoomDoorView>();
                var walls = tileRenderer.TileRenderer;
                _upDoor.Configure(frames, walls != null ? walls.sortingLayerID : 0, (walls != null ? walls.sortingOrder : 1) + 1);
            }

            // 문 그림은 타일 3칸 × 2칸(16px = 타일 하나)이고, 위쪽 벽 벽돌 면 두 줄에 걸친다.
            var parentScale = transform.lossyScale;
            _upDoor.transform.localScale = new Vector3(
                RoomLayoutRules.TileWorldSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                RoomLayoutRules.TileWorldSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);
            var top = Layout.Rows * RoomLayoutRules.CellWorldSize * 0.5f;
            _upDoor.transform.position = transform.position + new Vector3(0f, top + RoomLayoutRules.TileWorldSize, -0.01f);
        }

        private void ReplaceDecor(RoomLayout prefab)
        {
            if (_decor != null)
            {
                if (Application.isPlaying)
                    Destroy(_decor);
                else
                    DestroyImmediate(_decor);
                _decor = null;
            }

            // 장식이 없는 프리팹은 데이터만 쓴다. 빈 오브젝트를 방마다 만들지 않는다.
            if (prefab == null || prefab.transform.childCount == 0)
                return;

            var instance = Instantiate(prefab, transform);
            instance.name = "RoomDecor";
            instance.enabled = false;
            _decor = instance.gameObject;
            var parentScale = transform.lossyScale;
            _decor.transform.localPosition = new Vector3(0f, 0f, 0.005f);
            _decor.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);
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

        /// <summary>
        /// 유닛을 놓을 수 있는 칸을 격자에 칠해 준다.
        ///
        /// 격자는 선만 그려서 "칸이 있다"까지만 알려 준다. 어디가 비었는지는 유닛 배치를
        /// 들고 있는 이쪽만 알기 때문에, 좌표를 모아 격자에 넘긴다.
        /// </summary>
        public void ShowUnitPlacementHints()
        {
            var grid = TrapGrid;
            if (grid == null)
                return;

            placementHintCells.Clear();
            for (var row = 0; row < grid.Rows; row++)
            for (var column = 0; column < grid.Columns; column++)
            {
                if (IsUnitCellAvailable(column, row))
                    placementHintCells.Add(new Vector2Int(column, row));
            }

            grid.ShowPlacementHints(placementHintCells);
        }

        public void ClearUnitPlacementHints()
        {
            TrapGrid?.ClearPlacementHints();
        }

        private readonly List<Vector2Int> placementHintCells = new();

        public bool IsUnitCellAvailable(int column, int row)
        {
            var grid = TrapGrid;
            if (grid == null || !grid.IsValidCell(column, row))
                return false;

            if (grid.IsCentralBuildingSlotCell(column, row) || !grid.IsWalkableCell(column, row))
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

        /// <summary>살아 나간 모험가가 옮긴 견문을 소문에 더한다.</summary>
        public void AddRumor(in RoomRumor rumor)
        {
            Rumor = Rumor.Add(rumor);
        }

        /// <summary>하루가 지나 소문이 절반으로 사그라든다.</summary>
        public void HalveRumor()
        {
            Rumor = Rumor.Halved();
        }

        public void RestoreRumor(in RoomRumor rumor)
        {
            Rumor = rumor;
        }

        private void SetVisualColor(Color color)
        {
            _visualColor = color;
            RefreshRoomVisual();
        }

        private NodeGlow glow;

        /// <summary>
        /// 방 불빛을 챙긴다. 노드는 실행 중에 생성되므로 프리팹을 고치는 대신 여기서 붙인다.
        /// </summary>
        private NodeGlow EnsureGlow()
        {
            if (glow != null)
                return glow;

            glow = GetComponent<NodeGlow>();
            if (glow == null)
                glow = gameObject.AddComponent<NodeGlow>();

            return glow;
        }

        private void SetLockedOverlayVisible(bool visible)
        {
            SetLockedRootVisible(visible);

            // 방마다 있는 불빛도 같이 따라간다. 잠긴 곳은 차갑고 어둡게 두면
            // 열 수 있는 방과 아닌 방이 밝기만으로 갈린다.
            EnsureGlow()?.SetUnlocked(!visible);

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
            var capacityText = string.Format(unitCapacityFormat, deployed, capacity);
            unitCapacityText.text = Feature == RoomFeature.None
                ? capacityText
                : $"{RoomFeatureRules.Label(Feature)} {capacityText}";
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
            if (trapGrid != null)
                trapGrid.PartitionsChanged -= HandlePartitionsChanged;
            if (_dungeonTilemap != null)
                _dungeonTilemap.Unregister(this);
            allInstances.Remove(this);
            if (Data != null)
                nodesByDataId.Remove(Data.Id);
        }
    }
}
