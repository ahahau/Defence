using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Buildings;
using _01.Code.Cost;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.TownCommands;
using _01.Code.Tiles;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.UI
{
    public class BattleCommandPanelController : MonoBehaviour
    {
        private const int MaxBuildCommandCount = 4;
        private const int RemoveCommandSlot = 0;

        [SerializeField] private TownInteriorScreenUI panelUi;
        [SerializeField] private BattleBuildingCatalogSO buildingCatalog;

        private readonly List<TownCommandSO> _buildCommands = new();
        private readonly List<TownCommandSO> _obstacleCommands = new();
        private readonly List<TownCommandSO> _placedObjectCommands = new();
        private readonly List<BattleTileBuildingDataSO> _battleTileBuildingData = new();

        private BuildManager _buildManager;
        private CostManager _costManager;
        private GridManager _gridManager;
        private SaveManager _saveManager;
        private GameEventChannelSO _uiEventChannel;
        private TownInteriorScreenUI _panelUi;
        private BattleBuildingCatalogSO _buildingCatalog;
        private BattleRemovePlacementCommandSO _removeCommand;
        private TownUpgradeTileObjectCommandSO _upgradeCommand;
        private TownCommandContext _commandContext;
        private Vector2Int _selectedCell;
        private PlaceableEntity _selectedEntity;
        private bool _hasSelection;

        public bool IsPointerOverPanel()
        {
            return _panelUi != null && _panelUi.IsPointerOverBuildPanel();
        }

        public void Initialize(BuildManager buildManager, GridManager gridManager, SaveManager saveManager)
        {
            _buildManager = buildManager;
            _gridManager = gridManager;
            _saveManager = saveManager;
            _costManager = GameManager.Instance?.GetManager<CostManager>();
            _uiEventChannel = _buildManager != null ? _buildManager.UiEventChannel : null;
            EnsurePanel();
            RebuildCommands();
            Subscribe();
        }

        public bool HandleGroundClicked(Vector2 worldPosition)
        {
            if (!IsBattleScene() || _gridManager == null)
            {
                return false;
            }

            if (_buildManager != null && _buildManager.SelectedUnit != null)
            {
                HidePanel();
                return false;
            }

            Vector2Int cell = _gridManager.WorldToPlacementCell(worldPosition);
            if (!_gridManager.ContainsCell(cell))
            {
                HidePanel();
                return false;
            }

            if (_hasSelection && _selectedEntity == null && _selectedCell == cell)
            {
                HidePanel();
                return true;
            }

            _selectedCell = cell;
            _selectedEntity = null;
            _hasSelection = true;
            ShowCommands("BUILD", false);
            return true;
        }

        public bool HandlePlaceableClicked(PlaceableEntity entity)
        {
            if (!IsBattleScene() || entity == null)
            {
                return false;
            }

            if (_hasSelection && _selectedEntity == entity)
            {
                HidePanel();
                return true;
            }

            _selectedCell = entity.GridPosition;
            _selectedEntity = entity;
            _hasSelection = true;
            ShowCommands(entity.name.ToUpperInvariant(), true);
            return true;
        }

        public void HidePanel()
        {
            _hasSelection = false;
            _selectedEntity = null;
            if (_panelUi != null)
            {
                _panelUi.HideBuildPanelExternally();
                _panelUi.HideObjectDetailsExternally();
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void EnsurePanel()
        {
            _panelUi ??= panelUi;
            if (_panelUi == null)
            {
                _panelUi = FindFirstObjectByType<TownInteriorScreenUI>(FindObjectsInactive.Include);
                panelUi = _panelUi;
            }

            _buildingCatalog ??= buildingCatalog;
            buildingCatalog = _buildingCatalog;
        }

        private void RebuildCommands()
        {
            _buildCommands.Clear();
            _obstacleCommands.Clear();

            IReadOnlyList<UnitDataSO> availableBuildings = _buildManager != null ? _buildManager.GetAvailableBuildingsForCurrentScene() : null;
            if (availableBuildings != null)
            {
                int commandCount = Math.Min(MaxBuildCommandCount, availableBuildings.Count);
                for (int i = 0; i < commandCount; i++)
                {
                    UnitDataSO buildingData = availableBuildings[i];
                    if (buildingData == null)
                    {
                        continue;
                    }

                    TownBuildCommandSO buildCommand = ScriptableObject.CreateInstance<TownBuildCommandSO>();
                    buildCommand.ConfigureRuntime(buildingData, i);
                    buildCommand.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                    _buildCommands.Add(buildCommand);
                }
            }

            _removeCommand = ScriptableObject.CreateInstance<BattleRemovePlacementCommandSO>();
            _removeCommand.ConfigureRuntime(RemoveCommandSlot);
            _removeCommand.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        private void ShowCommands(string title, bool includeRemoveCommand)
        {
            EnsurePanel();
            RebuildCommands();
            if (_panelUi == null)
            {
                return;
            }

            List<TownCommandSO> commands;
            if (_selectedEntity is BattleTownBuilding selectedBuilding)
            {
                commands = BuildPlacedObjectCommands(selectedBuilding);
            }
            else if (includeRemoveCommand && _selectedEntity != null)
            {
                commands = BuildObstacleCommands();
            }
            else
            {
                commands = new List<TownCommandSO>(_buildCommands);
            }

            _commandContext = new TownCommandContext(null, _buildManager, _costManager, _selectedCell, null);
            _panelUi.ShowCommands(title, commands, _commandContext);
        }

        private void Subscribe()
        {
            if (_uiEventChannel == null)
            {
                return;
            }

            _uiEventChannel.RemoveListener<TownCommandSelectedEvent>(HandleTownCommandSelected);
            _uiEventChannel.AddListener<TownCommandSelectedEvent>(HandleTownCommandSelected);
        }

        private void Unsubscribe()
        {
            if (_uiEventChannel == null)
            {
                return;
            }

            _uiEventChannel.RemoveListener<TownCommandSelectedEvent>(HandleTownCommandSelected);
        }

        private void HandleTownCommandSelected(TownCommandSelectedEvent evt)
        {
            if (!_hasSelection || evt == null || evt.Command == null || _gridManager == null)
            {
                return;
            }

            if (evt.Command is TownBuildCommandSO buildCommand)
            {
                if (buildCommand.BuildingData == null || _buildManager == null)
                {
                    return;
                }

                if (_buildManager.TryInstall(buildCommand.BuildingData, _gridManager.CellToObjectWorld(_selectedCell), out _))
                {
                    HidePanel();
                }

                return;
            }

            if (evt.Command == _removeCommand)
            {
                TryRemoveSelectedEntity();
                return;
            }

            if (evt.Command == _upgradeCommand)
            {
                TryUpgradeSelectedTileObject();
                return;
            }

            if (evt.Command is TownBuildTileObjectCommandSO tileObjectCommand)
            {
                TryBuildSelectedTileObject(tileObjectCommand.BuildingData);
            }
        }

        private void TryRemoveSelectedEntity()
        {
            if (_selectedEntity == null || _gridManager == null)
            {
                return;
            }

            Vector2Int gridPosition = _selectedEntity.GridPosition;
            if (!_gridManager.TryClear(gridPosition, _selectedEntity))
            {
                return;
            }

            Destroy(_selectedEntity.gameObject);
            _saveManager?.SaveGame();
            HidePanel();
        }

        private List<TownCommandSO> BuildObstacleCommands()
        {
            _obstacleCommands.Clear();
            _obstacleCommands.Add(_removeCommand);

            EnsureBattleTileBuildingData();
            int slot = 1;
            for (int i = 0; i < _battleTileBuildingData.Count && slot < MaxBuildCommandCount; i++)
            {
                BattleTileBuildingDataSO buildingData = _battleTileBuildingData[i];
                if (!CanBuildTileObjectOnSelectedEntity(buildingData))
                {
                    continue;
                }

                TownBuildTileObjectCommandSO command = ScriptableObject.CreateInstance<TownBuildTileObjectCommandSO>();
                command.ConfigureRuntime(buildingData, slot);
                command.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                _obstacleCommands.Add(command);
                slot++;
            }

            return _obstacleCommands;
        }

        private List<TownCommandSO> BuildPlacedObjectCommands(BattleTownBuilding building)
        {
            _placedObjectCommands.Clear();
            if (building == null || building.Data == null)
            {
                return _placedObjectCommands;
            }

            int slot = 0;
            if (building.Data.GetResolvedNextUpgrade() != null)
            {
                _upgradeCommand = ScriptableObject.CreateInstance<TownUpgradeTileObjectCommandSO>();
                _upgradeCommand.ConfigureRuntime(building.Data, slot);
                _upgradeCommand.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                _placedObjectCommands.Add(_upgradeCommand);
                slot++;
            }

            _removeCommand.ConfigureRuntime(slot);
            _placedObjectCommands.Add(_removeCommand);
            return _placedObjectCommands;
        }

        private void EnsureBattleTileBuildingData()
        {
            if (_battleTileBuildingData.Count > 0)
            {
                return;
            }

            EnsurePanel();
            List<BattleTileBuildingDataSO> loadedData = _buildingCatalog != null
                ? _buildingCatalog.GetBuildingsForScope(BuildSceneScope.Battle)
                : new List<BattleTileBuildingDataSO>();

            for (int i = 0; i < loadedData.Count; i++)
            {
                BattleTileBuildingDataSO buildingData = loadedData[i];
                if (buildingData == null || !IsBattleTileBuildingData(buildingData))
                {
                    continue;
                }

                _battleTileBuildingData.Add(buildingData);
            }

            _battleTileBuildingData.Sort(CompareBattleTileBuildingData);
        }

        private bool IsBattleTileBuildingData(BattleTileBuildingDataSO buildingData)
        {
            if (buildingData == null)
            {
                return false;
            }

            return buildingData.SceneScope == BuildSceneScope.Battle &&
                   buildingData.BattlePlacementKind != BattleTilePlacementKind.None;
        }

        private int CompareBattleTileBuildingData(BattleTileBuildingDataSO left, BattleTileBuildingDataSO right)
        {
            string leftName = left != null ? left.DisplayName ?? string.Empty : string.Empty;
            string rightName = right != null ? right.DisplayName ?? string.Empty : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private bool CanBuildTileObjectOnSelectedEntity(BattleTileBuildingDataSO buildingData)
        {
            if (_selectedEntity == null || buildingData == null)
            {
                return false;
            }

            if (buildingData.BattlePlacementKind == BattleTilePlacementKind.AnyObstacle)
            {
                return true;
            }

            BattleTilePlacementKind selectedKind = ResolveSelectedPlacementKind();
            if (selectedKind == BattleTilePlacementKind.None)
            {
                return false;
            }

            if (buildingData.RequiredSourcePrefab != null &&
                MatchesSelectedResourcePrefab(buildingData.RequiredSourcePrefab))
            {
                return true;
            }

            BattleTilePlacementKind requiredKind = ResolvePlacementKind(buildingData.RequiredSourcePrefab);
            if (requiredKind != BattleTilePlacementKind.None)
            {
                return requiredKind == selectedKind;
            }

            return buildingData.BattlePlacementKind switch
            {
                BattleTilePlacementKind.Tree => selectedKind == BattleTilePlacementKind.Tree,
                BattleTilePlacementKind.Rock => selectedKind == BattleTilePlacementKind.Rock,
                _ => false
            };
        }

        private BattleTilePlacementKind ResolveSelectedPlacementKind()
        {
            if (_selectedEntity == null)
            {
                return BattleTilePlacementKind.None;
            }

            return ResolvePlacementKind(_selectedEntity);
        }

        private BattleTilePlacementKind ResolvePlacementKind(PlaceableEntity entity)
        {
            if (entity == null)
            {
                return BattleTilePlacementKind.None;
            }

            if (MatchesResourcePrefab(entity, _gridManager != null ? _gridManager.TreePrefab : null))
            {
                return BattleTilePlacementKind.Tree;
            }

            if (MatchesResourcePrefab(entity, _gridManager != null ? _gridManager.RockPrefab : null))
            {
                return BattleTilePlacementKind.Rock;
            }

            string normalizedName = NormalizeEntityName(entity.name);
            if (normalizedName.Contains("tree"))
            {
                return BattleTilePlacementKind.Tree;
            }

            if (normalizedName.Contains("rock"))
            {
                return BattleTilePlacementKind.Rock;
            }

            return BattleTilePlacementKind.None;
        }

        private bool MatchesSelectedResourcePrefab(PlaceableEntity resourcePrefab)
        {
            return MatchesResourcePrefab(_selectedEntity, resourcePrefab);
        }

        private bool MatchesResourcePrefab(PlaceableEntity entity, PlaceableEntity resourcePrefab)
        {
            if (entity == null || resourcePrefab == null)
            {
                return false;
            }

            string entityName = NormalizeEntityName(entity.name);
            string prefabName = NormalizeEntityName(resourcePrefab.name);
            return !string.IsNullOrWhiteSpace(prefabName) &&
                   (entityName.Contains(prefabName) || prefabName.Contains(entityName));
        }

        private string NormalizeEntityName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("(Clone)", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim()
                .ToLowerInvariant();
        }

        private void TryBuildSelectedTileObject(TownTileObjectDataSO buildingData)
        {
            if (_selectedEntity == null || _gridManager == null || buildingData == null || buildingData.Prefab == null)
            {
                return;
            }

            Vector2Int gridPosition = _selectedEntity.GridPosition;
            if (!_gridManager.TryClear(gridPosition, _selectedEntity))
            {
                return;
            }

            Destroy(_selectedEntity.gameObject);

            TownTileObject spawnedObject = Instantiate(buildingData.Prefab, _gridManager.CellToObjectWorld(gridPosition), Quaternion.identity);
            spawnedObject.BindData(buildingData);
            spawnedObject.BindSceneServices(_gridManager, GameManager.Instance?.GetManager<LogManager>());
            if (!spawnedObject.Initialize(gridPosition))
            {
                Destroy(spawnedObject.gameObject);
                return;
            }

            _saveManager?.RegisterPlacementForSave(spawnedObject, buildingData.SaveKey);
            _saveManager?.SaveGame();
            HidePanel();
        }

        private void TryUpgradeSelectedTileObject()
        {
            if (_selectedEntity is not BattleTownBuilding currentBuilding ||
                _gridManager == null ||
                currentBuilding.Data == null ||
                currentBuilding.Data.GetResolvedNextUpgrade() == null)
            {
                return;
            }

            TownTileObjectDataSO nextUpgrade = currentBuilding.Data.GetResolvedNextUpgrade();
            if (nextUpgrade.Prefab == null)
            {
                return;
            }

            CostBundleSO upgradeCosts = currentBuilding.Data.GetResolvedUpgradeCosts();
            if (upgradeCosts != null &&
                (_costManager == null || !_costManager.TryPayAll(upgradeCosts)))
            {
                return;
            }

            Vector2Int gridPosition = currentBuilding.GridPosition;
            string runtimeSaveId = currentBuilding.RuntimeSaveId;
            if (!_gridManager.TryClear(gridPosition, currentBuilding))
            {
                return;
            }

            Destroy(currentBuilding.gameObject);

            TownTileObject upgradedObject = Instantiate(nextUpgrade.Prefab, _gridManager.CellToObjectWorld(gridPosition), Quaternion.identity);
            upgradedObject.BindData(nextUpgrade);
            upgradedObject.BindSceneServices(_gridManager, GameManager.Instance?.GetManager<LogManager>());
            if (!upgradedObject.Initialize(gridPosition))
            {
                Destroy(upgradedObject.gameObject);
                return;
            }

            if (!string.IsNullOrWhiteSpace(runtimeSaveId))
            {
                upgradedObject.BindRuntimeSaveId(runtimeSaveId);
            }

            _saveManager?.RegisterPlacementForSave(upgradedObject, nextUpgrade.SaveKey);
            _saveManager?.SaveGame();
            HidePanel();
        }

        private bool IsBattleScene()
        {
            return gameObject.scene.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
