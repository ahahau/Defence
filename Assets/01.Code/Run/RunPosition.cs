namespace Code.Run
{
    /// <summary>방 중심선·복도·입구 굴 안의 거리. 방은 서쪽/북쪽 끝, 복도는 From, 굴은 바깥 끝부터 잰다.</summary>
    public readonly struct RunPosition
    {
        private RunPosition(int room, int link, float offset, RunAxis axis)
        {
            Room = room;
            Link = link;
            Offset = offset;
            Axis = axis;
        }

        public int Room { get; }
        public int Link { get; }
        public float Offset { get; }
        public RunAxis Axis { get; }
        public bool IsInRoom => Room >= 0;
        public bool IsAtEntrance => Room < 0 && Link < 0;

        public static RunPosition InRoom(int room, float offset) => InRoom(room, RunAxis.Horizontal, offset);
        public static RunPosition InRoom(int room, RunAxis axis, float offset) => new(room, -1, offset, axis);
        public static RunPosition OnLink(int link, float offset) => new(-1, link, offset, RunAxis.Horizontal);
        public static RunPosition AtEntrance(float offset) => new(-1, -1, offset, RunAxis.Vertical);
    }
}
