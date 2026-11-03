namespace Code.Stages
{
    /// <summary>벽·바리케이드에 붙이는 함정 종류(2026-11-03 사용자 결정). 직렬화되므로 중간에 끼워 넣지 않는다.</summary>
    public enum StageWallTrapKind
    {
        /// <summary>앞 칸의 적을 벽 반대쪽으로 민다. 구덩이에 닿으면 떨어진다.</summary>
        Push = 0,
        /// <summary>앞 직선에서 가장 가까운 적 한 명을 쏜다.</summary>
        Arrow = 1,
        /// <summary>바로 앞 몇 칸의 적을 모두 찌른다.</summary>
        Spear = 2,
        /// <summary>앞 칸들에 독가스를 뿜어 잠시 지속 피해를 준다.</summary>
        Gas = 3
    }
}
