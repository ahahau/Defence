using System;
using System.Collections.Generic;
using _01.Code.Commands;
using _01.Code.Commands.Battle;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Tiles;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.UI
{
    public class BattleCommandPanelController : MonoBehaviour
    {
        [SerializeField] private BattleCommandPanelUI emptyTilePanelUi;
        [SerializeField] private BattleCommandPanelUI selectionPanelUi;
        [SerializeField] private string groundCommandTitle = "BUILD";
        [SerializeField] private List<BaseCommandSO> groundCommands = new();

        private BuildManager _buildManager;
        private CostManager _costManager;
        private GridManager _gridManager;
        private SaveManager _saveManager;
        private GameEventChannelSO _uiEventChannel;
        private BattleCommandPanelUI _emptyTilePanelUi;
        private BattleCommandPanelUI _selectionPanelUi;
        private CommandContext _commandContext;
        private Vector2Int _selectedCell;
        private PlaceableEntity _selectedEntity;
        private bool _hasSelection;
        private UnitPanelUI _unitPanelUi;

        public void Initialize(BuildManager buildManager, GridManager gridManager, SaveManager saveManager)
        {
            _buildManager = buildManager;
            _gridManager = gridManager;
            _saveManager = saveManager;
            _costManager = GameManager.Instance?.GetManager<CostManager>();
            _uiEventChannel = _buildManager != null ? _buildManager.UiEventChannel : null;
            EnsurePanel();
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
            _selectionPanelUi?.HidePanel();
            ShowGroundCommands();
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
            _unitPanelUi?.HidePanel();
            ShowSelectionCommands(ResolveSelectedEntityCommandTitle(entity), true);
            return true;
        }

        public void HidePanel()
        {
            _hasSelection = false;
            _selectedEntity = null;
            _unitPanelUi?.HidePanel();
            _emptyTilePanelUi?.HidePanel();
            _selectionPanelUi?.HidePanel();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void EnsurePanel()
        {
            _emptyTilePanelUi ??= emptyTilePanelUi;
            _selectionPanelUi ??= selectionPanelUi;
        }

        private void ShowGroundCommands()
        {
            EnsurePanel();
            if (_emptyTilePanelUi == null)
            {
                return;
            }

            _commandContext = CreateCommandContext();
            _emptyTilePanelUi.ShowCommands(groundCommandTitle, groundCommands, _commandContext);
        }

        private void ShowSelectionCommands(string title, bool includeRemoveCommand)
        {
            EnsurePanel();
            if (_selectionPanelUi == null)
            {
                return;
            }

            List<BaseCommandSO> commands;
            if (_selectedEntity is TownTileObject selectedTileObject)
            {
                commands = selectedTileObject.Data != null ? selectedTileObject.Data.Commands : null;
            }
            else if (includeRemoveCommand && _selectedEntity != null)
            {
                commands = ResolveSelectedEntityCommands();
            }
            else
            {
                commands = null;
            }

            _commandContext = CreateCommandContext();
            _selectionPanelUi.ShowCommands(title, commands, _commandContext);
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
                Debug.Log($"BattleCommandPanel HandleTownCommandSelected ignored. hasSelection={_hasSelection}, evtNull={evt == null}, commandNull={evt?.Command == null}, gridNull={_gridManager == null}");
                return;
            }

            Debug.Log($"BattleCommandPanel HandleTownCommandSelected command={evt.Command.GetType().Name}, selectedEntity={_selectedEntity?.name}, selectedCell={_selectedCell}");

            CommandContext context = CreateCommandContext();
            if (!evt.Command.CanExecute(context))
            {
                return;
            }

            evt.Command.Execute(context);
        }

        private bool TryRemoveSelectedEntity()
        {
            if (_selectedEntity == null || _gridManager == null)
            {
                return false;
            }

            Vector2Int gridPosition = _selectedEntity.GridPosition;
            if (!_gridManager.TryClear(gridPosition, _selectedEntity))
            {
                return false;
            }

            Destroy(_selectedEntity.gameObject);
            _saveManager?.SaveGame();
            HidePanel();
            return true;
        }

        private bool TryBuildSelectedTileObject(TownTileObjectDataSO buildingData)
        {
            if (_selectedEntity == null || _gridManager == null || buildingData == null || buildingData.Prefab == null)
            {
                return false;
            }

            Vector2Int gridPosition = _selectedEntity.GridPosition;
            if (!_gridManager.TryClear(gridPosition, _selectedEntity))
            {
                return false;
            }

            // 자원 타일 위 건설은 기존 오브젝트를 지우고 같은 칸에 새 건물을 배치합니다.
            Destroy(_selectedEntity.gameObject);

            TownTileObject spawnedObject = Instantiate(buildingData.Prefab, _gridManager.CellToObjectWorld(gridPosition), Quaternion.identity);
            spawnedObject.BindData(buildingData);
            spawnedObject.BindSceneServices(_gridManager, GameManager.Instance?.GetManager<LogManager>());
            if (!spawnedObject.Initialize(gridPosition))
            {
                Destroy(spawnedObject.gameObject);
                return false;
            }

            _saveManager?.RegisterPlacementForSave(spawnedObject, buildingData.SaveKey);
            _saveManager?.SaveGame();
            HidePanel();
            return true;
        }

        private bool TryUpgradeSelectedTileObject()
        {
            if (_selectedEntity is not TownTileObject currentBuilding ||
                _gridManager == null ||
                currentBuilding.Data == null ||
                currentBuilding.Data.GetResolvedNextUpgrade() == null)
            {
                Debug.Log(
                    $"BattleCommandPanel TryUpgradeSelectedTileObject aborted. " +
                    $"selectedEntityType={_selectedEntity?.GetType().Name}, gridNull={_gridManager == null}, " +
                    $"dataNull={( _selectedEntity as TownTileObject )?.Data == null}, nextUpgradeNull={( _selectedEntity as TownTileObject )?.Data?.GetResolvedNextUpgrade() == null}");
                return false;
            }

            TownTileObjectDataSO nextUpgrade = currentBuilding.Data.GetResolvedNextUpgrade();
            if (nextUpgrade.Prefab == null)
            {
                Debug.Log($"BattleCommandPanel TryUpgradeSelectedTileObject aborted: nextUpgrade prefab missing for {currentBuilding.name}");
                return false;
            }

            List<TownTileObjectDataSO.Entry> upgradeCosts = currentBuilding.Data.GetResolvedUpgradeCosts();
            if (upgradeCosts != null &&
                (_costManager == null || !_costManager.TryPayAll(upgradeCosts)))
            {
                Debug.Log($"BattleCommandPanel TryUpgradeSelectedTileObject aborted: could not pay upgrade cost for {currentBuilding.name}");
                return false;
            }

            // 레벨만 바뀌고 프리팹은 같은 경우에는 오브젝트를 다시 만들지 않고 데이터만 교체합니다.
            if (nextUpgrade.Prefab == currentBuilding.Data.Prefab)
            {
                currentBuilding.BindData(nextUpgrade);
                Debug.Log($"BattleCommandPanel upgraded in place: {currentBuilding.name} -> {nextUpgrade.DisplayName}");
                if (currentBuilding is BattleTownBuilding battleTownBuilding)
                {
                    Debug.Log($"BattleCommandPanel upgraded level now {battleTownBuilding.level}");
                }
                _saveManager?.RegisterPlacementForSave(currentBuilding, nextUpgrade.SaveKey);
                _saveManager?.SaveGame();
                HidePanel();
                return true;
            }

            Vector2Int gridPosition = currentBuilding.GridPosition;
            string runtimeSaveId = currentBuilding.RuntimeSaveId;
            if (!_gridManager.TryClear(gridPosition, currentBuilding))
            {
                return false;
            }

            // 프리팹이 달라지는 업그레이드는 같은 칸에서 런타임 오브젝트를 교체합니다.
            Destroy(currentBuilding.gameObject);

            TownTileObject upgradedObject = Instantiate(nextUpgrade.Prefab, _gridManager.CellToObjectWorld(gridPosition), Quaternion.identity);
            upgradedObject.BindData(nextUpgrade);
            upgradedObject.BindSceneServices(_gridManager, GameManager.Instance?.GetManager<LogManager>());
            if (!upgradedObject.Initialize(gridPosition))
            {
                Destroy(upgradedObject.gameObject);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(runtimeSaveId))
            {
                upgradedObject.BindRuntimeSaveId(runtimeSaveId);
            }

            _saveManager?.RegisterPlacementForSave(upgradedObject, nextUpgrade.SaveKey);
            _saveManager?.SaveGame();
            HidePanel();
            return true;
        }

        private bool IsBattleScene()
        {
            return gameObject.scene.name.IndexOf("Battle", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void EnsureUnitPanel()
        {
            if (_unitPanelUi == null)
            {
                _unitPanelUi = FindFirstObjectByType<UnitPanelUI>(FindObjectsInactive.Include);
            }
        }

        private CommandContext CreateCommandContext()
        {
            _commandContext = new CommandContext(
                null,
                _buildManager,
                _costManager,
                _selectedCell,
                null,
                ShowUnitBuildSelection,
                TryBuildSelectedTileObject,
                TryUpgradeSelectedTileObject,
                TryRemoveSelectedEntity);
            return _commandContext;
        }

        private bool ShowUnitBuildSelection(UnitDataSO _)
        {
            EnsureUnitPanel();
            if (_unitPanelUi == null)
            {
                return false;
            }

            _unitPanelUi.ShowInstallPanel(_selectedCell);
            _emptyTilePanelUi?.HidePanel();
            _selectionPanelUi?.HidePanel();

            return true;
        }

        private List<BaseCommandSO> ResolveSelectedEntityCommands()
        {
            if (_selectedEntity == null)
            {
                return null;
            }

            PlaceableCommandSource commandSource = _selectedEntity.GetComponent<PlaceableCommandSource>();
            return commandSource != null ? commandSource.Commands : null;
        }

        private string ResolveSelectedEntityCommandTitle(PlaceableEntity entity)
        {
            if (entity == null)
            {
                return "COMMAND";
            }

            PlaceableCommandSource commandSource = entity.GetComponent<PlaceableCommandSource>();
            if (commandSource != null && !string.IsNullOrWhiteSpace(commandSource.CommandTitle))
            {
                return commandSource.CommandTitle;
            }

            return entity.name.ToUpperInvariant();
        }
    }
}
