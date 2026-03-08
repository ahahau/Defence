using _01.Code.System.Grids;
using UnityEngine;

namespace _01.Code.Manager
{
    
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;
        public GridManager GridManager { get; private set; }
        
        public CostManager CostManager { get; private set; }
        public UIManager UiManager { get; private set; }
        
        public WaveManager WaveManager { get; private set; }
        
        public EnemySpawnerManager EnemySpawnerManager { get; private set; }
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
            CostManager = GetComponentInChildren<CostManager>();
            UiManager = GetComponentInChildren<UIManager>();
            WaveManager = GetComponentInChildren<WaveManager>();
            EnemySpawnerManager = GetComponentInChildren<EnemySpawnerManager>();
            
            
            GridManager.Initialize();
            CostManager.Initialize();
            //UiManager.Initialize();
            WaveManager.Initialize();
            EnemySpawnerManager.Initialize();
        }
        
    }
}