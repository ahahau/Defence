using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "TownBuildingData", menuName = "SO/Town/Building Data", order = 0)]
    public class TownBuildingDataSO : TownTileObjectDataSO
    {
        public TownBuilding BuildingPrefab
        {
            get { return Prefab as TownBuilding; }
        }
    }
}
