namespace Code.MapCreateSystem
{
    /// <summary>방 안 칸 길찾기가 읽는 칸 지도. 칸 (0, 0)은 왼쪽 아래다.</summary>
    public interface IRoomCellMap
    {
        int Columns { get; }
        int Rows { get; }

        /// <summary>그 칸을 걸어 지나갈 수 있는가. 격자 밖은 false.</summary>
        bool IsWalkableCell(int column, int row);
    }
}
