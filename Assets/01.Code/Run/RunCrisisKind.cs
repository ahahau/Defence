namespace Code.Run
{
    /// <summary>앞의 세 종류는 자동 멈춤, 나머지는 알림이다.</summary>
    public enum RunCrisisKind
    {
        Telegraph = 0,
        RoomBroken = 1,
        ThiefAtVault = 2,
        LowHealth = 3,
        HeartEntered = 4,
    }
}
