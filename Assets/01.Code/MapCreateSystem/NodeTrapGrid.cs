using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.BT;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    /// <summary>노드 내부의 격자. 셀 단위로 건물(트랩 포함)을 자유 배치하고 점유를 관리한다.
    /// 셀 좌표는 노드 중심 기준 '월드 단위'로 계산해 노드의 큰 스케일 영향을 받지 않는다.
    /// (클래스명은 직렬화 참조 호환을 위해 유지하되, 트랩 외 일반 건물도 여러 개 배치할 수 있다.)</summary>
    public class NodeTrapGrid : MonoBehaviour
    {
        [SerializeField, Min(1)] private int columns = 6;
        [SerializeField, Min(1)] private int rows = 6;
        [SerializeField, Min(0.1f), Tooltip("셀 간격(월드 단위). 노드에 NodeBattlefield가 있으면 아레나에 맞게 자동 조정된다.")] private float cellSize = 2f;
        [SerializeField, Tooltip("노드 중심에서 격자 중심까지의 오프셋(월드 단위).")] private Vector2 originOffset;
        [SerializeField, Tooltip("아레나 반지름에 맞춰 셀 간격 자동 조정(켜두면 노드 크기에 맞게 퍼진다).")] private bool autoFitToArena = true;

        [Header("Focused Grid Visual")]
        [SerializeField] private bool showGridWhenFocused = true;
        [SerializeField] private Color focusedGridColor = new(0.3f, 0.85f, 1f, 0.58f);
        [SerializeField, Min(0.005f)] private float focusedGridLineWidth = 0.03f;
        [SerializeField] private int focusedGridSortingOrder = 35;
        [SerializeField] private Color selectedFreeCellColor = new(0.25f, 1f, 0.48f, 0.38f);
        [SerializeField] private Color selectedOccupiedCellColor = new(1f, 0.62f, 0.2f, 0.42f);

        private Building[] _cells;
        private readonly List<Building> _placed = new();
        private GameObject _focusedGridRoot;
        private SpriteRenderer _selectedCellMarker;
        private static Material _focusedGridMaterial;
        private static Sprite _cellMarkerSprite;

        public int Columns => columns;
        public int Rows => rows;
        public int CellCount => columns * rows;
        /// <summary>셀 간격(월드 단위). 배치 미리보기가 하이라이트 크기를 맞출 때 사용.</summary>
        public float CellSize => cellSize;
        /// <summary>그리드에 배치된 모든 건물(트랩 발동·정리 등에서 순회. 트랩만 필요하면 is Trap으로 거른다).</summary>
        public IReadOnlyList<Building> PlacedBuildings => _placed;
        public bool IsFocusedGridVisible => _focusedGridRoot != null && _focusedGridRoot.activeSelf;

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

        public bool IsValidCell(int column, int row) =>
            column >= 0 && column < columns && row >= 0 && row < rows;

        public bool IsCellFree(int column, int row)
        {
            EnsureCells();
            return IsValidCell(column, row) && _cells[Index(column, row)] == null;
        }

        /// <summary>현재 배치된 개수.</summary>
        public int PlacedCount => _placed.Count;

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

        /// <summary>지정 셀에 건물 프리팹을 설치한다(빈 셀일 때만). 성공 시 인스턴스 반환.</summary>
        public Building TryPlace(int column, int row, Building buildingPrefab)
        {
            EnsureCells();
            if (buildingPrefab == null || !IsCellFree(column, row))
                return null;

            var building = Instantiate(buildingPrefab, CellWorldPosition(column, row), Quaternion.identity);
            building.transform.SetParent(transform, true); // worldPositionStays=true → 노드 스케일에 안 끌려감

            _cells[Index(column, row)] = building;
            _placed.Add(building);
            return building;
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
                    if (_cells[i] == null) return true;
                return false;
            }
        }

        /// <summary>기준 위치에서 가장 가까운 '빈' 셀에 설치한다(클릭/중심 기준 자유 배치).</summary>
        public Building PlaceNearestFreeCell(Vector3 worldPosition, Building buildingPrefab)
        {
            EnsureCells();
            if (buildingPrefab == null) return null;

            int bestColumn = -1, bestRow = -1;
            var bestDistance = float.MaxValue;
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < columns; c++)
            {
                if (_cells[Index(c, r)] != null) continue;
                var d = (CellWorldPosition(c, r) - worldPosition).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; bestColumn = c; bestRow = r; }
            }

            return bestColumn >= 0 ? TryPlace(bestColumn, bestRow, buildingPrefab) : null;
        }

        public bool Remove(int column, int row)
        {
            EnsureCells();
            if (!IsValidCell(column, row)) return false;

            var building = _cells[Index(column, row)];
            if (building == null) return false;

            _cells[Index(column, row)] = null;
            _placed.Remove(building);
            Destroy(building.gameObject);
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

            EnsureSelectedCellMarker();
            _selectedCellMarker.transform.position = CellWorldPosition(column, row) + Vector3.back * 0.02f;
            _selectedCellMarker.color = IsCellFree(column, row)
                ? selectedFreeCellColor
                : selectedOccupiedCellColor;
            _selectedCellMarker.enabled = true;
            return true;
        }

        public void ClearCellSelection()
        {
            if (_selectedCellMarker != null)
                _selectedCellMarker.enabled = false;
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

            for (var column = 0; column <= columns; column++)
            {
                var x = min.x + column * cellSize;
                CreateFocusedGridLine(new Vector3(x, min.y, 0f), new Vector3(x, max.y, 0f));
            }

            for (var row = 0; row <= rows; row++)
            {
                var y = min.y + row * cellSize;
                CreateFocusedGridLine(new Vector3(min.x, y, 0f), new Vector3(max.x, y, 0f));
            }
        }

        private void EnsureSelectedCellMarker()
        {
            if (_selectedCellMarker != null)
                return;

            var markerObject = new GameObject("SelectedCell");
            markerObject.transform.SetParent(_focusedGridRoot.transform, false);
            _selectedCellMarker = markerObject.AddComponent<SpriteRenderer>();
            _selectedCellMarker.sprite = CellMarkerSprite;
            _selectedCellMarker.sortingOrder = focusedGridSortingOrder - 1;

            var parentScale = transform.lossyScale;
            _selectedCellMarker.transform.localScale = new Vector3(
                cellSize * 0.9f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                cellSize * 0.9f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f);
        }

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

        private void CreateFocusedGridLine(Vector3 start, Vector3 end)
        {
            var lineObject = new GameObject("GridLine");
            lineObject.transform.SetParent(_focusedGridRoot.transform, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = focusedGridLineWidth;
            line.endWidth = focusedGridLineWidth;
            line.material = FocusedGridMaterial;
            line.startColor = focusedGridColor;
            line.endColor = focusedGridColor;
            line.sortingOrder = focusedGridSortingOrder;
        }

        private static Material FocusedGridMaterial
        {
            get
            {
                if (_focusedGridMaterial == null)
                    _focusedGridMaterial = new Material(Shader.Find("Sprites/Default"));
                return _focusedGridMaterial;
            }
        }

        public void ClearAll()
        {
            if (_cells != null)
            {
                for (var i = 0; i < _cells.Length; i++)
                    _cells[i] = null;
            }

            foreach (var building in _placed)
            {
                if (building != null)
                    Destroy(building.gameObject);
            }
            _placed.Clear();
        }

#if UNITY_EDITOR
        // 에디터에서 격자 셀 위치를 시각화(배치 칸 확인용).
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < columns; c++)
                Gizmos.DrawWireCube(CellWorldPosition(c, r), new Vector3(cellSize * 0.9f, cellSize * 0.9f, 0.01f));
        }
#endif
    }
}
