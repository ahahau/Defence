using System;
using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.Serialization;

namespace _01.Code.Manager
{
    public class BuildManager : MonoBehaviour, IManageable
    {
        [FormerlySerializedAs("unitEventChannel")]
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;

        private GridManager _gridManager;
        private LogManager _logManager;

        public event Action<UnitDataSO, PlaceableEntity> OnBuildingInstalled;
        public event Action<UnitDataSO, Vector2Int> OnBuildFailed;
        public event Action OnBuildingMoved;
        public event Action OnBuildingMoveFailed;

        public void Initialize(IManagerContainer managerContainer)
        {
            _gridManager = managerContainer.GetManager<GridManager>();
            _logManager = managerContainer.GetManager<LogManager>();
            ResolveChannels();
            if (buildEventChannel == null)
            {
                Debug.LogError("Build event channel is missing on BuildManager.");
                return;
            }

            buildEventChannel.AddListener<BuildRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.AddListener<BuildMoveRequestedEvent>(HandleMoveBuildingRequestedEvent);
        }

        private void OnDestroy()
        {
            if (buildEventChannel == null)
            {
                return;
            }

            buildEventChannel.RemoveListener<BuildRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.RemoveListener<BuildMoveRequestedEvent>(HandleMoveBuildingRequestedEvent);
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

            Vector2Int targetPosition = _gridManager.WorldToCell(worldPosition);
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
            costEventChannel?.RaiseEvent(moveSpendRequest);
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

            Vector2Int buildPosition = _gridManager.WorldToCell(worldPosition);
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
            costEventChannel?.RaiseEvent(spendRequest);
            if (!spendRequest.Succeeded)
            {
                _logManager?.Building(
                    $"Failed to spend gold for `{unitData.Name}`. Unit cost={unitData.Cost}, total={totalCost}.",
                    LogLevel.Warning);
                RaiseBuildFailed(unitData, buildPosition);
                return false;
            }

            Vector3 buildWorldPosition = _gridManager.CellToWorld(buildPosition);
            placedEntity = Instantiate(unitData.Prefab, buildWorldPosition, Quaternion.identity);
            placedEntity.BindSceneServices(_gridManager, _logManager);
            if (!placedEntity.Initialize(buildPosition))
            {
                costEventChannel?.RaiseEvent(CostEvents.RefundCostEvent.Initializer(primaryCost, totalCost));
                _logManager?.Building($"Failed to finalize install for `{unitData.Name}` at {buildPosition}; cost refunded.", LogLevel.Error);
                Destroy(placedEntity.gameObject);
                placedEntity = null;
                RaiseBuildFailed(unitData, buildPosition);
                return false;
            }

            OnBuildingInstalled?.Invoke(unitData, placedEntity);
            buildEventChannel?.RaiseEvent(BuildEvents.BuildCompletedEvent.Initializer(unitData, placedEntity));
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
            buildEventChannel?.RaiseEvent(BuildEvents.BuildCompletedEvent.Initializer(unitData, spawnedEntity));
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
            buildEventChannel?.RaiseEvent(BuildEvents.BuildFailedEvent.Initializer(unitData, buildPosition));
        }

        private void RaiseBuildingMoved(PlaceableEntity placeableEntity, Vector2Int targetPosition)
        {
            OnBuildingMoved?.Invoke();
            buildEventChannel?.RaiseEvent(BuildEvents.BuildMovedEvent.Initializer(placeableEntity, targetPosition));
        }

        private void RaiseBuildingMoveFailed(PlaceableEntity placeableEntity, Vector2Int originalPosition)
        {
            OnBuildingMoveFailed?.Invoke();
            buildEventChannel?.RaiseEvent(BuildEvents.BuildMoveFailedEvent.Initializer(placeableEntity, originalPosition));
        }

        private CostDefinitionSO QueryPrimarySpendCost()
        {
            PrimarySpendCostQueryEvent query = CostEvents.PrimarySpendCostQueryEvent.Initializer();
            costEventChannel?.RaiseEvent(query);
            return query.Type;
        }

        private void ResolveChannels()
        {
            UIManager uiManager = FindFirstObjectByType<UIManager>();
            if (buildEventChannel == null)
            {
                buildEventChannel = uiManager != null ? uiManager.BuildEventChannel : null;
            }

            if (costEventChannel == null)
            {
                costEventChannel = uiManager != null ? uiManager.CostEventChannel : null;
            }
        }

        private bool CanModifyPlacements()
        {
            return true;
        }
    }
}
