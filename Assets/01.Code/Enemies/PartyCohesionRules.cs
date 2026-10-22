namespace Code.Enemies
{
    /// <summary>동료를 기다리되 작은 방의 정원에 따른 순차 통과는 허용한다.</summary>
    public static class PartyCohesionRules
    {
        /// <summary>시설·전투·이동 중인 동료는 시간 제한 없이 기다린다.</summary>
        public static bool ShouldWaitForMember(bool moving, bool busy, bool sameRoom, bool roomFull) =>
            moving || busy || !sameRoom && !roomFull;

        /// <summary>
        /// 줄지어 걷던 걸음을 싸움 때문에 그만둘지. 줄은 전투에 들어가기 전까지만 따른다 — 예전에는 걷기 시작한 동선을
        /// 끝까지 걸어, 방을 옮기는 파티원은 상대가 있는 방에서도 순회 잠금에 묶여 줄 자리까지 걸어가기만 했다
        /// (1일차 실측: 상대와 같은 방에 있던 프레임의 39%).
        /// 방을 옮기는 걸음은 몸이 새 방 바닥 위에 들어선 뒤(<paramref name="passedDoor"/>)에만 그만둔다. 그 전에 멈추면
        /// 몸은 앞 방·방 사이 길에 있는데 전투 범위는 새 방이라 상대에게 닿지 못하고 서 버린다(첫 수정 실측에서 셋 다 굳음).
        /// 귀환 걸음은 싸움과 상관없이 끝까지 간다.
        /// </summary>
        public static bool ShouldBreakWalkForCombat(bool returning, bool crossingRooms, bool passedDoor,
            bool inCombat, bool roomHasOpponents) =>
            !returning && (inCombat || roomHasOpponents) && (!crossingRooms || passedDoor);
    }
}
