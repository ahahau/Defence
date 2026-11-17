namespace Code.Run
{
    /// <summary>예고 종료 이유. 피해 취소와 빗나감을 구분한다.</summary>
    public enum RunTelegraphEndReason
    {
        None = 0,
        Completed = 1,
        Damage = 2,
        LockedDoor = 3,
        CasterLeft = 4,
        TargetEscaped = 5,
        BattleEnded = 6,
    }
}
