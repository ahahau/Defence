namespace Code.Run
{
    /// <summary>침입자의 특수 행동. 직렬화되므로 값을 중간에 끼우지 않는다.</summary>
    public enum RunIntruderRole
    {
        None = 0,

        /// <summary>금고에서 금화를 훔쳐 입구로 도주한다.</summary>
        Thief = 1,

        /// <summary>가장 가까운 켜진 방을 노리고 방 시스템에 2배 피해를 준다.</summary>
        Sapper = 2,
    }
}
