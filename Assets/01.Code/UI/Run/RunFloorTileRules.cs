namespace Code.UI.Run
{
    /// <summary>
    /// 평면도 칸(위가 +y)을 3/4 시점 타일로 고르는 규칙. 바닥 위쪽 벽은 벽돌 면, 나머지 벽은 윗면 테두리 9조각이다.
    /// 테두리 순서는 위·가운데·아래 줄 × 왼·가운데·오른쪽이다.
    /// </summary>
    public static class RunFloorTileRules
    {
        public const int RimCenter = 4;

        /// <summary>안쪽 모서리 번호의 시작. 9~12는 바닥이 왼쪽 위·오른쪽 위·왼쪽 아래·오른쪽 아래 대각선에 있는 조각이다.</summary>
        public const int RimInnerStart = 9;

        public static RunFloorCellKind KindOf(bool[,] open, int x, int y)
        {
            if (IsOpen(open, x, y)) return IsFace(open, x, y + 1) ? RunFloorCellKind.LitFloor : RunFloorCellKind.Floor;
            return IsOpen(open, x, y - 1) ? RunFloorCellKind.Face : RunFloorCellKind.Wall;
        }

        /// <summary>안쪽 모서리 조각이 있으면 9~12를, 없으면 반대쪽 바깥 모서리(작은 돌기가 남는다)를 돌려준다.</summary>
        public static int RimIndex(bool[,] open, int x, int y, bool hasInner)
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
            // 안쪽 모서리 조각은 타일셋에 없어 생성기가 테두리 조각을 합쳐 만든다(2026-11-12 사용자 목업 승인).
            if (hasInner) return RimInnerStart + (upLeft ? 0 : upRight ? 1 : downLeft ? 2 : 3);
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
