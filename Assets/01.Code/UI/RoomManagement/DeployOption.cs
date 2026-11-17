using Code.Units;

namespace Code.UI.RoomManagement
{
    /// <summary>설치 창 유닛 배치 갈래의 대기 유닛 한 종류. 같은 유닛은 몇 명인지로 묶는다.</summary>
    public readonly struct DeployOption
    {
        public DeployOption(UnitDataSO unit, int count, UnitConditionState condition)
        {
            Unit = unit;
            Count = count;
            Condition = condition;
        }

        public UnitDataSO Unit { get; }
        public int Count { get; }

        /// <summary>대기 명단에서 가장 좋은 상태. 배치하면 이 상태로 나간다.</summary>
        public UnitConditionState Condition { get; }
    }
}
