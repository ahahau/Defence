namespace Code.Stages
{
    /// <summary>스테이지 한정 적 특수 규칙(머리 위 표식). 직렬화되므로 중간에 끼워 넣지 않는다.</summary>
    public enum StageEnemyRule
    {
        None = 0,
        /// <summary>바리케이드를 막힌 칸으로 보지 않고 넘어간다. 부하는 그대로 막힌다.</summary>
        Vault = 1,
        /// <summary>부하와 교전하지 않고 코어로만 간다. 완전히 막히면 부수고 지나간다.</summary>
        Rush = 2
    }
}
