using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Unit
{
    [CreateAssetMenu(fileName = "BuildingData", menuName = "SO/Building/Data", order = 0)]
    public class UnitDataSO : ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public string Explanation { get; private set; }
        [field: SerializeField] public int Cost { get; private set; }
        [field: SerializeField] public Unit Prefab { get; private set; }
    }
}
