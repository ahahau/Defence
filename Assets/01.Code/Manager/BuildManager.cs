using System;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    public class BuildManager : MonoBehaviour
    {
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;

        public event Action<BuildingDataSO, PlaceableEntity> OnBuildingInstalled;
        public event Action<BuildingDataSO, Vector2Int> OnBuildFailed;

        public event Action OnBuildingMoved;
        public event Action OnBuildingMoveFailed;

        public void Initialize()
        {
            buildEventChannel.AddListener<BuildInstallRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.AddListener<MoveBuildingRequestedEvent>(HandleMoveBuildingRequestedEvent);
            GameManager.Instance.LogManager?.System("BuildManager initialized.");
        }

        private void OnDestroy()
        {
            buildEventChannel.RemoveListener<BuildInstallRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.RemoveListener<MoveBuildingRequestedEvent>(HandleMoveBuildingRequestedEvent);
        }

        public bool TryPlace(PlaceableEntity placeableEntity, Vector3 worldPosition)
        {
            if (placeableEntity == null)
            {
                return false;
            }

            Vector2Int cellPosition = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            Vector2Int buildPosition = GameManager.Instance.GridManager.Tilemap.CellToWorld(cellPosition);

            if (!GameManager.Instance.GridManager.Tilemap.TileEmpty(buildPosition))
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

            Vector2Int cellPosition = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            Vector2Int targetPosition = GameManager.Instance.GridManager.Tilemap.CellToWorld(cellPosition);
            Vector2Int currentPosition = placeableEntity.GridPosition;

            if (targetPosition == currentPosition)
            {
                placeableEntity.CommitPosition(targetPosition);
                RaiseBuildingMoved(placeableEntity, targetPosition);
                return true;
            }

            if (!GameManager.Instance.GridManager.Tilemap.TileEmpty(targetPosition))
            {
                GameManager.Instance.LogManager?.Building($"Target cell {targetPosition} is not empty for `{placeableEntity.name}`.", LogLevel.Warning);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            if (!GameManager.Instance.GridManager.Tilemap.ClearTileObject(currentPosition, placeableEntity))
            {
                GameManager.Instance.LogManager?.Building($"Failed to clear current cell {currentPosition} for `{placeableEntity.name}`.", LogLevel.Error);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            if (!GameManager.Instance.GridManager.Tilemap.TileObjectInstall(targetPosition, placeableEntity))
            {
                GameManager.Instance.GridManager.Tilemap.TileObjectInstall(currentPosition, placeableEntity);
                GameManager.Instance.LogManager?.Building($"Failed to place `{placeableEntity.name}` at {targetPosition}; restored original cell {currentPosition}.", LogLevel.Error);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            placeableEntity.CommitPosition(targetPosition);
            GameManager.Instance.LogManager?.Building($"Moved `{placeableEntity.name}` to {targetPosition}.");
            RaiseBuildingMoved(placeableEntity, targetPosition);
            return true;
        }

        public bool TryInstall(BuildingDataSO buildingData, Vector3 worldPosition, out PlaceableEntity placedEntity)
        {
            placedEntity = null;

            if (buildingData == null)
            {
                return false;
            }

            Vector2Int cellPosition = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            Vector2Int buildPosition = GameManager.Instance.GridManager.Tilemap.CellToWorld(cellPosition);

            if (!GameManager.Instance.GridManager.Tilemap.TileEmpty(buildPosition))
            {
                GameManager.Instance.LogManager?.Building($"Install blocked for `{buildingData.Name}` at occupied cell {buildPosition}.", LogLevel.Warning);
                RaiseBuildFailed(buildingData, buildPosition);
                return false;
            }

            TrySpendCostEvent spendRequest = CostEvents.TrySpendCost.Initializer(CostType.Gold, buildingData.Cost);
            costEventChannel.RaiseEvent(spendRequest);
            if (!spendRequest.Succeeded)
            {
                GameManager.Instance.LogManager?.Building($"Failed to spend gold for `{buildingData.Name}`.", LogLevel.Warning);
                RaiseBuildFailed(buildingData, buildPosition);
                return false;
            }

            placedEntity = Instantiate(buildingData.Prefab, new Vector3(buildPosition.x, buildPosition.y, 0f), Quaternion.identity);
            placedEntity.Initialize(buildPosition);
            GameManager.Instance.LogManager?.Building($"Installed `{buildingData.Name}` at {buildPosition}.");
            OnBuildingInstalled?.Invoke(buildingData, placedEntity);
            buildEventChannel.RaiseEvent(BuildEvents.BuildInstalled.Initializer(buildingData, placedEntity));
            return true;
        }

        private void HandleBuildInstallRequestedEvent(BuildInstallRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            TryInstall(evt.BuildingData, evt.WorldPosition, out _);
        }

        private void HandleMoveBuildingRequestedEvent(MoveBuildingRequestedEvent evt)
        {
            if (evt?.PlaceableEntity == null)
            {
                return;
            }

            TryMove(evt.PlaceableEntity, evt.WorldPosition);
        }

        private void RaiseBuildFailed(BuildingDataSO buildingData, Vector2Int buildPosition)
        {
            OnBuildFailed?.Invoke(buildingData, buildPosition);
            buildEventChannel.RaiseEvent(BuildEvents.BuildFailed.Initializer(buildingData, buildPosition));
        }

        private void RaiseBuildingMoved(PlaceableEntity placeableEntity, Vector2Int targetPosition)
        {
            OnBuildingMoved?.Invoke();
            buildEventChannel.RaiseEvent(BuildEvents.BuildingMoved.Initializer(placeableEntity, targetPosition));
        }

        private void RaiseBuildingMoveFailed(PlaceableEntity placeableEntity, Vector2Int originalPosition)
        {
            OnBuildingMoveFailed?.Invoke();
            buildEventChannel.RaiseEvent(BuildEvents.BuildingMoveFailed.Initializer(placeableEntity, originalPosition));
        }
    }
}
