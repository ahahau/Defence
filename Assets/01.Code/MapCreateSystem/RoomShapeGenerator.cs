using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 확장 후보마다 방 모양을 시드로 깎는다. 같은 시드면 늘 같은 모양이고, 저장에는 깎은 모양 자체를 넣는다.
    ///
    /// 틀 크기를 고르고 모서리·변의 홈을 깎고 방 안에 기둥과 칸막이 벽을 세운 뒤 규칙(<see cref="RoomLayoutRules.Validate(RoomShape, out string)"/>)에
    /// 맞을 때까지 다시 깎는다. 한 칸 폭 벽 돌기·바닥 틈은 3/4 시점 벽(면 2줄 + 난간)이 들어갈 자리가 없어 검은 틈이 생기므로 버리고,
    /// 틀을 거의 다 채운 모양은 네모로 보여 버린다.
    /// </summary>
    public static class RoomShapeGenerator
    {
        public const int MaxAttempts = 1000;

        /// <summary>틀 대비 바닥 비율 상한(입구 제외).</summary>
        public const float MaxFillRatio = 0.82f;

        /// <summary>방 안 칸막이 벽 한 줄의 최소 길이(칸).</summary>
        public const int MinWallCells = 2;

        /// <summary>벽 한 줄 길이 상한의 하한. 상한은 놓인 변의 절반이다.</summary>
        private const int MinWallLongest = 4;

        /// <summary>입구 틀(중형). 문 넷과 바깥문이 들어가는 넉넉한 방.</summary>
        private const int EntranceColumns = 12;
        private const int EntranceRows = 11;

        /// <summary>
        /// 방 모양을 만든다. 규칙에 맞는 모양을 못 찾으면(거의 없음) 문이 모두 열리는 작은 직사각형으로 대신한다.
        /// </summary>
        public static RoomShape Generate(RoomSize size, RoomDoors doors, bool entrance, int seed)
        {
            var random = new System.Random(seed);
            var id = entrance ? "gen-entrance" : $"gen-{seed:x8}";
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var walled = Carve(random, size, entrance, out var plain);

                // 칸막이 벽이 규칙을 깨면 같은 모양에서 벽만 뺀 것을 쓴다. 벽 때문에 시도를 다 써 버리지 않게 한다.
                var shape = new RoomShape(id, size, entrance, doors, walled);
                if (IsUsable(shape, entrance))
                    return shape;

                shape = new RoomShape(id, size, entrance, doors, plain);
                if (IsUsable(shape, entrance))
                    return shape;
            }

            Debug.LogWarning($"방 모양을 깎지 못해 직사각형으로 대신합니다(시드 {seed}, {size}).");
            return new RoomShape(id, size, entrance, doors, FallbackRows(size));
        }

        /// <summary>
        /// 후보의 문을 정한다. 부모 쪽 문은 반드시 있고, 나머지는 무작위로 1~3개 더(2개 50%·3개 35%·4개 15%).
        /// </summary>
        public static RoomDoors RollDoors(RoomDoors requiredDoor, int seed)
        {
            var random = new System.Random(seed);
            var roll = random.NextDouble();
            var total = roll < 0.5 ? 2 : roll < 0.85 ? 3 : 4;
            var doors = requiredDoor;
            var others = new List<RoomDoors>();
            foreach (var side in RoomDoorRules.Sides)
            {
                if (!RoomDoorRules.Has(doors, side))
                    others.Add(side);
            }

            while (RoomDoorRules.Count(doors) < total && others.Count > 0)
            {
                var index = random.Next(others.Count);
                doors |= others[index];
                others.RemoveAt(index);
            }

            return doors;
        }

        /// <summary>
        /// 가장자리와 이어진 막힌 칸이 좌우 또는 위아래로 바닥에 끼어 있거나(한 칸 폭 벽), 바닥 칸이 좌우 또는 위아래로
        /// 막힘에 끼어 있으면(한 칸 폭 바닥) 참. 방 안 기둥(갇힌 막힌 칸)은 따로 그리므로 괜찮다.
        /// </summary>
        public static bool HasThinFeatures(RoomShape shape)
        {
            bool Blocked(int x, int y) => !shape.IsFloor(x, y);

            for (var y = 0; y < shape.Rows; y++)
            for (var x = 0; x < shape.Columns; x++)
            {
                if (shape.IsFloor(x, y))
                {
                    if ((Blocked(x - 1, y) && Blocked(x + 1, y)) || (Blocked(x, y - 1) && Blocked(x, y + 1)))
                        return true;
                    continue;
                }

                var edge = x == 0 || y == 0 || x == shape.Columns - 1 || y == shape.Rows - 1;
                if (!edge)
                    continue;

                if ((shape.IsFloor(x - 1, y) && shape.IsFloor(x + 1, y))
                    || (shape.IsFloor(x, y - 1) && shape.IsFloor(x, y + 1)))
                    return true;
            }

            return false;
        }

        private static bool IsUsable(RoomShape shape, bool entrance) =>
            RoomLayoutRules.Validate(shape, out _) && !HasThinFeatures(shape)
            && (entrance || RoomLayoutRules.CountFloor(shape) <= shape.Columns * shape.Rows * MaxFillRatio);

        /// <summary>틀을 고르고 모서리를 깎고 홈을 내고 방 안에 기둥을 세운다. plain은 칸막이 벽을 넣기 전 모양이다.</summary>
        private static string[] Carve(System.Random random, RoomSize size, bool entrance, out string[] plain)
        {
            var maxColumns = RoomSizeRules.MaxColumns(size);
            var maxRows = RoomSizeRules.MaxRows(size);
            var columns = entrance ? EntranceColumns : random.Next(Mathf.Max(4, maxColumns - 3), maxColumns + 1);
            var rows = entrance ? EntranceRows : random.Next(Mathf.Max(4, maxRows - 3), maxRows + 1);
            var floor = new bool[columns, rows];
            for (var x = 0; x < columns; x++)
            for (var y = 0; y < rows; y++)
                floor[x, y] = true;

            // 모서리 깎기: 2~4곳, 크기는 틀의 절반 가까이까지.
            var corners = random.Next(2, 5);
            for (var i = 0; i < corners; i++)
            {
                var w = random.Next(1, Mathf.Max(2, columns / 2));
                var h = random.Next(1, Mathf.Max(2, rows / 2));
                var right = random.Next(2) == 0;
                var top = random.Next(2) == 0;
                Fill(floor, right ? columns - w : 0, top ? rows - h : 0, w, h);
            }

            // 변의 홈: 2~5곳. 길이는 틀의 1/3까지, 깊이는 짧은 변의 1/4까지(최소 1~2) — 큰 방도 네모로 보이지 않게.
            var notches = random.Next(2, 6);
            var maxNotchLength = Mathf.Max(3, columns / 3);
            var maxNotchDepth = Mathf.Max(2, Mathf.Min(columns, rows) / 4);
            for (var i = 0; i < notches; i++)
            {
                var side = random.Next(4);
                var length = random.Next(1, maxNotchLength + 1);
                var depth = random.Next(1, maxNotchDepth + 1);
                if (side < 2)
                {
                    var x = random.Next(0, Mathf.Max(1, columns - length));
                    Fill(floor, x, side == 0 ? rows - depth : 0, length, depth);
                }
                else
                {
                    var y = random.Next(0, Mathf.Max(1, rows - length));
                    Fill(floor, side == 2 ? columns - depth : 0, y, depth, length);
                }
            }

            // 방 안 기둥: 큰 방일수록 많이. 가장자리에 붙지 않게 한 칸 안쪽에만.
            var pillars = size switch
            {
                RoomSize.Large => random.Next(0, 7),
                RoomSize.Medium => random.Next(0, 5),
                _ => random.Next(0, 3)
            };
            for (var i = 0; i < pillars && columns > 2 && rows > 2; i++)
                floor[random.Next(1, columns - 1), random.Next(1, rows - 1)] = false;

            plain = ToLines(floor, columns, rows);
            if (!entrance)
                CarveWalls(random, floor, size, columns, rows);

            return ToLines(floor, columns, rows);
        }

        /// <summary>
        /// 방 안 칸막이 벽: 소형 0~1줄, 중형 0~2줄, 대형 0~3줄. 길이는 2칸부터 놓인 변의 절반(최소 4칸)까지.
        /// 가장자리에 붙지 않게 한 칸 안쪽에만 세워 바깥 벽과 이어지지 않는다(이어지면 3/4 시점 벽 띠가 들어갈 자리가 없다).
        /// 길이 끊기는 모양은 검사에서 버려진다.
        /// </summary>
        private static void CarveWalls(System.Random random, bool[,] floor, RoomSize size, int columns, int rows)
        {
            var count = size switch
            {
                RoomSize.Large => random.Next(0, 4),
                RoomSize.Medium => random.Next(0, 3),
                _ => random.Next(0, 2)
            };

            for (var i = 0; i < count && columns > 4 && rows > 4; i++)
            {
                var horizontal = random.Next(2) == 0;
                var span = horizontal ? columns - 2 : rows - 2;
                var maxLength = Mathf.Max(MinWallLongest, span / 2);
                var length = Mathf.Min(span, random.Next(MinWallCells, maxLength + 1));
                var along = random.Next(1, columns - length);
                var across = random.Next(1, rows - 1);
                if (!horizontal)
                {
                    along = random.Next(1, columns - 1);
                    across = random.Next(1, rows - length);
                }

                for (var step = 0; step < length; step++)
                {
                    if (horizontal)
                        floor[along + step, across] = false;
                    else
                        floor[along, across + step] = false;
                }
            }
        }

        private static string[] FallbackRows(RoomSize size)
        {
            var (columns, rows) = size switch
            {
                RoomSize.Small => (8, 7),
                RoomSize.Large => (15, 13),
                _ => (11, 10)
            };
            var floor = new bool[columns, rows];
            for (var x = 0; x < columns; x++)
            for (var y = 0; y < rows; y++)
                floor[x, y] = true;
            return ToLines(floor, columns, rows);
        }

        private static string[] ToLines(bool[,] floor, int columns, int rows)
        {
            var lines = new string[rows];
            for (var y = 0; y < rows; y++)
            {
                var chars = new char[columns];
                for (var x = 0; x < columns; x++)
                    chars[x] = floor[x, rows - 1 - y] ? '.' : '#';
                lines[y] = new string(chars);
            }

            return lines;
        }

        private static void Fill(bool[,] floor, int x0, int y0, int w, int h)
        {
            for (var x = Mathf.Max(0, x0); x < x0 + w && x < floor.GetLength(0); x++)
            for (var y = Mathf.Max(0, y0); y < y0 + h && y < floor.GetLength(1); y++)
                floor[x, y] = false;
        }
    }
}
