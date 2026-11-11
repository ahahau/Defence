namespace Code.Run
{
    /// <summary>앞의 세 종류와 방 공격 시작은 자동 멈춤, 나머지는 알림이다.</summary>
    public enum RunCrisisKind
    {
        Telegraph = 0,
        RoomBroken = 1,
        ThiefAtVault = 2,
        LowHealth = 3,
        HeartEntered = 4,

        /// <summary>방을 노리는 적(궁수·마법사)이 목표 방을 치기 시작했다(2026-11-11 사용자 결정, 자동 멈춤).</summary>
        RoomUnderAttack = 5,
    }
}
