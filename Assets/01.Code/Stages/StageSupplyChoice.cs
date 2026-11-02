namespace Code.Stages
{
    /// <summary>
    /// 초반 사건 "남은 보급품"의 선택(2026-11-02 사용자 승인). 준비 저장에 정수로 남으므로 중간에 끼워 넣거나 지우지 않는다.
    /// </summary>
    public enum StageSupplyChoice
    {
        None = 0,
        Gold = 1,
        Repair = 2,
        Skip = 3
    }
}
