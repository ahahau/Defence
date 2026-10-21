namespace Code.Enemies
{
    /// <summary>동료를 기다리되 작은 방의 정원에 따른 순차 통과는 허용한다.</summary>
    public static class PartyCohesionRules
    {
        /// <summary>시설·전투·이동 중인 동료는 시간 제한 없이 기다린다.</summary>
        public static bool ShouldWaitForMember(bool moving, bool busy, bool sameRoom, bool roomFull) =>
            moving || busy || !sameRoom && !roomFull;
    }
}
