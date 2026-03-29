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
        public InputManager InputManager { get; private set; }
        public UIManager UiManager { get; private set; }
        public BuildManager BuildManager { get; private set; }
        public WaveManager WaveManager { get; private set; }
        public TimeManager TimeManager { get; private set; }
        public EnemySpawnerManager EnemySpawnerManager { get; private set; }
        public LogManager LogManager { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            GridManager = GetComponentInChildren<GridManager>();
            CostManager = GetComponentInChildren<CostManager>();
            InputManager = GetComponentInChildren<InputManager>();
            UiManager = GetComponentInChildren<UIManager>();
            BuildManager = GetComponentInChildren<BuildManager>();
            WaveManager = GetComponentInChildren<WaveManager>();
            TimeManager = GetComponentInChildren<TimeManager>();
            EnemySpawnerManager = GetComponentInChildren<EnemySpawnerManager>();
            LogManager = GetComponentInChildren<LogManager>();

            LogManager.Initialize();
            GridManager.Initialize();
            CostManager.Initialize();
            WaveManager.Initialize();
            EnemySpawnerManager.Initialize();
            BuildManager.Initialize();
            TimeManager.Initialize();
            UiManager.Initialize();
            InputManager.Initialize();
        }
    }
}
