namespace Code.Manager
{
    /// <summary>보스 배너 종류. 색과 첫 줄 문구가 갈린다.</summary>
    public enum BossBannerKind
    {
        /// <summary>청산일 보스 등장.</summary>
        Arrival,

        /// <summary>보스가 부른 증원 파티 합류.</summary>
        Reinforcement,

        /// <summary>호위가 무너져 보스가 마지막 단계에 들어감.</summary>
        Rage,

        /// <summary>마지막 판(끝이 있는 판에서만). 끝없는 판에서는 쓰이지 않는다.</summary>
        Final
    }
}
