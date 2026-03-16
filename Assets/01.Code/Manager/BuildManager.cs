using System;
using _01.Code.Buildings;
using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.Manager
{
    public class BuildManager : MonoBehaviour, IManageable
    {
        [SerializeField] private GameEventChannelSO buildEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;

        public event Action<BuildingDataSO, PlaceableEntity> OnBuildingInstalled;
        public event Action<BuildingDataSO, Vector2Int> OnBuildFailed;

        public event Action OnBuildingMoved;
        public event Action OnBuildingMoveFailed;

        /// <summary>
        /// 이 함수는 빌드 채널 요청을 실제 설치, 이동 로직에 연결합니다
        /// </summary>
        public void Initialize()
        {
            buildEventChannel.AddListener<BuildInstallRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.AddListener<MoveBuildingRequestedEvent>(HandleMoveBuildingRequestedEvent);
        }

        private void OnDestroy()
        {
            buildEventChannel.RemoveListener<BuildInstallRequestedEvent>(HandleBuildInstallRequestedEvent);
            buildEventChannel.RemoveListener<MoveBuildingRequestedEvent>(HandleMoveBuildingRequestedEvent);
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

            // 같은 칸으로 놓았으면 타일 데이터 변경 없이 위치만 확정합니다
            if (targetPosition == currentPosition)
            {
                placeableEntity.CommitPosition(targetPosition);
                RaiseBuildingMoved(placeableEntity, targetPosition);
                return true;
            }

            // 이동은 비어있는 타일만 허용하고, 기존 타일 해제와 새 타일 설치가 모두 성공해야 합니다
            if (!GameManager.Instance.GridManager.Tilemap.TileEmpty(targetPosition))
            {
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            if (!GameManager.Instance.GridManager.Tilemap.ClearTileObject(currentPosition, placeableEntity))
            {
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            if (!GameManager.Instance.GridManager.Tilemap.TileObjectInstall(targetPosition, placeableEntity))
            {
                // 새 타일 설치에 실패하면 기존 점유 상태를 다시 복구합니다
                GameManager.Instance.GridManager.Tilemap.TileObjectInstall(currentPosition, placeableEntity);
                RaiseBuildingMoveFailed(placeableEntity, currentPosition);
                return false;
            }

            placeableEntity.CommitPosition(targetPosition);
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
                RaiseBuildFailed(buildingData, buildPosition);
                return false;
            }

            // 설치 전에 비용을 먼저 차감 요청하고, 실패하면 설치를 진행하지 않습니다
            TrySpendCostEvent spendRequest = CostEvents.TrySpendCost.Initializer(CostType.Gold, buildingData.Cost);
            costEventChannel.RaiseEvent(spendRequest);
            if (!spendRequest.Succeeded)
            {
                RaiseBuildFailed(buildingData, buildPosition);
                return false;
            }

            placedEntity = Instantiate(buildingData.Prefab, new Vector3(buildPosition.x, buildPosition.y, 0f), Quaternion.identity);
            placedEntity.Initialize(buildPosition);
            OnBuildingInstalled?.Invoke(buildingData, placedEntity);
            buildEventChannel.RaiseEvent(BuildEvents.BuildInstalled.Initializer(buildingData, placedEntity));
            return true;
        }

        /// <summary>
        /// 이 함수는 UI에서 들어온 설치 요청을 실제 설치 함수로 넘깁니다
        /// </summary>
        private void HandleBuildInstallRequestedEvent(BuildInstallRequestedEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            TryInstall(evt.BuildingData, evt.WorldPosition, out _);
        }

        /// <summary>
        /// 이 함수는 입력에서 들어온 이동 요청을 실제 이동 함수로 넘깁니다
        /// </summary>
        private void HandleMoveBuildingRequestedEvent(MoveBuildingRequestedEvent evt)
        {
            if (evt?.PlaceableEntity == null)
            {
                return;
            }

            TryMove(evt.PlaceableEntity, evt.WorldPosition);
        }

        /// <summary>
        /// 이 함수는 설치 실패를 내부 이벤트와 채널 이벤트로 같이 알려줍니다
        /// </summary>
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
