namespace Code.Manager
{
    /// <summary>한 전환(영업 종료)에 띄우는 사건 하나의 종류.</summary>
    public enum TransitionEventKind
    {
        None = 0,

        /// <summary>일차 대화(일반 랜덤 사건). "아무것도 하지 않음"이 붙는다.</summary>
        Normal = 1,

        /// <summary>가장 위험한 운영 문제(부채 한도 위험·망가진 시설).</summary>
        Situation = 2,

        /// <summary>운영 방침(주요 사건). 반드시 고른다.</summary>
        Major = 3,

        /// <summary>청산 위기. 운영 방침보다 먼저 뜬다.</summary>
        Urgent = 4
    }
}
