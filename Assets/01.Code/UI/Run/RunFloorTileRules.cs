namespace Code.UI.Run
{
    /// <summary>
    /// 평면도 칸(위가 +y)을 3/4 시점 타일로 고르는 규칙. 바닥 위쪽 벽은 벽돌 면, 나머지 벽은 윗면 테두리 9조각이다.
    /// 테두리 순서는 위·가운데·아래 줄 × 왼·가운데·오른쪽이다.
    /// </summary>
    public static class RunFloorTileRules
    {
        public const int RimCenter = 4;

        public static RunFloorCellKind KindOf(bool[,] open, int x, int y)
        {
            if (IsOpen(open, x, y)) return IsFace(open, x, y + 1) ? RunFloorCellKind.LitFloor : RunFloorCellKind.Floor;
            return IsOpen(open, x, y - 1) ? RunFloorCellKind.Face : RunFloorCellKind.Wall;
        }

        public static int RimIndex(bool[,] open, int x, int y)
        {
            bool top = !IsTop(open, x, y + 1);
            bool bottom = !IsTop(open, x, y - 1);
            bool left = !IsTop(open, x - 1, y);
            bool right = !IsTop(open, x + 1, y);
            int column = left ? 0 : right ? 2 : 1;
            int row = top ? 0 : bottom ? 2 : 1;
            if (top || bottom || left || right) return row * 3 + column;
            bool upLeft = !IsTop(open, x - 1, y + 1);
            bool upRight = !IsTop(open, x + 1, y + 1);
            bool downLeft = !IsTop(open, x - 1, y - 1);
            bool downRight = !IsTop(open, x + 1, y - 1);
            if (!(upLeft || upRight || downLeft || downRight)) return RimCenter;
            // 안쪽 모서리 조각이 타일셋에 없어 반대쪽 바깥 모서리로 잇는다(작은 돌기가 남는다).
            column = upLeft || downLeft ? 2 : 0;
            row = upLeft || upRight ? 2 : 0;
            return row * 3 + column;
        }

        private static bool IsOpen(bool[,] open, int x, int y) =>
            x >= 0 && y >= 0 && x < open.GetLength(0) && y < open.GetLength(1) && open[x, y];

        private static bool IsFace(bool[,] open, int x, int y) => !IsOpen(open, x, y) && IsOpen(open, x, y - 1);
        private static bool IsTop(bool[,] open, int x, int y) => !IsOpen(open, x, y) && !IsFace(open, x, y);
    }
}
