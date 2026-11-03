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
        /// <summary>벽 함정(2026-11-03). 부하·바닥 함정 대신 이것만 채운다.</summary>
        [field: SerializeField] public StageWallTrapSO WallTrap { get; private set; }
        public string Label => Unit != null ? Unit.Name : Trap != null ? Trap.DisplayName : WallTrap != null ? WallTrap.Label : Id;
        public int Cost => Mathf.Max(0, Unit != null ? Unit.Cost : Trap != null ? Trap.Cost : WallTrap != null ? WallTrap.Cost : 0);
        /// <summary>바닥 함정이나 벽 함정. 함정 본편은 전투 중에도 설치·강화·철거한다.</summary>
        public bool IsAnyTrap => Trap != null || WallTrap != null;

        public StageOption(string id, UnitDataSO unit, BuildingDataSO trap, string requiredClear, StageWallTrapSO wallTrap = null)
        {
            Id = id;
            Unit = unit;
            Trap = trap;
            RequiredClear = requiredClear;
            WallTrap = wallTrap;
        }
    }
}
