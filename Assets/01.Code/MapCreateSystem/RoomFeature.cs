namespace Code.MapCreateSystem
{
    /// <summary>방에 붙는 특수 효과. 후보 위에 미리 보이고, 방을 연 뒤에도 그 방에 남는다.</summary>
    public enum RoomFeature
    {
        /// <summary>효과 없음. 옛 저장도 이 값으로 읽힌다.</summary>
        None = 0,

        /// <summary>풍요: 그 방 수익 시설 수입 증가.</summary>
        Bounty = 1,

        /// <summary>요새: 그 방 부하가 받는 피해 감소.</summary>
        Fortress = 2,

        /// <summary>함정 지대: 그 방 함정 피해 증가.</summary>
        TrapGround = 3
    }
}
