using System;
using _01.Code.Manager;
using _01.Code.Save;
using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.Entities
{
    [Serializable]
    public struct PlaceableEntitySaveData
    {
        public string runtimeSaveId;
        public string placementSaveKey;
        public string customData;
    }

    public abstract class PlaceableEntity : Entity, ISaveable
    {
        [field: SerializeField] public CustomTile Tile { get; private set; }
        [field: SerializeField] public Vector2Int GridPosition { get; protected set; }
        [field: SerializeField] public string PlacementSaveKey { get; private set; }
        [field: SerializeField] public string RuntimeSaveId { get; private set; }

        public string SaveKey => RuntimeSaveId;

        public virtual bool Initialize(Vector2Int position)
        {
            return SetTile(position);
        }

        public virtual bool Initialize()
        {
            Vector2Int randomPos = GameManager.Instance.GridManager.GetRandomGridPosition();
            if (randomPos == Vector2Int.zero)
            {
                GameManager.Instance.LogManager?.Building($"{gameObject.name}: no empty tile found.", LogLevel.Error);
                return false;
            }

            return SetTile(randomPos);
        }

        public virtual bool SetTile(Vector2Int tilePos)
        {
            if (!GameManager.Instance.GridManager.TryInstall(tilePos, this))
            {
                GameManager.Instance.LogManager?.Building($"{gameObject.name}: tile install failed at {tilePos}.", LogLevel.Error);
                return false;
            }

            CommitPosition(tilePos);
            return true;
        }

        public virtual void PreviewPosition(Vector2Int tilePos)
        {
            transform.position = GameManager.Instance.GridManager.CellToWorld(tilePos);
        }

        public virtual void CommitPosition(Vector2Int tilePos)
        {
            GridPosition = tilePos;
            Tile = GameManager.Instance.GridManager.GetTile(tilePos);
            transform.position = GameManager.Instance.GridManager.CellToWorld(GridPosition);
        }

        public void BindPlacementSaveKey(string saveKey)
        {
            PlacementSaveKey = saveKey;
        }

        public void BindRuntimeSaveId(string runtimeSaveId)
        {
            RuntimeSaveId = runtimeSaveId;
        }

        public void EnsureRuntimeSaveId()
        {
            if (!string.IsNullOrWhiteSpace(RuntimeSaveId))
            {
                return;
            }

            RuntimeSaveId = Guid.NewGuid().ToString("N");
        }

        public virtual string GetSaveData()
        {
            EnsureRuntimeSaveId();
            PlaceableEntitySaveData saveData = new PlaceableEntitySaveData
            {
                runtimeSaveId = RuntimeSaveId,
                placementSaveKey = PlacementSaveKey,
                customData = CaptureCustomSaveData()
            };
            return JsonUtility.ToJson(saveData);
        }

        public virtual void RestoreData(string savedData)
        {
            if (string.IsNullOrWhiteSpace(savedData))
            {
                return;
            }

            PlaceableEntitySaveData saveData = JsonUtility.FromJson<PlaceableEntitySaveData>(savedData);
            if (!string.IsNullOrWhiteSpace(saveData.runtimeSaveId))
            {
                RuntimeSaveId = saveData.runtimeSaveId;
            }

            if (!string.IsNullOrWhiteSpace(saveData.placementSaveKey))
            {
                PlacementSaveKey = saveData.placementSaveKey;
            }

            RestoreCustomSaveData(saveData.customData);
        }

        protected virtual string CaptureCustomSaveData()
        {
            return string.Empty;
        }

        protected virtual void RestoreCustomSaveData(string savedData)
        {
        }
    }
}
