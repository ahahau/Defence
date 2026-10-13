namespace Code.Enemies
{
    /// <summary>
    /// 파티가 리더를 중심으로 뭉쳐 다니는 규칙(기획서: 리더 동기 이동).
    ///
    /// 파티원이 저마다 길을 찾으면 파티가 방 여러 개에 흩어져, 파티 단위로 막고 싸우는 그림이 나오지 않는다.
    /// 리더보다 한 방 넘게 앞서려는 파티원은 리더를 기다린다. 다만 리더가 시설에 오래 머물거나 싸움에 묶이면
    /// 파티 전체가 굳으므로 오래 기다리지는 않는다.
    /// </summary>
    public static class PartyCohesionRules
    {
        /// <summary>파티원이 리더보다 앞설 수 있는 방 수.</summary>
        public const int MaxRoomsFromLeader = 1;

        /// <summary>리더를 기다리는 최대 게임 시간(초). 넘으면 혼자 간다.</summary>
        public const float MaxLeaderWaitSeconds = 5f;

        /// <summary>다음 방으로 가면 리더와 이만큼 떨어질 때, 지금까지 기다린 시간으로 더 기다릴지 정한다.</summary>
        public static bool ShouldWaitForLeader(int roomsFromLeaderAfterMove, float waitedSeconds) =>
            roomsFromLeaderAfterMove > MaxRoomsFromLeader && waitedSeconds < MaxLeaderWaitSeconds;
    }
}
