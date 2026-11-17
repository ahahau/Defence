namespace Code.Run
{
    /// <summary>지금 띄울 첫 판 안내 하나. 방과 부하는 안내에 필요할 때만 채운다.</summary>
    public readonly struct RunCoachHint
    {
        public RunCoachHint(RunCoachStep step, int room, RunCombatant minion)
        {
            Step = step;
            Room = room;
            Minion = minion;
        }

        public RunCoachStep Step { get; }
        public int Room { get; }
        public RunCombatant Minion { get; }
    }
}
