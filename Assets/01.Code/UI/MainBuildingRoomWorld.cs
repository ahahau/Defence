using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.UI
{
    [ExecuteAlways]
    public class MainBuildingRoomWorld : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        [SerializeField] private TownInteriorScreenUI townInteriorScreenUI;
        [SerializeField] private Vector2Int boardOrigin = Vector2Int.zero;
        [SerializeField] private int boardColumns = 4;
        [SerializeField] private int boardRows = 4;
        [SerializeField] private Vector2 tileScale = new(1.2f, 1.2f);
        [SerializeField] private Vector2 boardOffset = new(0f, 0f);
        [SerializeField] private Color emptyColor = new(0.92f, 0.88f, 0.75f, 1f);
        [SerializeField] private Color occupiedColor = new(0.67f, 0.52f, 0.42f, 1f);
        [SerializeField] private Color selectedColor = new(0.95f, 0.71f, 0.31f, 1f);

        private readonly Dictionary<Vector2Int, MainBuildingRoomTile> _tiles = new();
        private readonly List<Vector2Int> _orderedCells = new();
        private BuildManager _buildManager;
        private Vector2Int _selectedCell;
        private bool _hasSelectedCell;

        private void Awake()
        {
            ResolveReferences();
            EnsureBoard();
            RefreshTiles();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureBoard();
            RefreshTiles();

            if (!Application.isPlaying)
            {
                return;
            }

            _buildManager ??= FindFirstObjectByType<BuildManager>();
            if (_buildManager == null)
            {
                return;
            }

            _buildManager.OnBuildingInstalled += HandleBuildingInstalled;
            _buildManager.OnBuildingMoved += HandleBuildStateChanged;
            _buildManager.OnBuildingMoveFailed += HandleBuildStateChanged;
            _buildManager.OnBuildFailed += HandleBuildFailed;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || _buildManager == null)
            {
                return;
            }

            _buildManager.OnBuildingInstalled -= HandleBuildingInstalled;
            _buildManager.OnBuildingMoved -= HandleBuildStateChanged;
            _buildManager.OnBuildingMoveFailed -= HandleBuildStateChanged;
            _buildManager.OnBuildFailed -= HandleBuildFailed;
        }

        private void ResolveReferences()
        {
            gridManager ??= FindFirstObjectByType<GridManager>();
            townInteriorScreenUI ??= FindFirstObjectByType<TownInteriorScreenUI>();
        }

        private void EnsureBoard()
        {
            if (gridManager == null)
            {
                return;
            }

            _tiles.Clear();
            _orderedCells.Clear();

            for (int y = boardRows - 1; y >= 0; y--)
            {
                for (int x = 0; x < boardColumns; x++)
                {
                    Vector2Int cell = boardOrigin + new Vector2Int(x, y);
                    _orderedCells.Add(cell);
                    EnsureTile(cell);
                }
            }
        }

        private void EnsureTile(Vector2Int cell)
        {
            string objectName = $"Tile_{cell.x}_{cell.y}";
            Transform existing = transform.Find(objectName);
            GameObject tileObject;
            if (existing == null || existing.GetComponent<MeshRenderer>() == null || existing.GetComponent<BoxCollider>() == null)
            {
                if (existing != null)
                {
                    DestroyTileObject(existing.gameObject);
                }

                tileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tileObject.name = objectName;
                tileObject.transform.SetParent(transform, false);
            }
            else
            {
                tileObject = existing.gameObject;
            }

            MainBuildingRoomTile tile = GetOrAddComponent<MainBuildingRoomTile>(tileObject);
            MeshRenderer meshRenderer = GetOrAddComponent<MeshRenderer>(tileObject);
            BoxCollider box = GetOrAddComponent<BoxCollider>(tileObject);

            tileObject.transform.position = GetCellWorldPosition(cell);
            tileObject.transform.localScale = GetTileVisualScale();
            box.size = Vector3.one;

            tile.Configure(this, cell, meshRenderer);
            _tiles[cell] = tile;
        }

        private Vector3 GetCellWorldPosition(Vector2Int cell)
        {
            Vector3 basePosition = gridManager != null && gridManager.Grid != null
                ? gridManager.CellToWorld(cell)
                : new Vector3(cell.x, cell.y, 0f);

            return basePosition + GetCenteredBoardOffset() + new Vector3(boardOffset.x, boardOffset.y, 0f);
        }

        public void HandleTileClicked(Vector2Int cell)
        {
            if (!Application.isPlaying || gridManager == null || gridManager.Tilemap == null)
            {
                return;
            }

            if (!IsCellEmpty(cell))
            {
                _hasSelectedCell = false;
                townInteriorScreenUI?.HideBuildPanelExternally();
                RefreshTiles();
                return;
            }

            if (_hasSelectedCell && _selectedCell == cell)
            {
                _hasSelectedCell = false;
                townInteriorScreenUI?.HideBuildPanelExternally();
                RefreshTiles();
                return;
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            townInteriorScreenUI?.OpenBuildPanel(cell);
            RefreshTiles();
        }

        private void HandleBuildingInstalled(Units.UnitDataSO _, Entities.PlaceableEntity __)
        {
            _hasSelectedCell = false;
            RefreshTiles();
        }

        private void HandleBuildStateChanged()
        {
            RefreshTiles();
        }

        private void HandleBuildFailed(Units.UnitDataSO _, Vector2Int __)
        {
            RefreshTiles();
        }

        private void RefreshTiles()
        {
            if (gridManager == null)
            {
                return;
            }

            for (int i = 0; i < _orderedCells.Count; i++)
            {
                Vector2Int cell = _orderedCells[i];
                if (!_tiles.TryGetValue(cell, out MainBuildingRoomTile tile) || tile == null)
                {
                    continue;
                }

                bool isEmpty = IsCellEmpty(cell);
                bool isSelected = _hasSelectedCell && cell == _selectedCell && isEmpty;
                Color color = isSelected ? selectedColor : isEmpty ? emptyColor : occupiedColor;
                tile.SetColor(color);
                tile.transform.position = GetCellWorldPosition(cell);
            }
        }

        private bool IsCellEmpty(Vector2Int cell)
        {
            if (gridManager == null || gridManager.Tilemap == null || gridManager.Tilemap.Tiles == null)
            {
                return true;
            }

            return gridManager.IsCellEmpty(cell);
        }

        private Vector3 GetCenteredBoardOffset()
        {
            float xOffset = -(boardOrigin.x + boardColumns * 0.5f);
            float yOffset = -(boardOrigin.y + boardRows * 0.5f);
            return new Vector3(xOffset, yOffset, 0f);
        }

        private Vector3 GetTileVisualScale()
        {
            const float gap = 0.3f;
            float x = Mathf.Max(0.1f, tileScale.x - gap);
            float y = Mathf.Max(0.1f, tileScale.y - gap);
            return new Vector3(x, y, 0.3f);
        }

        private void DestroyTileObject(GameObject tileObject)
        {
            if (Application.isPlaying)
            {
                Destroy(tileObject);
                return;
            }

            DestroyImmediate(tileObject);
        }

        private T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }

            return component;
        }

    }
}
