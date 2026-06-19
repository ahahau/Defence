using System.Collections.Generic;
using _01.Code.Buildings;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
    /// <summary>트랩 노드 내부의 격자. 셀 단위로 트랩을 자유 배치하고 점유를 관리한다.
    /// 셀 좌표는 노드 중심 기준 '월드 단위'로 계산해 노드의 큰 스케일 영향을 받지 않는다.</summary>
    public class NodeTrapGrid : MonoBehaviour
    {
        [SerializeField, Min(1)] private int columns = 3;
        [SerializeField, Min(1)] private int rows = 2;
        [SerializeField, Min(0.1f), Tooltip("셀 간격(월드 단위).")] private float cellSize = 1.5f;
        [SerializeField, Tooltip("노드 중심에서 격자 중심까지의 오프셋(월드 단위).")] private Vector2 originOffset;

        private Trap[] _cells;
        private readonly List<Trap> _placed = new();

        public int Columns => columns;
        public int Rows => rows;
        public int CellCount => columns * rows;
        public IReadOnlyList<Trap> PlacedTraps => _placed;

        private void Awake()
        {
            EnsureCells();
        }

        private void EnsureCells()
        {
            if (_cells == null || _cells.Length != CellCount)
                _cells = new Trap[CellCount];
        }

        public bool IsValidCell(int column, int row) =>
            column >= 0 && column < columns && row >= 0 && row < rows;

        public bool IsCellFree(int column, int row)
        {
            EnsureCells();
            return IsValidCell(column, row) && _cells[Index(column, row)] == null;
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

        /// <summary>지정 셀에 트랩 프리팹을 설치한다(빈 셀일 때만). 성공 시 인스턴스 반환.</summary>
        public Trap TryPlaceTrap(int column, int row, Trap trapPrefab)
        {
            EnsureCells();
            if (trapPrefab == null || !IsCellFree(column, row))
                return null;

            var trap = Instantiate(trapPrefab, CellWorldPosition(column, row), Quaternion.identity);
            trap.transform.SetParent(transform, true); // worldPositionStays=true → 노드 스케일에 안 끌려감

            _cells[Index(column, row)] = trap;
            _placed.Add(trap);
            return trap;
        }

        /// <summary>월드 클릭 위치에서 가장 가까운 셀에 설치(클릭 배치용 진입점).</summary>
        public Trap TryPlaceTrapAtWorld(Vector3 worldPosition, Trap trapPrefab)
        {
            return TryGetCell(worldPosition, out var column, out var row)
                ? TryPlaceTrap(column, row, trapPrefab)
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
        public Trap PlaceNearestFreeCell(Vector3 worldPosition, Trap trapPrefab)
        {
            EnsureCells();
            if (trapPrefab == null) return null;

            int bestColumn = -1, bestRow = -1;
            var bestDistance = float.MaxValue;
            for (var r = 0; r < rows; r++)
            for (var c = 0; c < columns; c++)
            {
                if (_cells[Index(c, r)] != null) continue;
                var d = (CellWorldPosition(c, r) - worldPosition).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; bestColumn = c; bestRow = r; }
            }

            return bestColumn >= 0 ? TryPlaceTrap(bestColumn, bestRow, trapPrefab) : null;
        }

        public bool RemoveTrap(int column, int row)
        {
            EnsureCells();
            if (!IsValidCell(column, row)) return false;

            var trap = _cells[Index(column, row)];
            if (trap == null) return false;

            _cells[Index(column, row)] = null;
            _placed.Remove(trap);
            Destroy(trap.gameObject);
            return true;
        }

        public void ClearAll()
        {
            if (_cells != null)
            {
                for (var i = 0; i < _cells.Length; i++)
                    _cells[i] = null;
            }

            foreach (var trap in _placed)
            {
                if (trap != null)
                    Destroy(trap.gameObject);
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
