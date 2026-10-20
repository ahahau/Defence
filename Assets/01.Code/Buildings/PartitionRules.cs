namespace Code.Buildings
{
    /// <summary>칸막이 벽 수치. 비용·내구도는 데이터·프리팹이 정하고, 여기는 모험가 쪽 판단만 둔다.</summary>
    public static class PartitionRules
    {
        /// <summary>
        /// 칸막이 때문에 이만큼(칸) 넘게 돌아가야 하면 모험가는 돌아가지 않고 벽을 부순다.
        /// 미로처럼 길게 꺾어 두면 그만큼 빨리 뚫린다.
        /// </summary>
        public const int SmashDetourCells = 4;

        /// <summary>벽을 칠 때 한 번 휘두르는 간격(초, 게임 시간). 공격 간격이 더 길면 그 값을 쓴다.</summary>
        public const float MinSmashInterval = 0.6f;

        /// <summary>벽을 부수러 가는 데 쓰는 최대 시간(초). 넘기면 포기하고 돌아간다.</summary>
        public const float MaxSmashSeconds = 20f;
    }
}
