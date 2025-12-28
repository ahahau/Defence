using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.Manager
{
    
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
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
            }
            
            GridManager = GetComponentInChildren<GridManager>();
            SpawnerManager = GetComponentInChildren<SpawnerManager>();
            GridManager.Initialize();
            SpawnerManager.Initialize();
            
        }
        
    }
}