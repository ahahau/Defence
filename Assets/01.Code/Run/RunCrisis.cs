namespace Code.Run
{
    /// <summary>감지한 순간의 식별 정보. 재사용 예고 객체를 보관하지 않는다.</summary>
    public readonly struct RunCrisis
    {
        public RunCrisis(RunCrisisKind kind, int actor, int room, int sequence = 0)
        {
            Kind = kind;
            Actor = actor;
            Room = room;
            Sequence = sequence;
        }

        public RunCrisisKind Kind { get; }
        public int Actor { get; }
        public int Room { get; }
        public int Sequence { get; }
        public bool Pauses => Kind <= RunCrisisKind.ThiefAtVault;
    }
}
