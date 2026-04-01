using UnityEngine;

namespace _01.Code.Units
{
    [CreateAssetMenu(fileName = "BuildingData", menuName = "SO/Building/Data", order = 0)]
    public class UnitDataSO : ScriptableObject
    {
        [field: SerializeField] public string Name { get; private set; }
        [field: SerializeField] public string Explanation { get; private set; }
        [field: SerializeField] public int Cost { get; private set; }
        [field: SerializeField] public Sprite CardIcon { get; private set; }
        [field: SerializeField] public Color CardColor { get; private set; } = Color.white;
        [field: SerializeField] public Unit Prefab { get; private set; }
    }
}
