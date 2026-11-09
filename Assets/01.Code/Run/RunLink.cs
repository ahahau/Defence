namespace Code.Run
{
    /// <summary>같은 층에서 맞닿은 방 사이 동서 또는 남북 복도.</summary>
    public class RunLink
    {
        public RunLink(int index, int from, int to, bool isVertical, float length, float fromOffset, float toOffset)
        {
            Index = index;
            From = from;
            To = to;
            IsVertical = isVertical;
            Length = length;
            FromOffset = fromOffset;
            ToOffset = toOffset;
        }

        public int Index { get; }
        public int From { get; }
        public int To { get; }
        public bool IsVertical { get; }
        public RunAxis Axis => IsVertical ? RunAxis.Vertical : RunAxis.Horizontal;
        public float Length { get; }
        public float FromOffset { get; }
        public float ToOffset { get; }
    }
}
