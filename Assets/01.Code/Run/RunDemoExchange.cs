namespace Code.Run
{
    /// <summary>글 사건의 대가 있는 교환(2026-11-11 사용자 결정). 저장 값이므로 번호를 바꾸지 않는다.</summary>
    public enum RunDemoExchange
    {
        /// <summary>심장 -10, 고른 부하 한 명 최대 체력 +4·공격력 +1(런 동안). 2026-11-11 "심장 → 금화"에서 바뀜.</summary>
        HeartForUpgrade = 0,

        /// <summary>금화 -40, 고른 부하 한 명 최대 체력 +4·공격력 +1(런 동안).</summary>
        GoldForUpgrade = 1,

        /// <summary>고른 부하 한 명이 최대 체력의 절반을 잃고 아직 없는 유물 하나를 얻는다.</summary>
        HealthForRelic = 2,

        /// <summary>고른 방 하나가 고장 난 채 다음 전투를 시작하고, 아직 없는 유물 하나를 얻는다. 2026-11-11 "방 내구 → 마력"에서 바뀜.</summary>
        RoomForRelic = 3,
    }
}
