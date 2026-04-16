using System;
using System.Collections.Generic;
using _01.Code.Commands;
using _01.Code.Commands.Battle;
using _01.Code.Commands.Town;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.TownPanels;
using _01.Code.UI;
using _01.Code.Units;
using _01.Code.Buildings;
using _01.Code.Cost;
using UnityEngine;

namespace _01.Code.Tiles
{
    [Serializable]
    public class TownTileObjectPlacement
    {
        [field: SerializeField] public TownTileObjectDataSO Data { get; private set; }
        [field: SerializeField] public Vector2Int CellPosition { get; private set; }
    }

    [ExecuteAlways]
    public class MainBuildingRoomWorld : MonoBehaviour
    {
        [field: SerializeField] public GridManager GridManager { get; private set; }
        [field: SerializeField] public TownInteriorScreenUI TownInteriorScreenUI { get; private set; }
        [field: SerializeField] public BattleBuildingCatalogSO BuildingCatalog { get; private set; }
        [field: SerializeField] public Vector2Int BoardOrigin { get; private set; } = Vector2Int.zero;
        [field: SerializeField] public bool UseChildTileCount { get; private set; } = true;
        [field: SerializeField] public int BoardColumns { get; private set; } = 4;
        [field: SerializeField] public int BoardRows { get; private set; } = 4;
        [field: SerializeField] public Vector2 TileScale { get; private set; } = new(1.2f, 1.2f);
        [field: SerializeField] public Vector2 BoardOffset { get; private set; } = new(0f, 0f);
        [field: SerializeField] public int SortingOrder { get; private set; } = -10;
        [field: SerializeField] public TownObstacleDataSO DefaultObstacleData { get; private set; }
        [field: SerializeField] public List<TownObstacleDataSO> DefaultObstacleVariants { get; private set; } = new();
        [field: SerializeField] public List<TownTileObjectPlacement> DefaultTileObjects { get; private set; } = new();
        [SerializeField] [Min(8)] private int tileOutlineResolution = 32;
        [SerializeField] [Min(1)] private int tileOutlineThickness = 2;
        [SerializeField] [Range(0f, 1f)] private float glowAlphaMultiplier = 0.55f;
        [SerializeField] [Min(1f)] private float selectedGlowIntensityMultiplier = 1.8f;
        [SerializeField] [Min(0f)] private float glowIntensity = 2.4f;
        [SerializeField] private Color glowTint = new Color(0.55f, 0.95f, 1f, 1f);
        [SerializeField] [Range(0f, 1f)] private float emptyAlpha = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float selectedAlpha = 0.55f;
        [SerializeField] [Range(0f, 1f)] private float occupiedAlpha = 0.85f;

        public Sprite TileSprite
        {
            get { return GetTileSprite(); }
        }

        public Material GlowMaterial
        {
            get { return GetGlowMaterial(); }
        }

        public int GlowSortingOrder
        {
            get { return SortingOrder + 1; }
        }

        public Color GlowTint
        {
            get { return glowTint; }
        }

        public float GlowAlphaMultiplier
        {
            get { return glowAlphaMultiplier; }
        }

        public float GlowIntensity
        {
            get { return glowIntensity; }
        }

        public float SelectedGlowIntensityMultiplier
        {
            get { return selectedGlowIntensityMultiplier; }
        }

        private readonly Dictionary<Vector2Int, MainBuildingRoomTile> _tiles = new();
        private readonly List<Vector2Int> _orderedCells = new();
        private Sprite _tileSprite;
        private Material _glowMaterial;
        private BuildManager _buildManager;
        private CostManager _costManager;
        private SaveManager _saveManager;
        private LogManager _logManager;
        private GameEventChannelSO _uiEventChannel;
        private Vector2Int _selectedCell;
        private bool _hasSelectedCell;
        private Camera _worldCamera;
        private int _lastHandledClickFrame = -1;
        private bool _hasProcessedStartupPlacements;
        private UnitPanelUI _unitPanelUi;

        private void Awake()
        {
            ResolveReferences();
            EnsureBoard();
            RefreshTiles();
        }
            
        private void Start()
        {
            TryApplyStartupPlacements();
            AlignTilesToGrid();
            RefreshTiles();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!_hasProcessedStartupPlacements)
            {
                TryApplyStartupPlacements();
            }
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

            if (_buildManager == null)
            {
                return;
            }

            SubscribeToTownCommandEvents();
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

            UnsubscribeFromTownCommandEvents();
            _buildManager.OnBuildingInstalled -= HandleBuildingInstalled;
            _buildManager.OnBuildingMoved -= HandleBuildStateChanged;
            _buildManager.OnBuildingMoveFailed -= HandleBuildStateChanged;
            _buildManager.OnBuildFailed -= HandleBuildFailed;
        }

        private void OnValidate()
        {
            _tileSprite = null;
            _glowMaterial = null;
        }

        private void ResolveReferences()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            GridManager ??= GameManager.Instance.GetManager<GridManager>();
            _buildManager ??= GameManager.Instance.GetManager<BuildManager>();
            _costManager ??= GameManager.Instance.GetManager<CostManager>();
            _saveManager ??= GameManager.Instance.GetManager<SaveManager>();
            _logManager ??= GameManager.Instance.GetManager<LogManager>();
            _uiEventChannel ??= _buildManager != null ? _buildManager.UiEventChannel : null;
            _unitPanelUi ??= FindFirstObjectByType<UnitPanelUI>(FindObjectsInactive.Include);
            if (GridManager != null && GridManager.Tilemap == null)
            {
                GridManager.Initialize(GameManager.Instance);
            }

            if (GridManager != null && _orderedCells.Count == 0)
            {
                EnsureBoard();
            }

        }

        private void ResolveWorldCamera()
        {
            _worldCamera = Camera.main;
            if (_worldCamera == null)
            {
                _worldCamera = FindFirstObjectByType<Camera>();
            }
        }

        private void SpawnMissingDefaultTileObjects()
        {
            if (!Application.isPlaying || GridManager == null)
            {
                return;
            }

            for (int i = 0; i < DefaultTileObjects.Count; i++)
            {
                TownTileObjectPlacement placement = DefaultTileObjects[i];
                if (placement == null)
                {
                    continue;
                }

                SpawnTileObject(placement.Data, placement.CellPosition);
            }
        }

        private void SpawnDefaultObstacles()
        {
            if (!Application.isPlaying || GridManager == null)
            {
                return;
            }

            List<TownObstacleDataSO> obstacleVariants = GetDefaultObstacleVariants();
            if (obstacleVariants.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _orderedCells.Count; i++)
            {
                Vector2Int cell = _orderedCells[i];
                if (cell == Vector2Int.zero)
                {
                    continue;
                }

                if (!GridManager.IsCellEmpty(cell))
                {
                    continue;
                }

                TownObstacleDataSO obstacleData = obstacleVariants[i % obstacleVariants.Count];
                SpawnTileObject(obstacleData, cell);
            }
        }

        private void TryApplyStartupPlacements()
        {
            if (!Application.isPlaying || GridManager == null)
            {
                return;
            }

            SpawnMissingDefaultTileObjects();

            if (_saveManager != null && _saveManager.HasSavedPlacements())
            {
                _logManager?.Building("Town startup obstacle placements skipped: saved placements detected. Required default tile objects were still ensured.");
                _hasProcessedStartupPlacements = true;
                RefreshTiles();
                return;
            }

            _logManager?.Building("Town startup placements: no saved placements detected, filling default obstacles.");
            SpawnDefaultObstacles();
            _hasProcessedStartupPlacements = true;
            RefreshTiles();
        }

        private void EnsureBoard()
        {
            if (GridManager == null)
            {
                return;
            }

            _tiles.Clear();
            _orderedCells.Clear();

            int tileCount = GetTargetTileCount();
            EnsureTileObjects(tileCount);
            List<Vector2Int> cells = UseChildTileCount
                ? CreateSquareSpiralCells(tileCount)
                : CreateRectangularCells(tileCount);

            for (int i = 0; i < cells.Count; i++)
            {
                Vector2Int cell = cells[i];
                _orderedCells.Add(cell);
                EnsureTile(cell, i);
            }
        }

        private void EnsureTile(Vector2Int cell, int tileIndex)
        {
            Transform childTransform = transform.GetChild(tileIndex);
            GameObject tileObject = childTransform.gameObject;
            tileObject.name = $"Tile_{cell.x}_{cell.y}";

            MainBuildingRoomTile tile = GetOrAddComponent<MainBuildingRoomTile>(tileObject);
            SpriteRenderer spriteRenderer = GetOrAddComponent<SpriteRenderer>(tileObject);
            BoxCollider2D boxCollider = GetOrAddComponent<BoxCollider2D>(tileObject);

            tileObject.transform.localPosition = GetCellLocalPosition(cell);
            tileObject.transform.localScale = GetTileVisualScale();
            spriteRenderer.sprite = GetTileSprite();
            spriteRenderer.sortingOrder = SortingOrder;
            boxCollider.size = Vector2.one;

            tile.Initialize(this, cell, spriteRenderer);
            _tiles[cell] = tile;
        }

        private Vector3 GetCellWorldPosition(Vector2Int cell)
        {
            Vector3 basePosition = GridManager != null && GridManager.Grid != null
                ? GridManager.CellToWorld(cell)
                : new Vector3(cell.x, cell.y, 0f);

            return basePosition + new Vector3(BoardOffset.x, BoardOffset.y, 1f);
        }

        private Vector3 GetCellLocalPosition(Vector2Int cell)
        {
            return transform.InverseTransformPoint(GetCellWorldPosition(cell));
        }

        public void HandleTileClicked(Vector2Int cell)
        {
            if (Application.isPlaying && _lastHandledClickFrame == Time.frameCount)
            {
                Debug.Log($"TownWorld HandleTileClicked skipped same frame for {cell}.");
                return;
            }

            _lastHandledClickFrame = Time.frameCount;
            ResolveReferences();
            if (!Application.isPlaying || !_tiles.ContainsKey(cell))
            {
                Debug.Log($"TownWorld HandleTileClicked ignored. isPlaying={Application.isPlaying}, hasCell={_tiles.ContainsKey(cell)}, cell={cell}");
                return;
            }

            if (TryHandleSelectedUnitPlacement(cell))
            {
                Debug.Log($"TownWorld HandleTileClicked consumed by selected unit at {cell}.");
                return;
            }

            if (_hasSelectedCell && _selectedCell == cell)
            {
                _hasSelectedCell = false;
                TownInteriorScreenUI?.HideBuildPanelExternally();
                TownInteriorScreenUI?.HideObjectDetailsExternally();
                _unitPanelUi?.HidePanel();
                RefreshTiles();
                return;
            }

            if (!IsCellEmpty(cell))
            {
                _unitPanelUi?.HidePanel();
                if (TryGetObstacle(cell, out TownObstacle obstacle))
                {
                    _selectedCell = cell;
                    _hasSelectedCell = true;
                    TownInteriorScreenUI?.HideObjectDetailsExternally();
                    ShowObstacleCommands(obstacle);
                    RefreshTiles();
                    return;
                }

                TownTileObject tileObject = GetTileObject(cell);
                if (tileObject != null && tileObject.Data != null && HasObjectCommands(tileObject))
                {
                    _selectedCell = cell;
                    _hasSelectedCell = true;
                    ShowObjectCommands(tileObject);
                    RefreshTiles();
                    return;
                }

                _selectedCell = cell;
                _hasSelectedCell = true;
                TownInteriorScreenUI?.HideObjectDetailsExternally();
                ShowSelectedTileCommands();
                RefreshTiles();
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            TownInteriorScreenUI?.HideObjectDetailsExternally();
            ShowSelectedTileCommands();
            RefreshTiles();
        }

        private bool TryHandleSelectedUnitPlacement(Vector2Int cell)
        {
            if (_buildManager == null || _buildManager.SelectedUnit == null)
            {
                return false;
            }

            if (!_tiles.ContainsKey(cell))
            {
                return true;
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            TownInteriorScreenUI?.HideBuildPanelExternally();
            TownInteriorScreenUI?.HideObjectDetailsExternally();
            _unitPanelUi?.HidePanel();

            if (!IsCellEmpty(cell))
            {
                RefreshTiles();
                return true;
            }

            TryBuildAtCell(_buildManager.SelectedUnit, cell);
            RefreshTiles();
            return true;
        }

        public bool TryHandleWorldClick(Vector2 worldPosition)
        {
            ResolveReferences();
            if (_tiles.Count == 0)
            {
                Debug.Log("TownWorld TryHandleWorldClick failed: tile cache empty.");
                return false;
            }

            if (!TryGetClickedCell(worldPosition, out Vector2Int cell))
            {
                Debug.Log($"TownWorld TryHandleWorldClick failed: no tile near {worldPosition}.");
                return false;
            }

            Debug.Log($"TownWorld TryHandleWorldClick resolved {worldPosition} -> {cell}.");
            HandleTileClicked(cell);
            return true;
        }

        private bool TryGetClickedCell(Vector2 worldPosition, out Vector2Int cell)
        {
            cell = default;

            float maxDistanceSqr = GetTileSelectionRadiusSqr();
            float closestDistanceSqr = float.MaxValue;
            bool found = false;

            foreach (KeyValuePair<Vector2Int, MainBuildingRoomTile> pair in _tiles)
            {
                MainBuildingRoomTile tile = pair.Value;
                if (tile == null)
                {
                    continue;
                }

                Vector3 tileWorldPosition = tile.transform.position;
                Vector2 delta = (Vector2)tileWorldPosition - worldPosition;
                float distanceSqr = delta.sqrMagnitude;
                if (distanceSqr > maxDistanceSqr || distanceSqr >= closestDistanceSqr)
                {
                    continue;
                }

                closestDistanceSqr = distanceSqr;
                cell = pair.Key;
                found = true;
            }

            return found;
        }

        private float GetTileSelectionRadiusSqr()
        {
            float halfWidth = Mathf.Max(0.5f, TileScale.x * 0.5f);
            float halfHeight = Mathf.Max(0.5f, TileScale.y * 0.5f);
            float radius = Mathf.Max(halfWidth, halfHeight);
            return radius * radius;
        }

        public bool TryHandleTileObjectClick(TownTileObject tileObject)
        {
            ResolveReferences();
            if (tileObject == null)
            {
                return false;
            }

            Vector2Int cell = tileObject.GridPosition;
            if (!_tiles.ContainsKey(cell))
            {
                return false;
            }

            HandleTileClicked(cell);
            return true;
        }

        private void HandleBuildingInstalled(Units.UnitDataSO _, Entities.PlaceableEntity __)
        {
            ResolveReferences();
            _hasSelectedCell = false;
            TownInteriorScreenUI?.HideObjectDetailsExternally();
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

        private void HandleTownCommandSelected(TownCommandSelectedEvent evt)
        {
            ResolveReferences();
            if (!_hasSelectedCell || evt == null || evt.Command == null)
            {
                return;
            }

            CommandContext context = new CommandContext(this, _buildManager, _costManager, _selectedCell, GetObstacle(_selectedCell));
            if (!evt.Command.CanExecute(context) || !evt.Command.Execute(context))
            {
                return;
            }

            if (_hasSelectedCell)
            {
                TownObstacle obstacle = GetObstacle(_selectedCell);
                if (obstacle != null)
                {
                    ShowObstacleCommands(obstacle);
                    return;
                }

                ShowSelectedTileCommands();
            }
        }

        private void RefreshTiles()
        {
            ResolveReferences();
            for (int i = 0; i < _orderedCells.Count; i++)
            {
                Vector2Int cell = _orderedCells[i];
                if (!_tiles.TryGetValue(cell, out MainBuildingRoomTile tile) || tile == null)
                {
                    continue;
                }

                bool isEmpty = IsCellEmpty(cell);
                bool isSelected = _hasSelectedCell && cell == _selectedCell;
                float alpha = isSelected ? selectedAlpha : isEmpty ? emptyAlpha : occupiedAlpha;
                Color color = new Color(Color.white.r,Color.white.g,Color.white.b, alpha);
                tile.SetVisualState(color, isSelected);
                tile.transform.localPosition = GetCellLocalPosition(cell);
            }
        }

        public void ShowTileCommands(MainBuildingRoomTile tile)
        {
            if (TownInteriorScreenUI == null || tile == null)
            {
                Debug.Log($"TownWorld ShowTileCommands aborted. tileNull={tile == null}, uiNull={TownInteriorScreenUI == null}");
                return;
            }

            string title = string.IsNullOrWhiteSpace(tile.CommandTitle)
                ? "COMMAND"
                : tile.CommandTitle;
            CommandContext context = new CommandContext(this, _buildManager, _costManager, tile.Cell, null);
            TownInteriorScreenUI.ShowCommands(title, tile.Commands, context);
        }

        private void ShowUnitPanel(Vector2Int cell)
        {
            if (_unitPanelUi == null)
            {
                Debug.Log($"TownWorld ShowUnitPanel aborted: UnitPanelUI missing for {cell}.");
                return;
            }

            Debug.Log($"TownWorld showing unit panel for empty cell {cell}.");
            _unitPanelUi.ShowInstallPanel(cell);
        }

        private void ShowObstacleCommands(TownObstacle obstacle)
        {
            if (TownInteriorScreenUI == null || obstacle == null)
            {
                return;
            }

            string title = obstacle.Data != null && !string.IsNullOrWhiteSpace(obstacle.Data.CommandTitle)
                ? obstacle.Data.CommandTitle
                : obstacle.Data != null && !string.IsNullOrWhiteSpace(obstacle.Data.DisplayName)
                    ? obstacle.Data.DisplayName.ToUpperInvariant()
                    : "OBSTACLE";
            CommandContext context = new CommandContext(this, _buildManager, _costManager, obstacle.GridPosition, obstacle);
            TownInteriorScreenUI.ShowCommands(title, obstacle.Data.Commands, context);
        }

        private void ShowObjectCommands(TownTileObject tileObject)
        {
            if (TownInteriorScreenUI == null || tileObject == null || tileObject.Data == null)
            {
                return;
            }

            string title = !string.IsNullOrWhiteSpace(tileObject.Data.CommandTitle)
                ? tileObject.Data.CommandTitle
                : !string.IsNullOrWhiteSpace(tileObject.Data.DisplayName)
                    ? tileObject.Data.DisplayName.ToUpperInvariant()
                : "OBJECT";
            CommandContext context = new CommandContext(this, _buildManager, _costManager, _selectedCell, null);
            TownInteriorScreenUI.HideObjectDetailsExternally();
            TownInteriorScreenUI.ShowCommands(title, tileObject.Data.Commands, context);
        }

        private bool HasObjectCommands(TownTileObject tileObject)
        {
            if (tileObject == null || tileObject.Data == null)
            {
                return false;
            }

            return tileObject.Data.Commands != null && tileObject.Data.Commands.Count > 0;
        }

        private void SubscribeToTownCommandEvents()
        {
            if (_uiEventChannel == null)
            {
                return;
            }

            _uiEventChannel.RemoveListener<TownCommandSelectedEvent>(HandleTownCommandSelected);
            _uiEventChannel.AddListener<TownCommandSelectedEvent>(HandleTownCommandSelected);
        }

        private void UnsubscribeFromTownCommandEvents()
        {
            if (_uiEventChannel == null)
            {
                return;
            }

                _uiEventChannel.RemoveListener<TownCommandSelectedEvent>(HandleTownCommandSelected);
        }

        private void ShowSelectedTileCommands()
        {
            if (!_tiles.TryGetValue(_selectedCell, out MainBuildingRoomTile tile) || tile == null || !tile.HasCommands)
            {
                TownInteriorScreenUI?.HideBuildPanelExternally();
                return;
            }

            ShowTileCommands(tile);
        }

        private void AlignTilesToGrid()
        {
            for (int i = 0; i < _orderedCells.Count; i++)
            {
                Vector2Int cell = _orderedCells[i];
                if (!_tiles.TryGetValue(cell, out MainBuildingRoomTile tile) || tile == null)
                {
                    continue;
                }

                tile.transform.localPosition = GetCellLocalPosition(cell);
            }
        }

        private bool IsCellEmpty(Vector2Int cell)
        {
            ResolveReferences();
            if (GridManager == null || GridManager.Tilemap == null || GridManager.Tilemap.Tiles == null)
            {
                return true;
            }
            
            return GridManager.IsCellEmpty(cell);
        }

        public bool TryRemoveSelectedObstacle(Vector2Int cell)
        {
            ResolveReferences();
            if (GridManager == null || _costManager == null)
            {
                return false;
            }

            TownObstacle obstacle = GetObstacle(cell);
            if (obstacle == null || !obstacle.TryRemove(_costManager))
            {
                return false;
            }

            _hasSelectedCell = false;
            TownInteriorScreenUI?.HideBuildPanelExternally();
            TownInteriorScreenUI?.HideObjectDetailsExternally();
            RefreshTiles();
            return true;
        }

        public bool TryBuildTownObjectAtCell(TownTileObjectDataSO data, Vector2Int cell, TownObstacle obstacleToReplace = null)
        {
            ResolveReferences();
            if (GridManager == null || _costManager == null || data == null || data.Prefab == null)
            {
                return false;
            }

            if (obstacleToReplace != null && obstacleToReplace.GridPosition != cell)
            {
                return false;
            }

            if (obstacleToReplace == null && !GridManager.IsCellEmpty(cell))
            {
                return false;
            }

            List<TownTileObjectDataSO.Entry> buildCosts = data.GetResolvedBuildCosts();
            if (buildCosts != null && !_costManager.TryPayAll(buildCosts))
            {
                return false;
            }

            // 장애물 위 건설인 경우 기존 장애물을 먼저 제거한 뒤 같은 칸에 설치합니다.
            if (obstacleToReplace != null)
            {
                GridManager.TryClear(cell, obstacleToReplace);
                Destroy(obstacleToReplace.gameObject);
            }

            TownTileObject spawnedObject = Instantiate(data.Prefab, GridManager.CellToObjectWorld(cell), Quaternion.identity);
            spawnedObject.BindData(data);
            spawnedObject.BindSceneServices(GridManager, _logManager);
            if (!spawnedObject.Initialize(cell))
            {
                Destroy(spawnedObject.gameObject);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(data.SaveKey))
            {
                _saveManager?.RegisterPlacementForSave(spawnedObject, data.SaveKey);
            }

            _hasSelectedCell = false;
            TownInteriorScreenUI?.HideBuildPanelExternally();
            TownInteriorScreenUI?.HideObjectDetailsExternally();
            RefreshTiles();
            return true;
        }

        public bool TryUpgradeTownObjectAtCell(Vector2Int cell)
        {
            ResolveReferences();
            if (GridManager == null || _costManager == null)
            {
                return false;
            }

            TownTileObject currentObject = GetTileObject(cell);
            if (currentObject == null || currentObject.Data == null || currentObject.Data.GetResolvedNextUpgrade() == null)
            {
                return false;
            }

            TownTileObjectDataSO nextUpgrade = currentObject.Data.GetResolvedNextUpgrade();
            if (nextUpgrade.Prefab == null)
            {
                return false;
            }

            List<TownTileObjectDataSO.Entry> upgradeCosts = currentObject.Data.GetResolvedUpgradeCosts();
            if (upgradeCosts != null && !_costManager.TryPayAll(upgradeCosts))
            {
                return false;
            }

            // 프리팹이 동일하면 인스턴스는 유지하고 데이터만 상위 레벨로 바꿉니다.
            if (nextUpgrade.Prefab == currentObject.Data.Prefab)
            {
                currentObject.BindData(nextUpgrade);
                if (!string.IsNullOrWhiteSpace(nextUpgrade.SaveKey))
                {
                    _saveManager?.RegisterPlacementForSave(currentObject, nextUpgrade.SaveKey);
                }

                _selectedCell = cell;
                _hasSelectedCell = true;
                ShowObjectCommands(currentObject);
                RefreshTiles();
                _saveManager?.SaveGame();
                return true;
            }

            string runtimeSaveId = currentObject.RuntimeSaveId;

            // 프리팹이 달라지면 기존 오브젝트를 지우고 새 업그레이드 프리팹을 같은 칸에 배치합니다.
            GridManager.TryClear(cell, currentObject);
            Destroy(currentObject.gameObject);

            TownTileObject upgradedObject = Instantiate(nextUpgrade.Prefab, GridManager.CellToObjectWorld(cell), Quaternion.identity);
            upgradedObject.BindData(nextUpgrade);
            upgradedObject.BindSceneServices(GridManager, _logManager);
            if (!upgradedObject.Initialize(cell))
            {
                Destroy(upgradedObject.gameObject);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(nextUpgrade.SaveKey))
            {
                _saveManager?.RegisterPlacementForSave(upgradedObject, nextUpgrade.SaveKey);
            }

            if (!string.IsNullOrWhiteSpace(runtimeSaveId))
            {
                upgradedObject.BindRuntimeSaveId(runtimeSaveId);
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            ShowObjectCommands(upgradedObject);
            RefreshTiles();
            _saveManager?.SaveGame();
            return true;
        }

        public bool TryBuildAtCell(UnitDataSO unitData, Vector2Int cell)
        {
            ResolveReferences();
            if (_buildManager == null || GridManager == null || unitData == null)
            {
                return false;
            }

            if (!_buildManager.TryInstall(unitData, GridManager.CellToObjectWorld(cell), out _))
            {
                return false;
            }

            _hasSelectedCell = false;
            TownInteriorScreenUI?.HideBuildPanelExternally();
            TownInteriorScreenUI?.HideObjectDetailsExternally();
            RefreshTiles();
            return true;
        }

        private bool TryGetObstacle(Vector2Int cell, out TownObstacle obstacle)
        {
            obstacle = GetObstacle(cell);
            return obstacle != null;
        }

        private TownObstacle GetObstacle(Vector2Int cell)
        {
            return GridManager?.GetTile(cell)?.TileObject as TownObstacle;
        }

        private TownTileObject GetTileObject(Vector2Int cell)
        {
            return GridManager?.GetTile(cell)?.TileObject as TownTileObject;
        }

        public bool ShowPanelSection(TownObjectPanelSectionSO section, TownTileObjectDataSO tileObjectData)
        {
            if (TownInteriorScreenUI == null || section == null)
            {
                return false;
            }

            TownInteriorScreenUI.ShowObjectSectionWindow(tileObjectData, section);
            return true;
        }

        private List<TownObstacleDataSO> GetDefaultObstacleVariants()
        {
            List<TownObstacleDataSO> variants = new List<TownObstacleDataSO>();
            for (int i = 0; i < DefaultObstacleVariants.Count; i++)
            {
                TownObstacleDataSO variant = DefaultObstacleVariants[i];
                if (variant == null || variant.Prefab == null || variants.Contains(variant))
                {
                    continue;
                }

                variants.Add(variant);
            }

            if (variants.Count == 0 && DefaultObstacleData != null && DefaultObstacleData.Prefab != null)
            {
                variants.Add(DefaultObstacleData);
            }

            return variants;
        }

        private void SpawnTileObject(TownTileObjectDataSO data, Vector2Int cellPosition)
        {
            if (data == null || data.Prefab == null || !GridManager.IsCellEmpty(cellPosition))
            {
                if (data != null && data.Prefab != null && GridManager != null && !GridManager.IsCellEmpty(cellPosition))
                {
                    _logManager?.Building($"Town startup placement skipped at {cellPosition}: cell already occupied.");
                }

                return;
            }

            TownTileObject spawnedObject = Instantiate(
                data.Prefab,
                GridManager.CellToObjectWorld(cellPosition),
                Quaternion.identity);

            spawnedObject.BindData(data);
            spawnedObject.BindSceneServices(GridManager, GameManager.Instance?.GetManager<LogManager>());
            if (!spawnedObject.Initialize(cellPosition))
            {
                _logManager?.Building($"Town startup placement failed for `{data.name}` at {cellPosition}.", LogLevel.Error);
                Destroy(spawnedObject.gameObject);
                return;
            }

            _logManager?.Building($"Town startup placement spawned `{data.name}` at {cellPosition}.");

            if (!string.IsNullOrWhiteSpace(data.SaveKey))
            {
                _saveManager?.RegisterPlacementForSave(spawnedObject, data.SaveKey);
            }
        }

        private int GetTargetTileCount()
        {
            if (UseChildTileCount && transform.childCount > 0)
            {
                return transform.childCount;
            }

            return Mathf.Max(1, BoardColumns * BoardRows);
        }

        private Vector2Int GetBoardSize(int tileCount)
        {
            if (!UseChildTileCount || transform.childCount <= 0)
            {
                return new Vector2Int(Mathf.Max(1, BoardColumns), Mathf.Max(1, BoardRows));
            }

            int columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(tileCount)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(tileCount / (float)columns));
            return new Vector2Int(columns, rows);
        }

        private List<Vector2Int> CreateRectangularCells(int tileCount)
        {
            Vector2Int boardSize = GetBoardSize(tileCount);
            List<Vector2Int> cells = new List<Vector2Int>(tileCount);

            for (int y = boardSize.y - 1; y >= 0; y--)
            {
                for (int x = 0; x < boardSize.x; x++)
                {
                    if (cells.Count >= tileCount)
                    {
                        return cells;
                    }

                    cells.Add(BoardOrigin + new Vector2Int(x, y));
                }
            }

            return cells;
        }

        private List<Vector2Int> CreateSquareSpiralCells(int tileCount)
        {
            List<Vector2Int> cells = new List<Vector2Int>(tileCount);
            if (tileCount <= 0)
            {
                return cells;
            }

            Vector2Int current = BoardOrigin;
            cells.Add(current);

            Vector2Int[] directions =
            {
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.left,
                Vector2Int.down
            };

            int stepLength = 1;
            int directionIndex = 0;
            while (cells.Count < tileCount)
            {
                for (int repeat = 0; repeat < 2 && cells.Count < tileCount; repeat++)
                {
                    Vector2Int direction = directions[directionIndex % directions.Length];
                    for (int step = 0; step < stepLength && cells.Count < tileCount; step++)
                    {
                        current += direction;
                        cells.Add(current);
                    }

                    directionIndex++;
                }

                stepLength++;
            }

            return cells;
        }

        private void EnsureTileObjects(int targetTileCount)
        {
            for (int i = transform.childCount; i < targetTileCount; i++)
            {
                GameObject tileObject = new GameObject($"Tile_{i}");
                tileObject.transform.SetParent(transform, false);
            }

            for (int i = transform.childCount - 1; i >= targetTileCount; i--)
            {
                DestroyTileObject(transform.GetChild(i).gameObject);
            }
        }

        private Vector3 GetTileVisualScale()
        {
            return new Vector3(
                Mathf.Max(0.1f, TileScale.x),
                Mathf.Max(0.1f, TileScale.y),
                1f);
        }

        private Sprite GetTileSprite()
        {
            if (_tileSprite != null)
            {
                return _tileSprite;
            }

            int resolution = Mathf.Max(8, tileOutlineResolution);
            int thickness = Mathf.Clamp(tileOutlineThickness, 1, resolution / 2);
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = "MainBuildingRoomTileTexture";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color border = Color.white;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    bool isBorder =
                        x < thickness ||
                        y < thickness ||
                        x >= resolution - thickness ||
                        y >= resolution - thickness;

                    texture.SetPixel(x, y, isBorder ? border : clear);
                }
            }

            texture.Apply();
            _tileSprite = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
            _tileSprite.name = "MainBuildingRoomTileSprite";
            return _tileSprite;
        }

        private Material GetGlowMaterial()
        {
            if (_glowMaterial != null)
            {
                return _glowMaterial;
            }

            Shader shader = Shader.Find("Custom/TownTileGlow");
            if (shader == null)
            {
                return null;
            }

            _glowMaterial = new Material(shader);
            _glowMaterial.name = "TownTileGlowMaterial";
            _glowMaterial.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            _glowMaterial.SetFloat("_Intensity", glowIntensity);
            return _glowMaterial;
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
