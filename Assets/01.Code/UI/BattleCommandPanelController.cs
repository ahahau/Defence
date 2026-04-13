using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Buildings;
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
        private const int LoggingGroundCommandSlot = 1;
        private const int QuarryCommandSlot = 1;

        [SerializeField] private TownInteriorScreenUI panelUi;

        private readonly List<TownCommandSO> _buildCommands = new();
        private readonly List<TownCommandSO> _obstacleCommands = new();

        private BuildManager _buildManager;
        private GridManager _gridManager;
        private SaveManager _saveManager;
        private GameEventChannelSO _uiEventChannel;
        private TownInteriorScreenUI _panelUi;
        private BattleRemovePlacementCommandSO _removeCommand;
        private TownBuildTileObjectCommandSO _loggingGroundCommand;
        private TownBuildTileObjectCommandSO _quarryCommand;
        private TownBuildingDataSO _loggingGroundData;
        private TownBuildingDataSO _quarryData;
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

            _loggingGroundData ??= Resources.Load<TownBuildingDataSO>("Town/LoggingGroundData");
            if (_loggingGroundData != null)
            {
                _loggingGroundCommand = ScriptableObject.CreateInstance<TownBuildTileObjectCommandSO>();
                _loggingGroundCommand.ConfigureRuntime(_loggingGroundData, LoggingGroundCommandSlot);
                _loggingGroundCommand.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }

            _quarryData ??= Resources.Load<TownBuildingDataSO>("Town/QuarryData");
            if (_quarryData != null)
            {
                _quarryCommand = ScriptableObject.CreateInstance<TownBuildTileObjectCommandSO>();
                _quarryCommand.ConfigureRuntime(_quarryData, QuarryCommandSlot);
                _quarryCommand.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            }
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
            if (includeRemoveCommand && _selectedEntity != null)
            {
                commands = BuildObstacleCommands();
            }
            else
            {
                commands = new List<TownCommandSO>(_buildCommands);
            }

            _panelUi.ShowCommands(title, commands, null);
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

            if (evt.Command == _loggingGroundCommand)
            {
                TryBuildLoggingGround();
                return;
            }

            if (evt.Command == _quarryCommand)
            {
                TryBuildQuarry();
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

            if (CanBuildLoggingGroundOnSelectedEntity())
            {
                _obstacleCommands.Add(_loggingGroundCommand);
            }

            if (CanBuildQuarryOnSelectedEntity())
            {
                _obstacleCommands.Add(_quarryCommand);
            }

            return _obstacleCommands;
        }

        private bool CanBuildLoggingGroundOnSelectedEntity()
        {
            if (_selectedEntity == null || _loggingGroundCommand == null || _loggingGroundData == null)
            {
                return false;
            }

            string entityName = _selectedEntity.name ?? string.Empty;
            return entityName.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool CanBuildQuarryOnSelectedEntity()
        {
            if (_selectedEntity == null || _quarryCommand == null || _quarryData == null)
            {
                return false;
            }

            string entityName = _selectedEntity.name ?? string.Empty;
            return entityName.IndexOf("rock", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void TryBuildLoggingGround()
        {
            TryBuildSelectedTileObject(_loggingGroundData);
        }

        private void TryBuildQuarry()
        {
            TryBuildSelectedTileObject(_quarryData);
        }

        private void TryBuildSelectedTileObject(TownBuildingDataSO buildingData)
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

        private bool IsBattleScene()
        {
            return gameObject.scene.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
