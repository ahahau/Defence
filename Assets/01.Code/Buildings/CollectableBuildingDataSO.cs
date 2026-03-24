using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "CollectableBuildingData", menuName = "SO/Building/CollectableBuilding", order = 0)]
    public class CollectableBuildingDataSO : BuildingDataSO
    {
        [field: SerializeField] public CostType Type { get; private set; }
        [field: SerializeField] public int GainCost { get; private set; }
        //[field: SerializeField] public  { get; private set; }
    }
}