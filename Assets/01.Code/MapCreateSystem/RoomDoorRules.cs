using System.Text;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 문과 방향의 규칙. 방은 서로 마주 보는 문이 있을 때만 이어진다.
    ///
    /// 입구는 예외다. 입구의 위·아래 문은 ㄱ자 복도로 꺾여 왼쪽 위·아래 방의 오른쪽 문에 닿는다.
    /// 오른쪽 문은 바깥문이라 확장으로 이어지지 않는다(침입자가 들어오는 곳).
    /// </summary>
    public static class RoomDoorRules
    {
        /// <summary>위·오른쪽·아래·왼쪽. 그래프가 이웃을 붙이는 순서와 같다.</summary>
        public static readonly RoomDoors[] Sides = { RoomDoors.Up, RoomDoors.Right, RoomDoors.Down, RoomDoors.Left };

        /// <summary>입구의 바깥문. 확장 후보를 뻗지 않는다.</summary>
        public const RoomDoors EntranceOutsideDoor = RoomDoors.Right;

        /// <summary>입구에서 복도가 서쪽으로 꺾이는 문.</summary>
        public const RoomDoors EntranceTurningDoors = RoomDoors.Up | RoomDoors.Down;

        public static Vector2Int ToDirection(RoomDoors side) => side switch
        {
            RoomDoors.Up => Vector2Int.up,
            RoomDoors.Right => Vector2Int.right,
            RoomDoors.Down => Vector2Int.down,
            RoomDoors.Left => Vector2Int.left,
            _ => Vector2Int.zero
        };

        public static RoomDoors FromDirection(Vector2Int direction)
        {
            if (direction == Vector2Int.up) return RoomDoors.Up;
            if (direction == Vector2Int.right) return RoomDoors.Right;
            if (direction == Vector2Int.down) return RoomDoors.Down;
            if (direction == Vector2Int.left) return RoomDoors.Left;
            return RoomDoors.None;
        }

        public static RoomDoors Opposite(RoomDoors side) => side switch
        {
            RoomDoors.Up => RoomDoors.Down,
            RoomDoors.Right => RoomDoors.Left,
            RoomDoors.Down => RoomDoors.Up,
            RoomDoors.Left => RoomDoors.Right,
            _ => RoomDoors.None
        };

        public static bool Has(RoomDoors doors, RoomDoors side) => side != RoomDoors.None && (doors & side) == side;

        public static int Count(RoomDoors doors)
        {
            var count = 0;
            foreach (var side in Sides)
            {
                if (Has(doors, side))
                    count++;
            }

            return count;
        }

        /// <summary>그 문으로 뻗는 확장 후보 자리(문을 가진 방 기준). 입구 바깥문은 자리가 없다(zero).</summary>
        public static Vector2Int CandidateOffset(bool fromEntrance, RoomDoors side)
        {
            if (!fromEntrance)
                return ToDirection(side);

            return side switch
            {
                RoomDoors.Up => new Vector2Int(-1, 1),
                RoomDoors.Down => new Vector2Int(-1, -1),
                RoomDoors.Left => Vector2Int.left,
                _ => Vector2Int.zero
            };
        }

        /// <summary>그 문으로 뻗은 후보가 반드시 가져야 하는 문. 입구의 ㄱ자 복도는 후보의 오른쪽 문에 닿는다.</summary>
        public static RoomDoors RequiredDoor(bool fromEntrance, RoomDoors side) =>
            fromEntrance && (side == RoomDoors.Up || side == RoomDoors.Down) ? RoomDoors.Right : Opposite(side);

        /// <summary>
        /// <paramref name="self"/>에서 <paramref name="other"/>로 가는 복도가 나가는 문.
        /// 맞닿은 방이면 그 방향, 입구와 왼쪽 위·아래 대각선 방이면 ㄱ자 복도의 문, 아니면 None.
        /// </summary>
        public static RoomDoors SideToward(Vector2Int self, bool selfIsEntrance, Vector2Int other, bool otherIsEntrance)
        {
            var delta = other - self;
            var orthogonal = FromDirection(delta);
            if (orthogonal != RoomDoors.None)
                return orthogonal;

            if (selfIsEntrance && delta.x == -1 && Mathf.Abs(delta.y) == 1)
                return delta.y > 0 ? RoomDoors.Up : RoomDoors.Down;

            if (otherIsEntrance && delta.x == 1 && Mathf.Abs(delta.y) == 1)
                return RoomDoors.Right;

            return RoomDoors.None;
        }

        /// <summary>문을 화살표로 적는다. 예: "문 ↑→".</summary>
        public static string Label(RoomDoors doors)
        {
            var builder = new StringBuilder("문 ");
            foreach (var side in Sides)
            {
                if (Has(doors, side))
                    builder.Append(RoomFeatureRules.Arrow(ToDirection(side)));
            }

            return builder.ToString();
        }
    }
}
