using System;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Unit;
using UnityEngine;

namespace _01.Code.Manager
{
    public class BuildManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;

        public event Action<UnitDataSO, PlaceableEntity> OnBuildingInstalled;
        public event Action<UnitDataSO, Vector2Int> OnBuildFailed;

        public event Action OnBuildingMoved;
        public event Action OnBuildingMoveFailed;

        public void Initialize()
        {
            buildEventChannel.AddListener<UnitGenerationRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.AddListener<MoveUnitRequestedEvent>(HandleMoveBuildingRequestedEvent);
        }

        private void OnDestroy()
        {
            buildEventChannel.RemoveListener<UnitGenerationRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.RemoveListener<MoveUnitRequestedEvent>(HandleMoveBuildingRequestedEvent);
        }

        public bool TryPlace(PlaceableEntity placeableEntity, Vector3 worldPosition)
        {
            if (placeableEntity == null)
            {
                return false;
            }

            Vector2Int buildPosition = GameManager.Instance.GridManager.WorldToCell(worldPosition);

            if (!GameManager.Instance.GridManager.IsCellEmpty(buildPosition))
            {
                GameManager.Instance.LogManager?.Building($"Move blocked for `{placeableEntity.name}` to {buildPosition}.", LogLevel.Warning);
                RaiseBuildingMoveFailed(placeableEntity, placeableEntity.GridPosition);
                return false;
            }

            RaiseBuildingMoved(placeableEntity, buildPosition);
            return true;
        }

        public bool TryMove(PlaceableEntity placeableEntity, Vector3 worldPosition)
        {
            if (placeableEntity == null)
            {
                return false;
            }

            Vector2Int targetPosition = GameManager.Instance.GridManager.WorldToCell(worldPosition);
            Vector2Int currentPosition = placeableEntity.GridPosition;

            if (targetPosition == currentPosition)
            {
                placeableEntity.CommitPosition(targetPosition);
                RaiseBuildingMoved(placeableEntity, targetPosition);
                return true;
            }

            if (!GameManager.Instance.GridManager.IsCellEmpty(targetPosition))
            {
                GameManager.Instance.LogManager?.Building($"Target cell {targetPosition} is not empty for `{placeableEntity.name}`.", LogLevel.Warning);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            if (!GameManager.Instance.GridManager.TryClear(currentPosition, placeableEntity))
            {
                GameManager.Instance.LogManager?.Building($"Failed to clear current cell {currentPosition} for `{placeableEntity.name}`.", LogLevel.Error);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            if (!GameManager.Instance.GridManager.TryInstall(targetPosition, placeableEntity))
            {
                GameManager.Instance.GridManager.TryInstall(currentPosition, placeableEntity);
                GameManager.Instance.LogManager?.Building($"Failed to place `{placeableEntity.name}` at {targetPosition}; restored original cell {currentPosition}.", LogLevel.Error);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            placeableEntity.CommitPosition(targetPosition);
            RaiseBuildingMoved(placeableEntity, targetPosition);
            return true;
        }

        public bool TryInstall(UnitDataSO unitData, Vector3 worldPosition, out PlaceableEntity UnitManager)
        {
            UnitManager = null;

            if (unitData == null)
            {
                return false;
            }

            Vector2Int buildPosition = GameManager.Instance.GridManager.WorldToCell(worldPosition);

            if (!GameManager.Instance.GridManager.IsCellEmpty(buildPosition))
            {
                GameManager.Instance.LogManager?.Building($"Install blocked for `{unitData.Name}` at occupied cell {buildPosition}.", LogLevel.Warning);
                RaiseBuildFailed(unitData, buildPosition);
                return false;
            }

            TrySpendCostEvent spendRequest = CostEvents.TrySpendCostEvent.Initializer(CostType.Gold, unitData.Cost);
            costEventChannel.RaiseEvent(spendRequest);
            if (!spendRequest.Succeeded)
            {
                GameManager.Instance.LogManager?.Building($"Failed to spend gold for `{unitData.Name}`.", LogLevel.Warning);
                RaiseBuildFailed(unitData, buildPosition);
                return false;
            }

            Vector3 buildWorldPosition = GameManager.Instance.GridManager.CellToWorld(buildPosition);
            UnitManager = Instantiate(unitData.Prefab, buildWorldPosition, Quaternion.identity);
            UnitManager.Initialize(buildPosition);
            OnBuildingInstalled?.Invoke(unitData, UnitManager);
            buildEventChannel.RaiseEvent(UnitEvents.UnitGenerationEvent.Initializer(unitData, UnitManager));
            return true;
        }

        private void HandleBuildInstallRequestedEvent(UnitGenerationRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            TryInstall(evt.UnitData, evt.WorldPosition, out _);
        }

        private void HandleMoveBuildingRequestedEvent(MoveUnitRequestedEvent evt)
        {
            if (evt?.PlaceableEntity == null)
            {
                return;
            }

            TryMove(evt.PlaceableEntity, evt.WorldPosition);
        }

        private void RaiseBuildFailed(UnitDataSO unitData, Vector2Int buildPosition)
        {
            OnBuildFailed?.Invoke(unitData, buildPosition);
            buildEventChannel.RaiseEvent(UnitEvents.UnitGenerationFailedEvent.Initializer(unitData, buildPosition));
        }

        private void RaiseBuildingMoved(PlaceableEntity placeableEntity, Vector2Int targetPosition)
        {
            OnBuildingMoved?.Invoke();
            buildEventChannel.RaiseEvent(UnitEvents.UnitMovedEvent.Initializer(placeableEntity, targetPosition));
        }

        private void RaiseBuildingMoveFailed(PlaceableEntity placeableEntity, Vector2Int originalPosition)
        {
            OnBuildingMoveFailed?.Invoke();
            buildEventChannel.RaiseEvent(UnitEvents.UnitMoveFailedEvent.Initializer(placeableEntity, originalPosition));
        }
    }
}
