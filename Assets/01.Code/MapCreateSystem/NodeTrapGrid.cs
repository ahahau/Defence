using System;
using System.Collections.Generic;
using Code.Buildings;
using Code.BT;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Code.MapCreateSystem
{
    /// <summary>노드 내부의 격자. 셀 단위로 건물(트랩 포함)을 자유 배치하고 점유를 관리한다.
    /// 셀 좌표는 노드 중심 기준 '월드 단위'로 계산해 노드의 큰 스케일 영향을 받지 않는다.
    /// (클래스명은 직렬화 참조 호환을 위해 유지하되, 트랩 외 일반 건물도 여러 개 배치할 수 있다.)
    /// 방 안 길찾기(<see cref="RoomCellPathfinder"/>)도 이 격자를 칸 지도로 읽는다.</summary>
    public class NodeTrapGrid : MonoBehaviour, IRoomCellMap
    {
        [SerializeField, Min(1)] private int columns = 6;
        [SerializeField, Min(1)] private int rows = 6;
        [SerializeField, Min(0.1f), Tooltip("셀 간격(월드 단위). 노드에 NodeBattlefield가 있으면 아레나에 맞게 자동 조정된다.")] private float cellSize = 2f;
        [SerializeField, Tooltip("노드 중심에서 격자 중심까지의 오프셋(월드 단위).")] private Vector2 originOffset;
        [SerializeField, Tooltip("아레나 반지름에 맞춰 셀 간격 자동 조정(켜두면 노드 크기에 맞게 퍼진다).")] private bool autoFitToArena = true;

        [Header("Focused Grid Visual")]
        [SerializeField] private bool showGridWhenFocused = true;
        [SerializeField, Tooltip("격자 셰이더 재질(DungeonGridOverlay). 실행 중에는 복사본을 쓴다.")]
        private Material focusedGridMaterial;
        [SerializeField] private Color focusedGridColor = new(0.3f, 0.85f, 1f, 0.58f);
        [SerializeField, Min(0.005f)] private float focusedGridLineWidth = 0.03f;
        [SerializeField] private int focusedGridSortingOrder = 5;
        [SerializeField] private Color selectedFreeCellColor = new(0.25f, 1f, 0.48f, 0.38f);
        [SerializeField] private Color selectedOccupiedCellColor = new(1f, 0.62f, 0.2f, 0.42f);
        [Header("Central Building Slot")]
        [SerializeField] private bool reserveCentralBuildingSlot = true;
        [SerializeField, Min(1)] private int centralBuildingSlotColumns = 2;
        [SerializeField, Min(1)] private int centralBuildingSlotRows = 2;
        [SerializeField] private Color centralBuildingSlotColor = new(0.25f, 0.95f, 1f, 0.55f);

        private Building[] _cells;
        private readonly List<Building> _placed = new();

        /// <summary>방 모양에서 막힌 칸(기둥·빈 공간). 아무것도 놓을 수 없다. null이면 막힌 칸이 없다.</summary>
        private bool[] _blocked;

        /// <summary>문 앞 칸. 칸막이 벽으로 막을 수 없다.</summary>
        private readonly List<Vector2Int> _doorCells = new();

        /// <summary>칸막이 벽이 서거나 사라졌을 때. 방 그림과 길찾기가 다시 읽는다.</summary>
        public event Action PartitionsChanged;

        /// <summary>여러 칸을 차지하는 건물의 기준 칸(왼쪽 아래). 저장과 철거가 이 칸으로 건물을 찾는다.</summary>
        private readonly Dictionary<Building, Vector2Int> _anchors = new();

        /// <summary>여러 칸 건물을 한 번만 세려고 쓰는 임시 집합. 매번 새로 만들지 않는다.</summary>
        private readonly HashSet<Building> _countBuffer = new();
        private GameObject _focusedGridRoot;
        private SpriteRenderer _focusedGridSurface;
        private SpriteRenderer _selectedCellMarker;

        /// <summary>격자 셰이더에 넘기는 열린 칸 표(칸마다 1텍셀, 1 = 방 바닥이고 칸막이 없음). 닫힌 칸끼리의 선은 그리지 않는다.</summary>
        private Texture2D _openCellMask;
        private static Material _focusedGridMaterialCopy;
        private static Sprite _cellMarkerSprite;

        public int Columns => columns;
        public int Rows => rows;
        public int CellCount => columns * rows;
        /// <summary>셀 간격(월드 단위). 배치 미리보기가 하이라이트 크기를 맞출 때 사용.</summary>
        public float CellSize => cellSize;
        public Vector2 CentralBuildingSlotWorldSize
        {
            get
            {
                var bounds = GetCentralSlotBounds();
                return new Vector2(
                    (bounds.maxColumn - bounds.minColumn + 1) * cellSize,
                    (bounds.maxRow - bounds.minRow + 1) * cellSize);
            }
        }
        /// <summary>그리드에 배치된 모든 건물(트랩 발동·정리 등에서 순회. 트랩만 필요하면 is Trap으로 거른다).</summary>
        public IReadOnlyList<Building> PlacedBuildings => _placed;
        public bool IsFocusedGridVisible => _focusedGridRoot != null && _focusedGridRoot.activeSelf;

        /// <summary>
        /// 중앙 핵심부 슬롯의 상하좌우 한 칸 고리에 배치된 살아 있는 건물 수.
        /// 핵심부 결속 시스템의 공간 투자량으로 사용한다.
        /// </summary>
        public int CountBuildingsAdjacentToCentralSlot()
        {
            if (!reserveCentralBuildingSlot)
                return 0;

            EnsureCells();
            var bounds = GetCentralSlotBounds();
            var count = 0;
            _countBuffer.Clear();
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var building = _cells[Index(column, row)];
                if (building == null || building.IsDestroyed || _countBuffer.Contains(building))
                    continue;

                var besideHorizontalEdge = (column == bounds.minColumn - 1 || column == bounds.maxColumn + 1)
                                           && row >= bounds.minRow
                                           && row <= bounds.maxRow;
                var besideVerticalEdge = (row == bounds.minRow - 1 || row == bounds.maxRow + 1)
                                         && column >= bounds.minColumn
                                         && column <= bounds.maxColumn;
                if (besideHorizontalEdge || besideVerticalEdge)
                {
                    _countBuffer.Add(building);
                    count++;
                }
            }

            return count;
        }

        private void Awake()
        {
            EnsureCells();
            FitToArena();
        }

        /// <summary>같은 노드의 전투 아레나 반지름에 맞춰 셀 간격을 키운다 → 격자가 노드 전체에 퍼져
        /// 트랩/건물이 가운데에 몰리지 않는다(여백 80%). 아레나가 없으면 인스펙터 값 유지.</summary>
        private void FitToArena()
        {
            if (!autoFitToArena) return;

            var battlefield = GetComponent<NodeBattlefield>();
            if (battlefield == null || battlefield.ArenaRadius <= 0f) return;

            var usable = battlefield.ArenaRadius * 2f * 0.8f;
            var span = Mathf.Max(columns, rows) - 1;
            if (span > 0)
                cellSize = usable / span;
        }

        private void EnsureCells()
        {
            if (_cells == null || _cells.Length != CellCount)
                _cells = new Building[CellCount];
        }

        /// <summary>
        /// 방 프리팹에 맞춰 칸 수·칸 간격·막힌 칸을 정한다. 방을 만들 때 건물을 놓기 전에 한 번 부른다.
        /// 칸 간격을 정해 주므로 아레나에 맞춰 늘리는 자동 조정은 끈다.
        /// </summary>
        public void ConfigureCells(RoomShape layout, float worldCellSize)
        {
            if (layout == null || layout.Columns <= 0 || layout.Rows <= 0)
                return;

            if (_placed.Count > 0)
            {
                Debug.LogError($"{name}: 건물이 놓인 격자의 크기를 바꿀 수 없습니다.", this);
                return;
            }

            columns = layout.Columns;
            rows = layout.Rows;
            cellSize = worldCellSize;
            originOffset = Vector2.zero;
            autoFitToArena = false;
            _cells = new Building[CellCount];
            _blocked = new bool[CellCount];
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
                _blocked[Index(column, row)] = !layout.IsFloor(column, row);

            _doorCells.Clear();
            foreach (var side in RoomDoorRules.Sides)
            {
                if (!layout.HasDoor(side))
                    continue;

                var (a, b) = RoomLayoutRules.DoorCells(columns, rows, side);
                _doorCells.Add(a);
                if (b != a)
                    _doorCells.Add(b);
            }

            // 칸 수가 바뀌면 격자선·표시도 새 크기로 다시 만든다.
            if (_focusedGridRoot != null)
            {
                DestroyRuntimeObject(_focusedGridRoot);
                _focusedGridRoot = null;
                _focusedGridSurface = null;
                _selectedCellMarker = null;
            }

            if (_openCellMask != null)
            {
                DestroyRuntimeObject(_openCellMask);
                _openCellMask = null;
            }

            if (_placementHintRoot != null)
            {
                DestroyRuntimeObject(_placementHintRoot);
                _placementHintRoot = null;
                _placementHints.Clear();
            }
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        public bool IsValidCell(int column, int row) =>
            column >= 0 && column < columns && row >= 0 && row < rows;

        /// <summary>기둥·빈 공간처럼 방 모양에서 막힌 칸인가.</summary>
        public bool IsBlockedCell(int column, int row) =>
            IsValidCell(column, row) && _blocked != null && _blocked.Length == CellCount && _blocked[Index(column, row)];

        /// <summary>
        /// 걸어 지나갈 수 있는 칸인가. 막힌 칸, 서 있는 칸막이 벽, 시설이 선 중앙 슬롯은 못 지나간다. 함정·유닛 칸은 지나간다.
        /// </summary>
        public bool IsWalkableCell(int column, int row) =>
            IsValidCell(column, row) && !IsBlockedCell(column, row) && !IsPartitionCell(column, row)
            && !(_centralSlotBlocked && IsCentralBuildingSlotCell(column, row));

        /// <summary>중앙 슬롯에 시설이 서 있어 통행을 막는가(<see cref="SetCentralSlotBlocked"/>).</summary>
        private bool _centralSlotBlocked;

        /// <summary>
        /// 중앙 슬롯 시설이 통행을 막는지 정한다. 방(<see cref="Node"/>)이 시설을 세우거나 치울 때 부른다.
        /// 예전에는 상점·여관 같은 시설 그림 위를 부하·모험가가 그냥 걸어 지나갔다.
        /// </summary>
        public void SetCentralSlotBlocked(bool blocked)
        {
            _centralSlotBlocked = blocked;
        }

        /// <summary>서 있는(부서지지 않은) 칸막이 벽이 있는 칸인가.</summary>
        public bool IsPartitionCell(int column, int row)
        {
            if (!IsValidCell(column, row) || _cells == null || _cells.Length != CellCount)
                return false;

            var building = _cells[Index(column, row)];
            return building is Partition && building != null && !building.IsDestroyed;
        }

        /// <summary>그 월드 위치가 걸을 수 있는 칸 위인가. 격자 밖이면 false.</summary>
        public bool IsWalkableWorld(Vector3 worldPosition) =>
            TryGetCell(worldPosition, out var column, out var row) && IsWalkableCell(column, row);

        /// <summary>막힌 칸이나 칸막이 벽이 하나라도 있는가. 없으면 방 안 어디로든 곧장 걸어도 된다.</summary>
        public bool HasWalkBlockers
        {
            get
            {
                if (_blocked != null)
                {
                    for (var i = 0; i < _blocked.Length; i++)
                    {
                        if (_blocked[i])
                            return true;
                    }
                }

                foreach (var building in _placed)
                {
                    if (building is Partition && building != null && !building.IsDestroyed)
                        return true;
                }

                return _centralSlotBlocked;
            }
        }

        /// <summary>문 앞 칸인가.</summary>
        public bool IsDoorCell(int column, int row) => _doorCells.Contains(new Vector2Int(column, row));

        /// <summary>그 변 문 앞 칸(두 칸 중 아래·왼쪽). 문이 없으면 false.</summary>
        public bool TryGetDoorCell(RoomDoors side, out Vector2Int cell)
        {
            var (a, _) = RoomLayoutRules.DoorCells(columns, rows, side);
            cell = a;
            return _doorCells.Contains(a);
        }

        /// <summary>
        /// 그 칸에 칸막이 벽을 세워도 방이 열려 있는가. 문 앞 칸이 아니고, 걸을 수 있는 칸이 모두 한 덩어리로 남아야 한다.
        /// </summary>
        public bool KeepsRoomOpen(int column, int row) =>
            IsValidCell(column, row)
            && !IsDoorCell(column, row)
            && RoomCellPathfinder.StaysConnectedWithout(this, new Vector2Int(column, row));

        /// <summary>그 건물을 그 칸에 놓을 수 있는가. 칸막이 벽이면 방이 열려 있는지도 본다.</summary>
        public bool CanPlace(int column, int row, Building buildingPrefab)
        {
            if (buildingPrefab == null || !CanPlace(column, row, buildingPrefab.Footprint))
                return false;

            return buildingPrefab is not Partition || KeepsRoomOpen(column, row);
        }

        /// <summary>칸막이 벽이 선 칸들을 담는다(서 있는 것만).</summary>
        public void CollectPartitionCells(ICollection<Vector2Int> into)
        {
            if (into == null)
                return;

            foreach (var building in _placed)
            {
                if (building is Partition && building != null && !building.IsDestroyed
                    && _anchors.TryGetValue(building, out var anchor))
                    into.Add(anchor);
            }
        }

        /// <summary>그 월드 위치에서 가장 가까운 걸을 수 있는 칸. 그런 칸이 없으면 false.</summary>
        public bool TryGetNearestWalkableCell(Vector3 worldPosition, out Vector2Int cell)
        {
            cell = new Vector2Int(-1, -1);
            var best = float.MaxValue;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                if (!IsWalkableCell(column, row))
                    continue;

                var distance = (CellWorldPosition(column, row) - worldPosition).sqrMagnitude;
                if (distance >= best)
                    continue;

                best = distance;
                cell = new Vector2Int(column, row);
            }

            return cell.x >= 0;
        }

        /// <summary>
        /// 부서진 건물을 칸에서 뺀다. 오브젝트는 건물이 스스로 지운다(<see cref="Remove"/>와 달리 파괴하지 않는다).
        /// </summary>
        public void Release(Building building)
        {
            if (building is null || _cells == null)
                return;

            for (var i = 0; i < _cells.Length; i++)
            {
                if (ReferenceEquals(_cells[i], building))
                    _cells[i] = null;
            }

            _anchors.Remove(building);
            _placed.Remove(building);
            if (building is Partition)
                NotifyPartitionsChanged();
        }

        public bool IsCellFree(int column, int row)
        {
            EnsureCells();
            return IsValidCell(column, row)
                   && !IsBlockedCell(column, row)
                   && !IsCentralBuildingSlotCell(column, row)
                   && _cells[Index(column, row)] == null;
        }

        /// <summary>현재 배치된 개수.</summary>
        public int PlacedCount => _placed.Count;

        /// <summary>살아 있는 함정이 하나라도 있는가. 손님이 머무는 시설과 한 방에 두지 않기 위해 본다.</summary>
        public bool HasLiveTrap
        {
            get
            {
                foreach (var building in _placed)
                {
                    if (building is Trap trap && trap != null && !trap.IsDestroyed)
                        return true;
                }

                return false;
            }
        }

        private int Index(int column, int row) => row * columns + column;

        /// <summary>셀의 월드 좌표(노드 중심 기준, 격자를 중앙 정렬).</summary>
        public Vector3 CellWorldPosition(int column, int row)
        {
            var width = (columns - 1) * cellSize;
            var height = (rows - 1) * cellSize;
            var x = originOffset.x + column * cellSize - width * 0.5f;
            var y = originOffset.y + row * cellSize - height * 0.5f;
            return transform.position + new Vector3(x, y, 0f);
        }

        public Vector3 CentralBuildingWorldPosition()
        {
            var bounds = GetCentralSlotBounds();
            var min = CellWorldPosition(bounds.minColumn, bounds.minRow);
            var max = CellWorldPosition(bounds.maxColumn, bounds.maxRow);
            return (min + max) * 0.5f;
        }

        public bool IsCentralBuildingSlotCell(int column, int row)
        {
            if (!reserveCentralBuildingSlot || !IsValidCell(column, row))
                return false;

            var bounds = GetCentralSlotBounds();
            return column >= bounds.minColumn
                   && column <= bounds.maxColumn
                   && row >= bounds.minRow
                   && row <= bounds.maxRow;
        }

        /// <summary>월드 좌표가 어느 셀인지(클릭 배치용). 격자 밖이면 false.</summary>
        public bool TryGetCell(Vector3 worldPosition, out int column, out int row)
        {
            var local = worldPosition - transform.position;
            var width = (columns - 1) * cellSize;
            var height = (rows - 1) * cellSize;
            column = Mathf.RoundToInt((local.x - originOffset.x + width * 0.5f) / cellSize);
            row = Mathf.RoundToInt((local.y - originOffset.y + height * 0.5f) / cellSize);
            return IsValidCell(column, row);
        }

        /// <summary>
        /// (column, row)를 왼쪽 아래로 하는 <paramref name="footprint"/> 칸이 모두 비어 있는가.
        /// 하나라도 격자 밖·중앙 슬롯·다른 건물이면 놓을 수 없다.
        /// </summary>
        public bool CanPlace(int column, int row, Vector2Int footprint)
        {
            var width = Mathf.Max(1, footprint.x);
            var height = Mathf.Max(1, footprint.y);
            for (var dy = 0; dy < height; dy++)
            for (var dx = 0; dx < width; dx++)
            {
                if (!IsCellFree(column + dx, row + dy))
                    return false;
            }

            return true;
        }

        /// <summary>그 크기의 건물을 놓을 자리가 하나라도 있는가.</summary>
        public bool HasRoomFor(Vector2Int footprint)
        {
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < columns; c++)
            {
                if (CanPlace(c, r, footprint))
                    return true;
            }

            return false;
        }

        /// <summary>덮은 칸들의 가운데 월드 좌표.</summary>
        public Vector3 FootprintWorldCenter(int column, int row, Vector2Int footprint)
        {
            var min = CellWorldPosition(column, row);
            var max = CellWorldPosition(column + Mathf.Max(1, footprint.x) - 1, row + Mathf.Max(1, footprint.y) - 1);
            return (min + max) * 0.5f;
        }

        /// <summary>
        /// 지정 칸을 왼쪽 아래로 하여 건물 프리팹을 설치한다. 프리팹이 여러 칸짜리면 덮는 칸이 모두 비어야 한다.
        /// </summary>
        public Building TryPlace(int column, int row, Building buildingPrefab)
        {
            EnsureCells();
            if (buildingPrefab == null)
                return null;

            var footprint = buildingPrefab.Footprint;
            if (!CanPlace(column, row, footprint))
                return null;

            var building = Instantiate(buildingPrefab, FootprintWorldCenter(column, row, footprint), Quaternion.identity);
            building.transform.SetParent(transform, true); // worldPositionStays=true → 노드 스케일에 안 끌려감

            // 그림이 한 칸 크기 그대로면 두 칸을 막고 있다는 것이 보이지 않는다. 덮은 칸만큼 늘린다.
            if (footprint.x > 1 || footprint.y > 1)
            {
                var scale = building.transform.localScale;
                building.transform.localScale = new Vector3(scale.x * footprint.x, scale.y * footprint.y, scale.z);
            }

            for (var dy = 0; dy < footprint.y; dy++)
            for (var dx = 0; dx < footprint.x; dx++)
                _cells[Index(column + dx, row + dy)] = building;

            _anchors[building] = new Vector2Int(column, row);
            _placed.Add(building);
            if (building is Partition)
                NotifyPartitionsChanged();
            return building;
        }

        /// <summary>건물의 기준 칸(왼쪽 아래). 이 격자에 없으면 false.</summary>
        public bool TryGetAnchor(Building building, out int column, out int row)
        {
            column = -1;
            row = -1;
            if (building == null || !_anchors.TryGetValue(building, out var anchor))
                return false;

            column = anchor.x;
            row = anchor.y;
            return true;
        }

        /// <summary>월드 클릭 위치에서 가장 가까운 셀에 설치(클릭 배치용 진입점).</summary>
        public Building TryPlaceAtWorld(Vector3 worldPosition, Building buildingPrefab)
        {
            return TryGetCell(worldPosition, out var column, out var row)
                ? TryPlace(column, row, buildingPrefab)
                : null;
        }

        public bool HasFreeCell
        {
            get
            {
                EnsureCells();
                for (var i = 0; i < _cells.Length; i++)
                {
                    var column = i % columns;
                    var row = i / columns;
                    if (IsCellFree(column, row))
                        return true;
                }

                return false;
            }
        }

        /// <summary>기준 위치에서 가장 가까운 '빈' 셀을 찾는다. 빈 셀이 없으면 false.</summary>
        public bool TryGetNearestFreeCell(Vector3 worldPosition, out int column, out int row) =>
            TryGetNearestFreeCell(worldPosition, Vector2Int.one, out column, out row);

        /// <summary>
        /// 그 크기의 건물이 들어갈 자리 중 가운데가 기준 위치에서 가장 가까운 기준 칸(왼쪽 아래)을 찾는다.
        /// </summary>
        public bool TryGetNearestFreeCell(Vector3 worldPosition, Vector2Int footprint, out int column, out int row)
        {
            EnsureCells();

            column = -1;
            row = -1;
            var bestDistance = float.MaxValue;
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < columns; c++)
            {
                if (!CanPlace(c, r, footprint)) continue;
                var d = (FootprintWorldCenter(c, r, footprint) - worldPosition).sqrMagnitude;
                if (d >= bestDistance) continue;

                bestDistance = d;
                column = c;
                row = r;
            }

            return column >= 0;
        }

        /// <summary>기준 위치에서 가장 가까운 '빈' 셀에 설치한다(클릭/중심 기준 자유 배치).</summary>
        public Building PlaceNearestFreeCell(Vector3 worldPosition, Building buildingPrefab)
        {
            if (buildingPrefab == null)
                return null;

            return TryGetNearestFreeCell(worldPosition, out var column, out var row)
                ? TryPlace(column, row, buildingPrefab)
                : null;
        }

        /// <summary>그 칸에 선 건물. 비었거나 칸 범위를 벗어나면 null.</summary>
        public Building BuildingAt(int column, int row)
        {
            EnsureCells();
            return IsValidCell(column, row) ? _cells[Index(column, row)] : null;
        }

        public bool Remove(int column, int row)
        {
            EnsureCells();
            if (!IsValidCell(column, row)) return false;

            var building = _cells[Index(column, row)];
            if (building == null) return false;

            // 여러 칸 건물은 덮은 칸을 모두 비운다.
            for (var i = 0; i < _cells.Length; i++)
            {
                if (_cells[i] == building)
                    _cells[i] = null;
            }

            _anchors.Remove(building);
            _placed.Remove(building);
            var wasPartition = building is Partition;
            // EditMode 테스트에서도 같은 경로를 탄다. 런타임 Destroy는 에디트 모드에서 경고를 남긴다.
            if (Application.isPlaying)
                Destroy(building.gameObject);
            else
                DestroyImmediate(building.gameObject);
            if (wasPartition)
                NotifyPartitionsChanged();
            return true;
        }

        /// <summary>노드 확대 선택 상태에서 실제 배치 좌표와 일치하는 격자선을 표시한다.</summary>
        public void SetFocusedGridVisible(bool visible)
        {
            if (!visible || !showGridWhenFocused)
            {
                ClearCellSelection();
                if (_focusedGridRoot != null)
                    _focusedGridRoot.SetActive(false);
                return;
            }

            EnsureFocusedGridVisual();
            _focusedGridRoot.SetActive(true);
        }

        private void NotifyPartitionsChanged()
        {
            RefreshOpenCellMask();
            PartitionsChanged?.Invoke();
        }

        /// <summary>격자 선을 그릴 열린 칸: 방 바닥이고 칸막이가 서 있지 않은 칸.</summary>
        public bool IsOpenGridCell(int column, int row) =>
            IsValidCell(column, row) && !IsBlockedCell(column, row) && !IsPartitionCell(column, row);

        private void RefreshOpenCellMask()
        {
            if (_openCellMask == null)
                return;

            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
                _openCellMask.SetPixel(column, row, IsOpenGridCell(column, row) ? Color.white : Color.clear);
            _openCellMask.Apply(false);
        }

        private void OnDestroy()
        {
            if (_openCellMask != null)
                DestroyRuntimeObject(_openCellMask);
        }

        public bool TrySelectCell(Vector3 worldPosition, out int column, out int row)
        {
            column = -1;
            row = -1;
            if (!IsFocusedGridVisible)
                return false;

            var half = cellSize * 0.5f;
            var min = CellWorldPosition(0, 0) - new Vector3(half, half, 0f);
            var max = CellWorldPosition(columns - 1, rows - 1) + new Vector3(half, half, 0f);
            if (worldPosition.x < min.x || worldPosition.x > max.x ||
                worldPosition.y < min.y || worldPosition.y > max.y ||
                !TryGetCell(worldPosition, out column, out row))
                return false;

            if (IsCentralBuildingSlotCell(column, row))
            {
                var centralBounds = GetCentralSlotBounds();
                column = centralBounds.minColumn;
                row = centralBounds.minRow;
            }

            // Grid cells are placement targets, not selectable UI controls.  Keep their
            // visual state stable so clicks never repaint a small tile or imply ownership.
            ClearCellSelection();
            return true;
        }

        public void ClearCellSelection()
        {
            if (_selectedCellMarker != null)
                _selectedCellMarker.enabled = false;
        }

        /// <summary>
        /// 놓을 수 있는 칸을 칠해 보여 준다.
        ///
        /// 격자는 선만 그려서 "칸이 있다"까지만 알려 준다. 정작 어디가 비었는지는 보이지 않아
        /// 어디에 놓이는지 알기 어려웠다. 빈 칸만 옅게 칠하면 물어볼 것이 없어진다.
        ///
        /// 어느 칸이 비었는지는 <see cref="Node"/>가 안다(유닛 배치를 들고 있는 쪽이다).
        /// 여기는 좌표를 받아 그리기만 한다.
        /// </summary>
        public void ShowPlacementHints(IReadOnlyList<Vector2Int> cells)
        {
            EnsurePlacementHintRoot();

            var needed = cells != null ? cells.Count : 0;
            while (_placementHints.Count < needed)
                _placementHints.Add(CreatePlacementHint());

            for (var i = 0; i < _placementHints.Count; i++)
            {
                var marker = _placementHints[i];
                if (marker == null)
                    continue;

                if (i >= needed)
                {
                    marker.enabled = false;
                    continue;
                }

                marker.transform.position = CellWorldPosition(cells[i].x, cells[i].y) + Vector3.back * 0.02f;
                marker.enabled = true;
            }

            _placementHintRoot.SetActive(needed > 0);
        }

        public void ClearPlacementHints()
        {
            if (_placementHintRoot != null)
                _placementHintRoot.SetActive(false);
        }

        private void EnsurePlacementHintRoot()
        {
            if (_placementHintRoot != null)
                return;

            _placementHintRoot = new GameObject("PlacementHints");
            _placementHintRoot.transform.SetParent(transform, false);
        }

        private SpriteRenderer CreatePlacementHint()
        {
            var go = new GameObject("PlacementHint");
            go.transform.SetParent(_placementHintRoot.transform, false);

            var marker = go.AddComponent<SpriteRenderer>();
            marker.sprite = CellMarkerSprite;
            marker.color = PlacementHintColor;
            marker.sortingOrder = focusedGridSortingOrder + 2;

            // 부모 배율을 그대로 받으면 노드 크기에 따라 표시가 커졌다 작아진다.
            // 한 칸을 조금 안쪽으로 채우도록 월드 크기로 맞춘다.
            var parentScale = transform.lossyScale;
            var side = cellSize * 0.78f;
            go.transform.localScale = new Vector3(
                side / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                side / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);

            return marker;
        }

        private void EnsureFocusedGridVisual()
        {
            if (_focusedGridRoot != null)
                return;

            _focusedGridRoot = new GameObject("FocusedGridLines");
            _focusedGridRoot.transform.SetParent(transform, false);

            var half = cellSize * 0.5f;
            var min = CellWorldPosition(0, 0) - new Vector3(half, half, 0f);
            var max = CellWorldPosition(columns - 1, rows - 1) + new Vector3(half, half, 0f);
            var centralBounds = GetCentralSlotBounds();

            CreateFocusedGridSurface(min, max, centralBounds);
        }

        private void CreateFocusedGridSurface(
            Vector3 min,
            Vector3 max,
            (int minColumn, int maxColumn, int minRow, int maxRow) centralBounds)
        {
            var surfaceObject = new GameObject("FocusedGridSurface");
            surfaceObject.transform.SetParent(_focusedGridRoot.transform, false);
            surfaceObject.transform.position = (min + max) * 0.5f + Vector3.back * 0.015f;

            _focusedGridSurface = surfaceObject.AddComponent<SpriteRenderer>();
            _focusedGridSurface.sprite = CellMarkerSprite;
            _focusedGridSurface.sharedMaterial = ResolveFocusedGridMaterial();
            _focusedGridSurface.sortingOrder = focusedGridSortingOrder;

            var worldSize = max - min;
            var parentScale = transform.lossyScale;
            surfaceObject.transform.localScale = new Vector3(
                worldSize.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                worldSize.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);

            var properties = new MaterialPropertyBlock();
            properties.SetVector("_GridSize", new Vector4(columns, rows, 0f, 0f));
            properties.SetColor("_GridColor", focusedGridColor);
            properties.SetColor("_CenterColor", centralBuildingSlotColor);
            properties.SetFloat("_LineWidth", Mathf.Clamp(focusedGridLineWidth / cellSize * 0.62f, 0.008f, 0.04f));
            properties.SetFloat("_GlowStrength", 0.09f);
            _openCellMask = new Texture2D(columns, rows, TextureFormat.R8, false, true)
            {
                name = "OpenCellMask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            RefreshOpenCellMask();
            properties.SetTexture("_CellMask", _openCellMask);
            properties.SetVector("_CentralRect", reserveCentralBuildingSlot
                ? new Vector4(
                    centralBounds.minColumn / (float)columns,
                    centralBounds.minRow / (float)rows,
                    (centralBounds.maxColumn + 1f) / columns,
                    (centralBounds.maxRow + 1f) / rows)
                : new Vector4(-1f, -1f, -1f, -1f));
            _focusedGridSurface.SetPropertyBlock(properties);
        }

        private void EnsureSelectedCellMarker()
        {
            if (_selectedCellMarker != null)
                return;

            var markerObject = new GameObject("SelectedCell");
            markerObject.transform.SetParent(_focusedGridRoot.transform, false);
            _selectedCellMarker = markerObject.AddComponent<SpriteRenderer>();
            _selectedCellMarker.sprite = CellMarkerSprite;
            _selectedCellMarker.sortingOrder = focusedGridSortingOrder + 1;
        }

        private void SetSelectedCellMarker(Vector3 worldPosition, Vector2 worldSize, Color color)
        {
            _selectedCellMarker.transform.position = worldPosition + Vector3.back * 0.02f;
            _selectedCellMarker.color = color;
            var parentScale = transform.lossyScale;
            _selectedCellMarker.transform.localScale = new Vector3(
                worldSize.x * 0.9f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                worldSize.y * 0.9f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);
        }

        /// <summary>놓을 수 있는 칸 표시. 초록은 "여기 된다"로 읽히는 몇 안 되는 색이다.</summary>
        private static readonly Color PlacementHintColor = new(0.42f, 0.92f, 0.55f, 0.3f);

        private GameObject _placementHintRoot;
        private readonly List<SpriteRenderer> _placementHints = new();

        private static Sprite CellMarkerSprite
        {
            get
            {
                if (_cellMarkerSprite != null)
                    return _cellMarkerSprite;

                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    name = "NodeGridCellMarker",
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _cellMarkerSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                _cellMarkerSprite.name = "NodeGridCellMarker";
                return _cellMarkerSprite;
            }
        }

        /// <summary>모든 방이 한 복사본을 나눠 쓴다. 칸 수·색은 MaterialPropertyBlock으로 방마다 준다.</summary>
        private Material ResolveFocusedGridMaterial()
        {
            if (_focusedGridMaterialCopy != null)
                return _focusedGridMaterialCopy;

            if (focusedGridMaterial == null)
            {
                Debug.LogError($"{name}의 {nameof(NodeTrapGrid)}.{nameof(focusedGridMaterial)}이(가) 비어 있습니다.", this);
                return null;
            }

            _focusedGridMaterialCopy = new Material(focusedGridMaterial)
            {
                name = "Dungeon Grid Overlay (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            return _focusedGridMaterialCopy;
        }

        private (int minColumn, int maxColumn, int minRow, int maxRow) GetCentralSlotBounds()
        {
            var slotColumns = Mathf.Clamp(centralBuildingSlotColumns, 1, columns);
            var slotRows = Mathf.Clamp(centralBuildingSlotRows, 1, rows);
            var minColumn = Mathf.Clamp((columns - slotColumns) / 2, 0, columns - 1);
            var minRow = Mathf.Clamp((rows - slotRows) / 2, 0, rows - 1);
            return (
                minColumn,
                Mathf.Min(columns - 1, minColumn + slotColumns - 1),
                minRow,
                Mathf.Min(rows - 1, minRow + slotRows - 1));
        }

        public void ClearAll()
        {
            if (_cells != null)
            {
                for (var i = 0; i < _cells.Length; i++)
                    _cells[i] = null;
            }

            var hadPartition = false;
            foreach (var building in _placed)
            {
                hadPartition |= building is Partition;
                if (building != null)
                    Destroy(building.gameObject);
            }
            _placed.Clear();
            _anchors.Clear();
            if (hadPartition)
                NotifyPartitionsChanged();
        }

    }
}
