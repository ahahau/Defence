using _01.Code.Enemies;
using _01.Code.PlaceableObjects;
using _01.Code.Players;
using UnityEngine;

namespace _01.Code.Manager
{
    public class SpawnerManager : MonoBehaviour, IManageable
    {
        [field:SerializeField]public EnemySpawnerController EnemySpawnerController {get; private set;}
        [field:SerializeField]public ObjectSpawnController ObjectSpawnController {get; private set;}
        [SerializeField] private CommandCenter commandCenter;
        public void Initialize()
        {
            //EnemySpawnerController.GetComponentInChildren<EnemySpawnerController>();
            //ObjectSpawnController.GetComponentInChildren<ObjectSpawnController>();
            
            commandCenter.Initialize();
            
            ObjectSpawnController.Initialize();
            EnemySpawnerController.Initialize();    
        }
    }
}