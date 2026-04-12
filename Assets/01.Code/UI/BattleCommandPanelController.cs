using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.TownCommands;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.UI
{
    public class BattleCommandPanelController : MonoBehaviour
    {
        private const int MaxBuildCommandCount = 4;
        private const int RemoveCommandSlot = 4;

        private readonly List<TownCommandSO> _buildCommands = new();

        private BuildManager _buildManager;
        private GridManager _gridManager;
        private SaveManager _saveManager;
        private GameEventChannelSO _uiEventChannel;
        private TownInteriorScreenUI _panelUi;
        private BattleRemovePlacementCommandSO _removeCommand;
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
            if (_panelUi != null)
            {
                return;
            }

            Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                return;
            }

            _panelUi = canvas.GetComponentInChildren<TownInteriorScreenUI>(true);
            if (_panelUi != null)
            {
                return;
            }

            GameObject panelRoot = new GameObject("BattleCommandPanelUI", typeof(RectTransform), typeof(TownInteriorScreenUI));
            panelRoot.transform.SetParent(canvas.transform, false);
            _panelUi = panelRoot.GetComponent<TownInteriorScreenUI>();
        }

        private void RebuildCommands()
        {
            _buildCommands.Clear();

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

            List<TownCommandSO> commands = new List<TownCommandSO>(_buildCommands);
            if (includeRemoveCommand && _selectedEntity != null)
            {
                commands.Add(_removeCommand);
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

        private bool IsBattleScene()
        {
            return gameObject.scene.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
