namespace Code.Stages
{
    /// <summary>스테이지에 고정된 환경 장치 종류. 직렬화되므로 중간에 끼워 넣지 않는다.</summary>
    public enum StageDeviceKind
    {
        /// <summary>주기마다 칸에 불을 뿜어 적·부하 모두에게 피해. 지나갈 수 있다.</summary>
        FireVent = 0,
        /// <summary>주기마다 통로 한 칸을 막는다. 닫힌 동안은 벽이고 부술 수 없다.</summary>
        Gate = 1,
        /// <summary>전투 중 플레이어가 눌러 주변 적에게 피해. 재사용 대기가 있다.</summary>
        Rockfall = 2
    }
}
