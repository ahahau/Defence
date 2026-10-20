using System;

namespace Code.MapCreateSystem
{
    /// <summary>방 프리팹의 문이 난 변. 문은 늘 그 변의 가운데에 있다.</summary>
    [Flags]
    public enum RoomDoors
    {
        None = 0,
        Up = 1,
        Right = 2,
        Down = 4,
        Left = 8
    }
}
