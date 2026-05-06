using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Units
{
    [CreateAssetMenu(menuName = "SO/Unit/Data", fileName = "UnitData", order = 0)]
    public class UnitDataSO : EntityDataSO
    {
        [field:SerializeField] public Sprite Sprite { get; private set; }
        [field:SerializeField] public string Name { get; private set; }
        [field:SerializeField] public int Cost { get; private set; }
        [field: SerializeField, Min(1)] public int MagicCost { get; private set; } = 1;
        [field:SerializeField] public bool Locked { get; private set; }
        [field: SerializeField, Min(0)] public int BaseDanger { get; private set; } = 1;
        [field: SerializeField, Min(0)] public int DangerIncreaseOnCombat { get; private set; } = 1;
    }
}
