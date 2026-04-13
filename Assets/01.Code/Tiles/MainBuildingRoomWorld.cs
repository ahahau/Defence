using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.TownCommands;
using _01.Code.TownPanels;
using _01.Code.UI;
using _01.Code.Units;
using _01.Code.Buildings;
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
        private readonly List<TownCommandSO> _townBuildCommands = new();
        private readonly List<TownCommandSO> _townObjectCommands = new();
        private TownRemoveObstacleCommandSO _removeObstacleCommand;

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

            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (TownInteriorScreenUI != null && TownInteriorScreenUI.IsPointerOverBuildPanel())
            {
                return;
            }

            if (_lastHandledClickFrame == Time.frameCount)
            {
                return;
            }

            ResolveReferences();
            if (GridManager == null)
            {
                return;
            }

            ResolveWorldCamera();
            if (_worldCamera == null)
            {
                return;
            }

            Vector3 worldPosition = _worldCamera.ScreenToWorldPoint(Input.mousePosition);
            worldPosition.z = 0f;
            TryHandlePointerClick(worldPosition);
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
            if (GridManager != null && GridManager.Tilemap == null)
            {
                GridManager.Initialize(GameManager.Instance);
            }

            if (GridManager != null && _orderedCells.Count == 0)
            {
                EnsureBoard();
            }

            if (Application.isPlaying)
            {
                EnsureCommands();
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
            _lastHandledClickFrame = Time.frameCount;
            ResolveReferences();
            if (GameManager.Instance != null && GridManager != null && GridManager.Tilemap == null)
            {
                GridManager.Initialize(GameManager.Instance);
            }
            if (!Application.isPlaying || GridManager == null || GridManager.Tilemap == null)
            {
                return;
            }

            if (TryHandleSelectedUnitPlacement(cell))
            {
                return;
            }

            if (_hasSelectedCell && _selectedCell == cell)
            {
                _hasSelectedCell = false;
                TownInteriorScreenUI?.HideBuildPanelExternally();
                TownInteriorScreenUI?.HideObjectDetailsExternally();
                RefreshTiles();
                return;
            }

            if (!IsCellEmpty(cell))
            {
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
                if (tileObject != null && tileObject.Data != null && tileObject.Data.InteractionPanel != null)
                {
                    _selectedCell = cell;
                    _hasSelectedCell = true;
                    ShowObjectCommands(tileObject.Data);
                    RefreshTiles();
                    return;
                }

                _hasSelectedCell = false;
                TownInteriorScreenUI?.HideBuildPanelExternally();
                TownInteriorScreenUI?.HideObjectDetailsExternally();
                RefreshTiles();
                return;
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            TownInteriorScreenUI?.HideObjectDetailsExternally();
            ShowBuildCommands(cell);
            RefreshTiles();
        }

        private bool TryHandleSelectedUnitPlacement(Vector2Int cell)
        {
            if (_buildManager == null || _buildManager.SelectedUnit == null)
            {
                return false;
            }

            if (!GridManager.ContainsCell(cell))
            {
                return true;
            }

            _selectedCell = cell;
            _hasSelectedCell = true;
            TownInteriorScreenUI?.HideBuildPanelExternally();
            TownInteriorScreenUI?.HideObjectDetailsExternally();

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
            if (GridManager == null)
            {
                return false;
            }

            Vector2Int cell = GridManager.WorldToPlacementCell(worldPosition);
            if (!_tiles.ContainsKey(cell))
            {
                return false;
            }

            HandleTileClicked(cell);
            return true;
        }

        private bool TryHandlePointerClick(Vector2 worldPosition)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition);
            Array.Sort(hits, CompareHitPriority);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null)
                {
                    continue;
                }

                MainBuildingRoomTile roomTile = hit.GetComponentInParent<MainBuildingRoomTile>();
                if (roomTile != null && roomTile.transform.parent == transform)
                {
                    HandleTileClicked(roomTile.Cell);
                    return true;
                }

                TownTileObject tileObject = hit.GetComponentInParent<TownTileObject>();
                if (tileObject != null && TryHandleTileObjectClick(tileObject))
                {
                    return true;
                }
            }

            return TryHandleWorldClick(worldPosition);
        }

        private int CompareHitPriority(Collider2D left, Collider2D right)
        {
            int leftOrder = GetHitSortingOrder(left);
            int rightOrder = GetHitSortingOrder(right);
            if (leftOrder != rightOrder)
            {
                return rightOrder.CompareTo(leftOrder);
            }

            float leftDepth = left != null ? left.transform.position.z : float.MinValue;
            float rightDepth = right != null ? right.transform.position.z : float.MinValue;
            return leftDepth.CompareTo(rightDepth);
        }

        private int GetHitSortingOrder(Collider2D hit)
        {
            if (hit == null)
            {
                return int.MinValue;
            }

            SpriteRenderer spriteRenderer = hit.GetComponentInParent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                return int.MinValue;
            }

            return spriteRenderer.sortingOrder;
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

            TownCommandContext context = new TownCommandContext(this, _buildManager, _costManager, _selectedCell, GetObstacle(_selectedCell));
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

                ShowBuildCommands(_selectedCell);
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

        private void ShowBuildCommands(Vector2Int cell)
        {
            if (TownInteriorScreenUI == null)
            {
                return;
            }

            EnsureCommands();
            TownCommandContext context = new TownCommandContext(this, _buildManager, _costManager, cell, null);
            TownInteriorScreenUI.ShowCommands("COMMAND", _townBuildCommands, context);
        }

        private void ShowObstacleCommands(TownObstacle obstacle)
        {
            if (TownInteriorScreenUI == null || obstacle == null)
            {
                return;
            }

            EnsureCommands();
            TownObstacleDataSO obstacleData = obstacle.Data as TownObstacleDataSO;
            string title = obstacle.Data != null && !string.IsNullOrWhiteSpace(obstacle.Data.DisplayName)
                ? obstacle.Data.DisplayName.ToUpperInvariant()
                : "OBSTACLE";
            TownCommandContext context = new TownCommandContext(this, _buildManager, _costManager, obstacle.GridPosition, obstacle);
            TownInteriorScreenUI.ShowCommands(title, new List<TownCommandSO> { _removeObstacleCommand }, context);
        }

        private void ShowObjectCommands(TownTileObjectDataSO tileObjectData)
        {
            if (TownInteriorScreenUI == null || tileObjectData == null || tileObjectData.InteractionPanel == null)
            {
                return;
            }

            BuildTownObjectCommands(tileObjectData);
            string title = !string.IsNullOrWhiteSpace(tileObjectData.DisplayName)
                ? tileObjectData.DisplayName.ToUpperInvariant()
                : "OBJECT";
            TownCommandContext context = new TownCommandContext(this, _buildManager, _costManager, _selectedCell, null);
            TownInteriorScreenUI.HideObjectDetailsExternally();
            TownInteriorScreenUI.ShowCommands(title, _townObjectCommands, context);
        }

        private void EnsureCommands()
        {
            if (_removeObstacleCommand == null)
            {
                _removeObstacleCommand = ScriptableObject.CreateInstance<TownRemoveObstacleCommandSO>();
                _removeObstacleCommand.ConfigureRuntime(0);
                _removeObstacleCommand.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            IReadOnlyList<UnitDataSO> availableBuildings = _buildManager != null ? _buildManager.GetAvailableBuildingsForCurrentScene() : null;
            if (availableBuildings == null || _townBuildCommands.Count == availableBuildings.Count)
            {
                return;
            }

            _townBuildCommands.Clear();
            for (int i = 0; i < availableBuildings.Count && i < 5; i++)
            {
                UnitDataSO unitData = availableBuildings[i];
                if (unitData == null)
                {
                    continue;
                }

                TownBuildCommandSO buildCommand = ScriptableObject.CreateInstance<TownBuildCommandSO>();
                buildCommand.ConfigureRuntime(unitData, i);
                buildCommand.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                _townBuildCommands.Add(buildCommand);
            }
        }

        private void BuildTownObjectCommands(TownTileObjectDataSO tileObjectData)
        {
            _townObjectCommands.Clear();
            if (tileObjectData == null || tileObjectData.InteractionPanel == null || tileObjectData.InteractionPanel.Sections == null)
            {
                return;
            }

            for (int i = 0; i < tileObjectData.InteractionPanel.Sections.Count && i < 5; i++)
            {
                TownObjectPanelSectionSO section = tileObjectData.InteractionPanel.Sections[i];
                if (section == null)
                {
                    continue;
                }

                TownOpenPanelSectionCommandSO command = ScriptableObject.CreateInstance<TownOpenPanelSectionCommandSO>();
                command.ConfigureRuntime(section, tileObjectData, i);
                command.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                _townObjectCommands.Add(command);
            }
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

            if (data.BuildCosts != null && !_costManager.TryPayAll(data.BuildCosts))
            {
                return false;
            }

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
