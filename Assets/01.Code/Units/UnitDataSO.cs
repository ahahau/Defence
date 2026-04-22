using UnityEngine;

namespace _01.Code.Units
{
    [CreateAssetMenu(menuName = "SO/Unit/Data", fileName = "UnitData", order = 0)]
    public class UnitDataSO : ScriptableObject
    {
        [field:SerializeField] public Sprite Sprite { get; private set; }
        [field:SerializeField] public string Name { get; private set; }
        [field:SerializeField] public int Cost { get; private set; }
        [field:SerializeField] public bool Locked { get; private set; }
    }
}