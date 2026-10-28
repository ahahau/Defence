namespace Code.Stages
{
    /// <summary>스테이지 부하 궁극기 게이지 충전량(게이지 1이 가득). 2026-10-28 승인된 임시 값이며 밸런스 확정값이 아니다.</summary>
    public static class StageUltimateRules
    {
        /// <summary>평타가 맞을 때마다(약 8번이면 가득).</summary>
        public const float ChargePerHit = 0.125f;
        /// <summary>피해를 받을 때마다(약 14번이면 가득).</summary>
        public const float ChargePerHurt = 0.07f;
    }
}
