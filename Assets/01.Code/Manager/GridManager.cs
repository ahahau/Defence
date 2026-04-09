using _01.Code.Buildings;
using _01.Code.Entities;
using _01.Code.System.Grids;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _01.Code.Manager
{
    public class GridManager : MonoBehaviour, IManageable
    {
        [field: SerializeField] public Grid Grid { get; private set; }
        [field: SerializeField] public CustomTilemap Tilemap { get; private set; }
        [field: SerializeField] public int Size { get; private set; }
        [field: SerializeField] public int CellSize { get; private set; } = 1;
        [SerializeField] [Min(0f)] private float cellGap = 0f;

        public CommandCenter commandCenter;

        public Pathfinder PathFinder { get; private set; }
        private bool _isInitialized;

        public void Initialize(IManagerContainer managerContainer)
        {
            InitializeRuntimeState();
        }

        private void Awake()
        {
            EnsureRuntimeState();
        }

        private void OnEnable()
        {
            EnsureRuntimeState();
        }

        public void EnsureInitialized()
        {
            EnsureRuntimeState();
        }

        private void InitializeRuntimeState()
        {
            ApplyGridCellSize();
            Tilemap = new CustomTilemap(Size, Size);
            PathFinder = new Pathfinder(Tilemap);
            commandCenter?.BindGrid(this);
            _isInitialized = true;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            EditorApplication.delayCall -= HandleEditorValidate;
            EditorApplication.delayCall += HandleEditorValidate;
#endif
        }

        public Vector2Int WorldToCell(Vector3 worldPosition)
        {
            return WorldToPlacementCell(worldPosition);
        }

        public Vector2Int WorldToPlacementCell(Vector3 worldPosition)
        {
            EnsureRuntimeState();
            float halfCell = GetCellStep() * 0.5f;
            Vector3 adjustedWorldPosition = worldPosition + new Vector3(halfCell, halfCell, 0f);
            Vector3Int cellPosition = Grid.WorldToCell(adjustedWorldPosition);
            return new Vector2Int(cellPosition.x, cellPosition.y);
        }

        public Vector3 CellToWorld(Vector2Int cellPosition) => Grid.CellToWorld(new Vector3Int(cellPosition.x, cellPosition.y, 0));

        public Vector3 CellToObjectWorld(Vector2Int cellPosition) => CellToWorld(cellPosition);

        public bool IsCellEmpty(Vector2Int cellPosition)
        {
            EnsureRuntimeState();
            return Tilemap.TileEmpty(cellPosition);
        }

        public bool TryInstall(Vector2Int cellPosition, Entity entity)
        {
            EnsureRuntimeState();
            return Tilemap.TileObjectInstall(cellPosition, entity);
        }

        public bool TryClear(Vector2Int cellPosition, Entity expectedObject = null)
        {
            EnsureRuntimeState();
            return Tilemap.ClearTileObject(cellPosition, expectedObject);
        }

        public CustomTile GetTile(Vector2Int cellPosition)
        {
            EnsureRuntimeState();
            return Tilemap.GetTile(cellPosition);
        }

        public int GetTileCost(Vector2Int cellPosition)
        {
            CustomTile tile = GetTile(cellPosition);
            return tile == null ? 1 : Mathf.Max(1, tile.Cost);
        }

        public Vector2Int GetRandomGridPosition()
        {
            EnsureRuntimeState();
            int cnt = 0;
            while (true)
            {
                cnt++;
                if (cnt == Size * Size)
                {
                    return Vector2Int.zero;
                }

                int x = Random.Range(-Size, Size);
                int y = Random.Range(-Size, Size);
                Vector2Int pos = new Vector2Int(x, y);
                if (Tilemap.TileEmpty(pos))
                {
                    return pos;
                }
            }
        }

        private void ApplyGridCellSize()
        {
            if (Grid == null)
            {
                return;
            }

            float cellStep = GetCellStep();
            Grid.cellSize = new Vector3(cellStep, cellStep, 0f);
        }

        private float GetCellStep()
        {
            return Mathf.Max(0.01f, CellSize + cellGap);
        }

        private void EnsureRuntimeState()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (_isInitialized && Tilemap != null && PathFinder != null)
            {
                return;
            }

            InitializeRuntimeState();
        }

#if UNITY_EDITOR
        private void HandleEditorValidate()
        {
            if (this == null)
            {
                return;
            }

            ApplyGridCellSize();
        }
#endif
    }
}
