using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Tiles
{
    [CreateAssetMenu(fileName = "TownObstacleData", menuName = "SO/Town/Obstacle Data", order = 0)]
    public class TownObstacleDataSO : TownTileObjectDataSO
    {
        [field: SerializeField] public int RemoveCost { get; private set; }
        [field: SerializeField] public List<TownTileObjectDataSO.Entry> ReturnCosts { get; private set; }

        public TownObstacle ObstaclePrefab
        {
            get { return Prefab as TownObstacle; }
        }
    }
}
