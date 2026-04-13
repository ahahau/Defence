using System;
using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Save
{
    public class GlobalSaveAgentModule : MonoBehaviour, ISaveAgentModule
    {
        public int Order
        {
            get { return 0; }
        }

        public void Initialize(SaveManager saveManager)
        {
            TimeManager timeManager = GameManager.Instance?.GetManager<TimeManager>();
            if (timeManager != null)
            {
                TimeSaveAgent timeSaveAgent = EnsureAgentOnObject<TimeSaveAgent>(timeManager.gameObject);
                saveManager.RegisterSaveable(timeSaveAgent);
            }

            CostManager costManager = GameManager.Instance?.GetManager<CostManager>();
            if (costManager != null)
            {
                CostSaveAgent costSaveAgent = EnsureAgentOnObject<CostSaveAgent>(costManager.gameObject);
                saveManager.RegisterSaveable(costSaveAgent);
            }

            GridManager gridManager = GameManager.Instance?.GetManager<GridManager>();
            if (gridManager != null)
            {
                GridStateSaveAgent gridStateSaveAgent = EnsureAgentOnObject<GridStateSaveAgent>(gridManager.gameObject);
                saveManager.RegisterSaveable(gridStateSaveAgent);
            }
        }

        private T EnsureAgentOnObject<T>(GameObject target) where T : Component
        {
            if (target == null)
            {
                return null;
            }

            T component = target.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            return target.AddComponent<T>();
        }
    }

    [Serializable]
    public struct GridChunkSaveEntry
    {
        public int x;
        public int y;
    }

    [Serializable]
    public struct GridStateSaveData
    {
        public List<GridChunkSaveEntry> activeChunks;
    }

    public class GridStateSaveAgent : MonoBehaviour, ISaveable
    {
        [SerializeField] private string saveKey = "grid.state";
        private GridManager _gridManager;

        public string SaveKey
        {
            get { return saveKey; }
        }

        public int RestoreOrder
        {
            get { return -100; }
        }

        public string GetSaveData()
        {
            _gridManager = GetComponent<GridManager>();
            List<GridChunkSaveEntry> activeChunks = new List<GridChunkSaveEntry>();
            if (_gridManager == null)
            {
                return JsonUtility.ToJson(new GridStateSaveData { activeChunks = activeChunks });
            }

            List<Vector2Int> chunkCoordinates = _gridManager.GetActiveChunkCoordinates();
            for (int i = 0; i < chunkCoordinates.Count; i++)
            {
                Vector2Int chunkCoordinate = chunkCoordinates[i];
                activeChunks.Add(new GridChunkSaveEntry
                {
                    x = chunkCoordinate.x,
                    y = chunkCoordinate.y
                });
            }

            return JsonUtility.ToJson(new GridStateSaveData { activeChunks = activeChunks });
        }

        public void RestoreData(string savedData)
        {
            _gridManager = GetComponent<GridManager>();
            if (_gridManager == null || string.IsNullOrWhiteSpace(savedData))
            {
                return;
            }

            GridStateSaveData data = JsonUtility.FromJson<GridStateSaveData>(savedData);
            if (data.activeChunks == null || data.activeChunks.Count == 0)
            {
                return;
            }

            List<Vector2Int> chunkCoordinates = new List<Vector2Int>(data.activeChunks.Count);
            for (int i = 0; i < data.activeChunks.Count; i++)
            {
                GridChunkSaveEntry activeChunk = data.activeChunks[i];
                chunkCoordinates.Add(new Vector2Int(activeChunk.x, activeChunk.y));
            }

            _gridManager.RestoreActiveChunks(chunkCoordinates);
        }
    }
}
