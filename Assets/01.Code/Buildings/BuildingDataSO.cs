using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(menuName = "SO/Building/Data", fileName = "BuildingData", order = 0)]
    public class BuildingDataSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public int Cost { get; private set; }
        [field: SerializeField] public Building Prefab { get; private set; }
        [field: SerializeField] public bool Unique { get; private set; }
    }
}
