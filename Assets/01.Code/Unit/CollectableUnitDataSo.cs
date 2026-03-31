using _01.Code.Buildings;
using _01.Code.Cost;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Unit
{
    [CreateAssetMenu(fileName = "CollectableBuildingData", menuName = "SO/Building/CollectableBuilding", order = 0)]
    public class CollectableUnitDataSo : UnitDataSO
    {
        [field: SerializeField] public CostDefinitionSO Type { get; private set; }
        [field: SerializeField] public int GainCost { get; private set; }
        //[field: SerializeField] public  { get; private set; }
    }
}
