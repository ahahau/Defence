using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 설치 격자 선을 고른다. 열린 칸(방 바닥, 칸막이 없음)에 닿은 경계만 그리고, 이어진 경계는 선 하나로 합친다.
    /// 예전에는 격자 틀 전체(columns × rows)에 선을 그어 방 모양 밖·기둥·칸막이 위까지 선이 지나갔다.
    /// 좌표는 칸 꼭짓점이다: (c, r)은 칸 (c, r)의 왼쪽 아래 모서리.
    /// </summary>
    public static class PlacementGridLines
    {
        public static void Collect(int columns, int rows, Func<int, int, bool> isOpen, List<(Vector2Int start, Vector2Int end)> into)
        {
            into.Clear();
            if (columns <= 0 || rows <= 0 || isOpen == null)
                return;

            bool Open(int column, int row) =>
                column >= 0 && column < columns && row >= 0 && row < rows && isOpen(column, row);

            for (var x = 0; x <= columns; x++)
            {
                var start = -1;
                for (var y = 0; y <= rows; y++)
                {
                    var visible = y < rows && (Open(x - 1, y) || Open(x, y));
                    if (visible && start < 0)
                        start = y;
                    else if (!visible && start >= 0)
                    {
                        into.Add((new Vector2Int(x, start), new Vector2Int(x, y)));
                        start = -1;
                    }
                }
            }

            for (var y = 0; y <= rows; y++)
            {
                var start = -1;
                for (var x = 0; x <= columns; x++)
                {
                    var visible = x < columns && (Open(x, y - 1) || Open(x, y));
                    if (visible && start < 0)
                        start = x;
                    else if (!visible && start >= 0)
                    {
                        into.Add((new Vector2Int(start, y), new Vector2Int(x, y)));
                        start = -1;
                    }
                }
            }
        }
    }
}
