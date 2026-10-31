namespace Code.Stages
{
    /// <summary>
    /// 준비 단계에 부하 한 명씩 주는 이동 범위 명령(2026-11-01 사용자 결정). 능력치는 바꾸지 않는다.
    /// 준비 저장에 정수로 남으므로 중간에 끼우거나 지우지 않는다.
    /// </summary>
    public enum StageUnitCommand
    {
        Default = 0,
        Hold = 1,
        Pursue = 2
    }
}
