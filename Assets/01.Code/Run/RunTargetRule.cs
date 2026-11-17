namespace Code.Run
{
    /// <summary>공격 효과가 대상을 고르는 방식. 직렬화되므로 값을 바꾸지 않는다(1·2는 턴제 줄 규칙이라 지웠다).</summary>
    public enum RunTargetRule
    {
        /// <summary>시전자의 공격 거리 안에서 가까운 상대부터 MaxTargets명.</summary>
        Nearest = 0,

        /// <summary>시전자의 공격 거리 안 상대 전원.</summary>
        AllInRange = 3,
    }
}
