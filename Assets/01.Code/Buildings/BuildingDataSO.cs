using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "BuildingData", menuName = "SO/Building/Data", order = 0)]
    public class BuildingDataSO : ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public string Explanation { get; private set; }
        [field: SerializeField] public int Cost { get; private set; }
        [field: SerializeField] public PlaceableEntity Prefab { get; private set; }
    }
}
