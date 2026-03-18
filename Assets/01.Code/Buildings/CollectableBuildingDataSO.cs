using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "CollectableBuildingData", menuName = "SO/CollectableBuilding", order = 0)]
    public class CollectableBuildingDataSO : ScriptableObject
    {
        [field: SerializeField] public CostType Type { get; private set; }
        [field: SerializeField] public int GainCost { get; private set; }
        //[field: SerializeField] public  { get; private set; }
    }
}