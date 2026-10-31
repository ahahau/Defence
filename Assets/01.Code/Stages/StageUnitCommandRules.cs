namespace Code.Stages
{
    /// <summary>
    /// 명령별 추적·발견 범위와 문구. 수치는 임시 값이다.
    /// 사수는 걸어서 쫓지 않고 사거리 안의 적만 친다(StageActor). 추적 반경을 0이 아니라 1칸으로 둔 것은
    /// 밀려난 뒤 제자리로 돌아오는 길이 막히지 않게 하려는 것이다.
    /// </summary>
    public static class StageUnitCommandRules
    {
        public const float PursueMultiplier = 1.5f;
        public const float HoldCells = 1f;

        /// <summary>배치 칸에서 이 거리 밖으로는 쫓지도 걷지도 않는다.</summary>
        public static float LeashRadius(StageUnitCommand command, float baseRadius, float cellSize) => command switch
        {
            StageUnitCommand.Hold => HoldCells * cellSize,
            StageUnitCommand.Pursue => baseRadius * PursueMultiplier,
            _ => baseRadius
        };

        /// <summary>자기 자리에서 이 거리 안의 적을 알아챈다. 사수는 추적 반경이 좁아 사거리 안 적만 남는다.</summary>
        public static float EngageRadius(StageUnitCommand command, float baseRadius) =>
            command == StageUnitCommand.Pursue ? baseRadius * PursueMultiplier : baseRadius;

        public static string Label(StageUnitCommand command) => command switch
        {
            StageUnitCommand.Hold => "사수",
            StageUnitCommand.Pursue => "추격",
            _ => "기본"
        };

        public static string Description(StageUnitCommand command) => command switch
        {
            StageUnitCommand.Hold => "자리를 지키며 사거리 안의 적만 칩니다",
            StageUnitCommand.Pursue => "적을 더 멀리서 알아채고 1.5배 범위까지 쫓습니다",
            _ => "배치 지점 주변을 지키다 돌아옵니다"
        };
    }
}
