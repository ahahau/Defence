using System;
using UnityEngine;

namespace Code.Run
{
    /// <summary>층 안 격자 칸. 같은 층에서 맞닿은 칸만 연결되며 층 간 이동은 없다.</summary>
    [Serializable]
    public struct RunRoomLayout
    {
        [field: SerializeField] public RunRoomKind Kind { get; private set; }
        [field: SerializeField, Min(0)] public int Floor { get; private set; }
        [field: SerializeField, Min(0)] public int Row { get; private set; }
        [field: SerializeField, Min(0)] public int Column { get; private set; }
        [field: SerializeField] public RunAffinity TrapAffinity { get; private set; }

        public RunRoomLayout(RunRoomKind kind, int row, int column, int floor = 0,
            RunAffinity trapAffinity = RunAffinity.Physical)
        {
            Kind = kind;
            Floor = floor;
            Row = row;
            Column = column;
            TrapAffinity = trapAffinity;
        }
    }
}
