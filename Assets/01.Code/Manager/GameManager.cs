using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.Manager
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        public InGameManager InGameManager { get; private set; }
        public GridManager GridManager { get; private set; }
        public SpawnerManager SpawnerManager { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }

            InGameManager = GetComponentInChildren<InGameManager>();
            GridManager = GetComponentInChildren<GridManager>();
            SpawnerManager = GetComponentInChildren<SpawnerManager>();
            
            InGameManager.Initialize();
            GridManager.Initialize();
            SpawnerManager.Initialize();
        }
    }
}