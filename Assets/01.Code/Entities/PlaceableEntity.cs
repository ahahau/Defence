using System;
using _01.Code.Enemies;
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
        [SerializeField] [Min(0)] private int pathTraversalCostOverride;

        protected GridManager GridManager { get; private set; }
        protected LogManager LogManager { get; private set; }

        public event Action<Enemy> EnemyPassedThrough;

        public string SaveKey
        {
            get { return RuntimeSaveId; }
        }
        public int RestoreOrder { get; }

        public int PathTraversalCost
        {
            get
            {
                return pathTraversalCostOverride > 0
                    ? pathTraversalCostOverride
                    : GetDefaultPathTraversalCost();
            }
        }

        public void BindSceneServices(GridManager gridManager, LogManager logManager)
        {
            GridManager = gridManager;
            LogManager = logManager;
        }

        public virtual bool Initialize(Vector2Int position)
        {
            return SetTile(position);
        }

        public virtual bool Initialize()
        {
            EnsureSceneServices();
            Vector2Int randomPos = GridManager.GetRandomGridPosition();
            if (randomPos == Vector2Int.zero)
            {
                LogManager?.Building($"{gameObject.name}: no empty tile found.", LogLevel.Error);
                return false;
            }

            return SetTile(randomPos);
        }

        public virtual bool SetTile(Vector2Int tilePos)
        {
            EnsureSceneServices();
            if (!GridManager.TryInstall(tilePos, this))
            {
                LogManager?.Building($"{gameObject.name}: tile install failed at {tilePos}.", LogLevel.Error);
                return false;
            }

            CommitPosition(tilePos);
            return true;
        }

        public virtual void PreviewPosition(Vector2Int tilePos)
        {
            EnsureSceneServices();
            transform.position = GridManager.CellToObjectWorld(tilePos);
        }

        public virtual void CommitPosition(Vector2Int tilePos)
        {
            EnsureSceneServices();
            GridPosition = tilePos;
            Tile = GridManager.GetTile(tilePos);
            transform.position = GridManager.CellToObjectWorld(GridPosition);
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

        public void NotifyEnemyPassedThrough(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            EnemyPassedThrough?.Invoke(enemy);
            OnEnemyPassedThrough(enemy);
        }

        protected virtual int GetDefaultPathTraversalCost()
        {
            return 10;
        }

        protected virtual void OnEnemyPassedThrough(Enemy enemy)
        {
        }

        private void EnsureSceneServices()
        {
            GridManager ??= GameManager.Instance?.GetManager<GridManager>();
            LogManager ??= GameManager.Instance?.GetManager<LogManager>();
        }
    }
}
