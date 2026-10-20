namespace Code.MapCreateSystem
{
    /// <summary>
    /// 칸막이 벽을 없는 셈 치는 칸 지도. 모험가가 "벽이 없으면 얼마나 가까운가"를 재서 부술지 정할 때 쓴다.
    /// 길찾기마다 새로 만들지 않도록 격자만 바꿔 끼워 쓴다.
    /// </summary>
    public sealed class RoomFloorMap : IRoomCellMap
    {
        public NodeTrapGrid Grid { get; set; }

        public int Columns => Grid != null ? Grid.Columns : 0;
        public int Rows => Grid != null ? Grid.Rows : 0;

        public bool IsWalkableCell(int column, int row) =>
            Grid != null && Grid.IsValidCell(column, row) && !Grid.IsBlockedCell(column, row);
    }
}
