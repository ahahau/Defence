using Code.Buildings;
using Code.Units;
using UnityEngine;

namespace Code.Stages
{
    [System.Serializable]
    public sealed class StageOption
    {
        [field: SerializeField] public string Id { get; private set; }
        [field: SerializeField] public UnitDataSO Unit { get; private set; }
        [field: SerializeField] public BuildingDataSO Trap { get; private set; }
        [field: SerializeField] public string RequiredClear { get; private set; }
        public string Label => Unit != null ? Unit.Name : Trap != null ? Trap.DisplayName : Id;
        public int Cost => Mathf.Max(0, Unit != null ? Unit.Cost : Trap != null ? Trap.Cost : 0);

        public StageOption(string id, UnitDataSO unit, BuildingDataSO trap, string requiredClear)
        {
            Id = id;
            Unit = unit;
            Trap = trap;
            RequiredClear = requiredClear;
        }
    }
}
