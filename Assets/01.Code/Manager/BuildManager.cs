using System;
using _01.Code.Buildings;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Manager
{
    public class BuildManager : MonoBehaviour, IManageable
    {
        public event Action<BuildingDataSO, PlaceableEntity> OnBuildingInstalled;
        public event Action<BuildingDataSO, Vector2Int> OnBuildFailed;

        public event Action OnBuildingMoved;
        public event Action OnBuildingMoveFailed;

        public void Initialize()
        {
        }

        public bool TryPlace(PlaceableEntity placeableEntity, Vector3 worldPosition)
        {
            if(placeableEntity == null)
            {
                return false;
            }
            
            Vector2Int cellPosition = GameManager.Instance.GridManager.Tilemap.WorldToCell(worldPosition);
            Vector2Int buildPosition = GameManager.Instance.GridManager.Tilemap.CellToWorld(cellPosition);
            
            if(!GameManager.Instance.GridManager.Tilemap.TileEmpty(buildPosition))
            {
                OnBuildingMoveFailed?.Invoke();
                return false;
            }
            
            OnBuildingMoved?.Invoke();
            
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
                OnBuildingMoved?.Invoke();
                return true;
            }

            if (!GameManager.Instance.GridManager.Tilemap.TileEmpty(targetPosition))
            {
                OnBuildingMoveFailed?.Invoke();
                return false;
            }

            if (!GameManager.Instance.GridManager.Tilemap.ClearTileObject(currentPosition, placeableEntity))
            {
                OnBuildingMoveFailed?.Invoke();
                return false;
            }

            if (!GameManager.Instance.GridManager.Tilemap.TileObjectInstall(targetPosition, placeableEntity))
            {
                GameManager.Instance.GridManager.Tilemap.TileObjectInstall(currentPosition, placeableEntity);
                OnBuildingMoveFailed?.Invoke();
                return false;
            }

            placeableEntity.CommitPosition(targetPosition);
            OnBuildingMoved?.Invoke();
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
                OnBuildFailed?.Invoke(buildingData, buildPosition);
                return false;
            }
            
            placedEntity = Instantiate(buildingData.Prefab, new Vector3(buildPosition.x, buildPosition.y, 0f), Quaternion.identity);
            placedEntity.Initialize(buildPosition);
            OnBuildingInstalled?.Invoke(buildingData, placedEntity);
            return true;
        }
    }
}
