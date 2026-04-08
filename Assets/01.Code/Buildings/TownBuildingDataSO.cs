using _01.Code.Tiles;

namespace _01.Code.Buildings
{
    [UnityEngine.CreateAssetMenu(fileName = "TownBuildingData", menuName = "SO/Town/Building Data", order = 0)]
    public class TownBuildingDataSO : TownTileObjectDataSO
    {
        public TownBuilding BuildingPrefab
        {
            get { return Prefab as TownBuilding; }
        }
    }
}
