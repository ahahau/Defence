using System;
using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.UI;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.Serialization;

namespace _01.Code.Manager
{
    public class BuildManager : MonoBehaviour, IManageable
    {
        [FormerlySerializedAs("unitEventChannel")]
        [SerializeField] private GameEventChannelSO uiEventChannel;
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private List<UnitDataSO> availableBuildings = new();
        [SerializeField] private List<UnitDataSO> availableUnits = new();

        private GridManager _gridManager;
        private LogManager _logManager;
        private UiCatalog _catalog;
        private UiBuildSelectionController _selectionController;

        public event Action<UnitDataSO, PlaceableEntity> OnBuildingInstalled;
        public event Action<UnitDataSO, Vector2Int> OnBuildFailed;
        public event Action OnBuildingMoved;
        public event Action OnBuildingMoveFailed;
        public UnitDataSO SelectedUnit => _selectionController != null ? _selectionController.SelectedUnit : null;

        public void Initialize(IManagerContainer managerContainer)
        {
            _gridManager = managerContainer.GetManager<GridManager>();
            _logManager = managerContainer.GetManager<LogManager>();
            _catalog = new UiCatalog(availableBuildings, availableUnits);
            _selectionController = new UiBuildSelectionController(
                buildEventChannel,
                CanUseDayActions,
                unitData => _catalog.IsSelectablePlacementForCurrentScene(unitData),
                null);
            buildEventChannel.AddListener<BuildRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.AddListener<BuildMoveRequestedEvent>(HandleMoveBuildingRequestedEvent);
            uiEventChannel.AddListener<UiSkipDayRequestedEvent>(HandleSkipDayRequestedEvent);
            uiEventChannel.AddListener<UiCancelSelectionRequestedEvent>(HandleCancelSelectionRequestedEvent);
            uiEventChannel.AddListener<UiBuildAtWorldPositionRequestedEvent>(HandleBuildAtWorldPositionRequestedEvent);
            uiEventChannel.AddListener<UiHoverCellChangedEvent>(HandleHoverCellChangedEvent);
            uiEventChannel.AddListener<UiUnitCatalogQueryEvent>(HandleUnitCatalogQueryEvent);
        }

        private void Update()
        {
            _selectionController?.Tick();
        }

        private void OnDestroy()
        {
            buildEventChannel.RemoveListener<BuildRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.RemoveListener<BuildMoveRequestedEvent>(HandleMoveBuildingRequestedEvent);
            uiEventChannel.RemoveListener<UiSkipDayRequestedEvent>(HandleSkipDayRequestedEvent);
            uiEventChannel.RemoveListener<UiCancelSelectionRequestedEvent>(HandleCancelSelectionRequestedEvent);
            uiEventChannel.RemoveListener<UiBuildAtWorldPositionRequestedEvent>(HandleBuildAtWorldPositionRequestedEvent);
            uiEventChannel.RemoveListener<UiHoverCellChangedEvent>(HandleHoverCellChangedEvent);
            uiEventChannel.RemoveListener<UiUnitCatalogQueryEvent>(HandleUnitCatalogQueryEvent);
            _selectionController?.Dispose();
        }

        public void ReplaceAvailableBuildings(IEnumerable<UnitDataSO> catalog)
        {
            availableBuildings.Clear();
            if (catalog == null)
            {
                return;
            }

            foreach (UnitDataSO unitData in catalog)
            {
                if (unitData != null)
                {
                    availableBuildings.Add(unitData);
                }
            }

            _catalog = new UiCatalog(availableBuildings, availableUnits);
        }

        public IReadOnlyList<UnitDataSO> GetAvailableBuildingsForCurrentScene()
        {
            return _catalog.GetAvailableBuildingsForCurrentScene();
        }

        public void SelectBuilding(UnitDataSO unitData)
        {
            _selectionController.SelectBuilding(unitData);
        }

        public void CancelSelection()
        {
            _selectionController.CancelSelection();
        }

        public bool TryRequestBuild(Vector3 worldPosition)
        {
            return _selectionController.TryRequestBuild(worldPosition);
        }

        public bool TryMove(PlaceableEntity placeableEntity, Vector3 worldPosition)
        {
            if (placeableEntity == null || _gridManager == null)
            {
                return false;
            }

            if (!CanModifyPlacements())
            {
                RaiseBuildingMoveFailed(placeableEntity, placeableEntity.GridPosition);
                return false;
            }

            Vector2Int targetPosition = _gridManager.WorldToPlacementCell(worldPosition);
            Vector2Int currentPosition = placeableEntity.GridPosition;

            if (targetPosition == currentPosition)
            {
                placeableEntity.CommitPosition(targetPosition);
                RaiseBuildingMoved(placeableEntity, targetPosition);
                return true;
            }

            if (!_gridManager.IsCellEmpty(targetPosition))
            {
                _logManager?.Building($"Target cell {targetPosition} is not empty for `{placeableEntity.name}`.", LogLevel.Warning);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            CostDefinitionSO primaryCost = QueryPrimarySpendCost();
            int moveCost = 0;
            TrySpendCostEvent moveSpendRequest = CostEvents.TrySpendCostEvent.Initializer(primaryCost, moveCost);
            costEventChannel.RaiseEvent(moveSpendRequest);
            if (!moveSpendRequest.Succeeded)
            {
                _logManager?.Building(
                    $"Failed to spend move cost ({moveCost}) for `{placeableEntity.name}` to {targetPosition}.",
                    LogLevel.Warning);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            if (!_gridManager.TryClear(currentPosition, placeableEntity))
            {
                _logManager?.Building($"Failed to clear current cell {currentPosition} for `{placeableEntity.name}`.", LogLevel.Error);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            if (!_gridManager.TryInstall(targetPosition, placeableEntity))
            {
                _gridManager.TryInstall(currentPosition, placeableEntity);
                _logManager?.Building(
                    $"Failed to place `{placeableEntity.name}` at {targetPosition}; restored original cell {currentPosition}.",
                    LogLevel.Error);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            placeableEntity.CommitPosition(targetPosition);
            RaiseBuildingMoved(placeableEntity, targetPosition);
            return true;
        }

        public bool TryInstall(UnitDataSO unitData, Vector3 worldPosition, out PlaceableEntity placedEntity)
        {
            placedEntity = null;
            if (unitData == null || _gridManager == null)
            {
                return false;
            }

            Vector2Int buildPosition = _gridManager.WorldToPlacementCell(worldPosition);
            if (!CanModifyPlacements())
            {
                RaiseBuildFailed(unitData, buildPosition);
                return false;
            }

            if (!_gridManager.IsCellEmpty(buildPosition))
            {
                _logManager?.Building($"Install blocked for `{unitData.Name}` at occupied cell {buildPosition}.", LogLevel.Warning);
                RaiseBuildFailed(unitData, buildPosition);
                return false;
            }

            CostDefinitionSO primaryCost = QueryPrimarySpendCost();
            int totalCost = unitData.Cost;
            TrySpendCostEvent spendRequest = CostEvents.TrySpendCostEvent.Initializer(primaryCost, totalCost);
            costEventChannel.RaiseEvent(spendRequest);
            if (!spendRequest.Succeeded)
            {
                _logManager?.Building(
                    $"Failed to spend gold for `{unitData.Name}`. Unit cost={unitData.Cost}, total={totalCost}.",
                    LogLevel.Warning);
                RaiseBuildFailed(unitData, buildPosition);
                return false;
            }

            Vector3 buildWorldPosition = _gridManager.CellToObjectWorld(buildPosition);
            placedEntity = Instantiate(unitData.Prefab, buildWorldPosition, Quaternion.identity);
            placedEntity.BindSceneServices(_gridManager, _logManager);
            if (!placedEntity.Initialize(buildPosition))
            {
                costEventChannel.RaiseEvent(CostEvents.RefundCostEvent.Initializer(primaryCost, totalCost));
                _logManager?.Building($"Failed to finalize install for `{unitData.Name}` at {buildPosition}; cost refunded.", LogLevel.Error);
                Destroy(placedEntity.gameObject);
                placedEntity = null;
                RaiseBuildFailed(unitData, buildPosition);
                return false;
            }

            OnBuildingInstalled?.Invoke(unitData, placedEntity);
            _selectionController.HandleBuildCompleted();
            buildEventChannel.RaiseEvent(BuildEvents.BuildCompletedEvent.Initializer(unitData, placedEntity));
            return true;
        }

        public bool TrySpawnFree(UnitDataSO unitData, out PlaceableEntity spawnedEntity)
        {
            spawnedEntity = null;
            if (unitData == null || unitData.Prefab == null || _gridManager == null)
            {
                return false;
            }

            spawnedEntity = Instantiate(unitData.Prefab, Vector3.zero, Quaternion.identity);
            spawnedEntity.BindSceneServices(_gridManager, _logManager);
            if (!spawnedEntity.Initialize())
            {
                _logManager?.Building($"Failed to spawn `{unitData.Name}` on a free tile.", LogLevel.Error);
                Destroy(spawnedEntity.gameObject);
                spawnedEntity = null;
                return false;
            }

            if (spawnedEntity is Unit spawnedUnit)
            {
                spawnedUnit.level = 1;
            }

            OnBuildingInstalled?.Invoke(unitData, spawnedEntity);
            _selectionController.HandleBuildCompleted();
            buildEventChannel.RaiseEvent(BuildEvents.BuildCompletedEvent.Initializer(unitData, spawnedEntity));
            return true;
        }

        private void HandleBuildInstallRequestedEvent(BuildRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            TryInstall(evt.UnitData, evt.WorldPosition, out _);
        }

        private void HandleMoveBuildingRequestedEvent(BuildMoveRequestedEvent evt)
        {
            if (evt?.PlaceableEntity == null)
            {
                return;
            }

            evt.Succeeded = TryMove(evt.PlaceableEntity, evt.WorldPosition);
        }

        private void RaiseBuildFailed(UnitDataSO unitData, Vector2Int buildPosition)
        {
            OnBuildFailed?.Invoke(unitData, buildPosition);
            buildEventChannel.RaiseEvent(BuildEvents.BuildFailedEvent.Initializer(unitData, buildPosition));
        }

        private void RaiseBuildingMoved(PlaceableEntity placeableEntity, Vector2Int targetPosition)
        {
            OnBuildingMoved?.Invoke();
            buildEventChannel.RaiseEvent(BuildEvents.BuildMovedEvent.Initializer(placeableEntity, targetPosition));
        }

        private void RaiseBuildingMoveFailed(PlaceableEntity placeableEntity, Vector2Int originalPosition)
        {
            OnBuildingMoveFailed?.Invoke();
            buildEventChannel.RaiseEvent(BuildEvents.BuildMoveFailedEvent.Initializer(placeableEntity, originalPosition));
        }

        private CostDefinitionSO QueryPrimarySpendCost()
        {
            PrimarySpendCostQueryEvent query = CostEvents.PrimarySpendCostQueryEvent.Initializer();
            costEventChannel.RaiseEvent(query);
            return query.Type;
        }

        private void HandleSkipDayRequestedEvent(UiSkipDayRequestedEvent _)
        {
            CancelSelection();
        }

        private void HandleCancelSelectionRequestedEvent(UiCancelSelectionRequestedEvent _)
        {
            CancelSelection();
        }

        private void HandleBuildAtWorldPositionRequestedEvent(UiBuildAtWorldPositionRequestedEvent evt)
        {
            evt.Succeeded = TryRequestBuild(evt.WorldPosition);
        }

        private void HandleHoverCellChangedEvent(UiHoverCellChangedEvent evt)
        {
            _selectionController.SetHoveredCell(evt.CellPosition);
        }

        private void HandleUnitCatalogQueryEvent(UiUnitCatalogQueryEvent evt)
        {
            evt.Units = _catalog.GetAvailableUnitsForCurrentScene();
        }

        private bool CanUseDayActions()
        {
            UiClockStateQueryEvent query = UIEvents.UiClockStateQueryEvent.Initializer();
            uiEventChannel.RaiseEvent(query);
            return query.IsDay;
        }

        private bool CanModifyPlacements()
        {
            return true;
        }
    }
}
