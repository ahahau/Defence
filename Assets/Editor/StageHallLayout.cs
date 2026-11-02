using System;
using UnityEngine;

/// <summary>긴 홀의 고정 지형만 작성한다. 웨이브/경제 수치를 만들지 않는다.</summary>
public static class StageHallLayout
{
    public const int Width = 39;
    public const int Height = 15;

    public static string[] Create(int number)
    {
        if (number < 1 || number > 7)
            throw new ArgumentOutOfRangeException(nameof(number));
        char[][] cells = new char[Height][];
        for (int y = 0; y < Height; y++)
        {
            cells[y] = new char[Width];
            Array.Fill(cells[y], '#');
        }
        Rect(cells, 1, 2, 8, 11, '.');
        Rect(cells, 13, 1, 23, 11, '.');
        Rect(cells, 28, 2, 37, 11, '.');
        bool reverse = number == 3 || number == 5;
        Rect(cells, 8, reverse ? 7 : 3, 13, reverse ? 10 : 6, '.');
        Rect(cells, 23, reverse ? 3 : 7, 28, reverse ? 6 : 10, '.');
        if (number == 2)
            Rect(cells, 18, 4, 20, 8, '#');
        else if (number == 6)
        {
            Rect(cells, 8, 8, 13, 10, '.');
            Rect(cells, 23, 3, 28, 5, '.');
            Rect(cells, 18, 4, 21, 7, '#');
        }
        else if (number == 7)
        {
            Rect(cells, 16, 3, 18, 7, '#');
            Rect(cells, 20, 7, 21, 10, '#');
            Rect(cells, 8, 8, 13, 10, '.');
        }
        else
            Rect(cells, 18, 4, 20, 7, '#');
        if (number == 5)
            Rect(cells, 8, 3, 13, 5, '.');
        if (number == 3 || number == 7)
        {
            Rect(cells, 4, 3, 5, 4, 'x');
            Rect(cells, 4, 8, 5, 9, 'x');
            if (number == 3)
            {
                Rect(cells, 14, 2, 15, 3, '+');
                Rect(cells, 33, 8, 34, 8, '+');
            }
            else
            {
                Rect(cells, 14, 8, 15, 9, '+');
                Rect(cells, 30, 3, 31, 3, '+');
            }
        }
        Terrain(cells, number);
        string[] rows = new string[Height];
        for (int y = 0; y < Height; y++)
            rows[y] = new string(cells[y]);
        return rows;
    }

    /// <summary>
    /// 지형 칸 문자(2026-11-02 사용자 결정, 위치·칸 수 임시). '~' 물웅덩이, 'g' 마른 풀섬, 'r' 단단한 암반, 'o' 봉인 문양.
    /// 3 물웅덩이, 4 풀섬(불 구멍 옆), 5 암반, 6 봉인 + 물웅덩이, 7 네 가지 전부.
    /// </summary>
    private static readonly (int stage, char tile, RectInt area)[] TerrainTable =
    {
        (3, '~', new RectInt(14, 6, 2, 2)), (3, '~', new RectInt(29, 4, 2, 2)), (3, '~', new RectInt(19, 9, 2, 2)),
        (4, 'g', new RectInt(15, 4, 2, 2)), (4, 'g', new RectInt(27, 7, 2, 2)), (4, 'g', new RectInt(6, 9, 2, 2)),
        (5, 'r', new RectInt(14, 3, 2, 2)), (5, 'r', new RectInt(28, 8, 2, 2)), (5, 'r', new RectInt(21, 9, 2, 2)),
        (6, 'o', new RectInt(14, 8, 2, 2)), (6, 'o', new RectInt(29, 3, 2, 2)),
        (6, '~', new RectInt(6, 4, 2, 2)), (6, '~', new RectInt(22, 9, 2, 2)),
        (7, '~', new RectInt(6, 5, 2, 2)), (7, 'g', new RectInt(13, 5, 2, 2)),
        (7, 'o', new RectInt(30, 9, 2, 2)), (7, 'r', new RectInt(22, 10, 2, 2))
    };

    /// <summary>지형 문자를 일반 바닥('.')으로 되돌린 맵. 지형만 다른지 비교할 때 쓴다.</summary>
    public static string StripTerrain(string row)
    {
        char[] chars = row.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            foreach ((int _, char tile, RectInt _) in TerrainTable)
                if (chars[i] == tile)
                    chars[i] = '.';
        return new string(chars);
    }

    // 일반 바닥만 바꾼다. 벽·설치 불가·강화 칸과 환경 장치 칸은 그대로 둔다(던전문 열·코어 칸은 표에서 비켜 둠).
    private static void Terrain(char[][] cells, int number)
    {
        Vector2Int[] devices = Devices(number);
        foreach ((int stage, char tile, RectInt area) in TerrainTable)
        {
            if (stage != number)
                continue;
            foreach (Vector2Int cell in area.allPositionsWithin)
                if (cells[cell.y][cell.x] == '.' && Array.IndexOf(devices, cell) < 0)
                    cells[cell.y][cell.x] = tile;
        }
    }

    public static Vector2Int[] Doors(int count) => count switch
    {
        1 => new[] { new Vector2Int(2, 6) },
        2 => new[] { new Vector2Int(2, 3), new Vector2Int(2, 10) },
        3 => new[] { new Vector2Int(2, 3), new Vector2Int(2, 6), new Vector2Int(2, 10) },
        _ => throw new ArgumentOutOfRangeException(nameof(count))
    };

    public static Vector2Int[] Devices(int number) => number switch
    {
        4 => new[] { new Vector2Int(14, 5), new Vector2Int(26, 8), new Vector2Int(11, 5) },
        6 => new[] { new Vector2Int(11, 4), new Vector2Int(25, 9), new Vector2Int(25, 4), new Vector2Int(32, 5) },
        7 => new[] { new Vector2Int(15, 5), new Vector2Int(29, 8), new Vector2Int(11, 5), new Vector2Int(26, 8), new Vector2Int(33, 5) },
        _ => Array.Empty<Vector2Int>()
    };

    private static void Rect(char[][] cells, int left, int bottom, int right, int top, char tile)
    {
        for (int y = bottom; y <= top; y++)
        for (int x = left; x <= right; x++)
            cells[y][x] = tile;
    }
}
