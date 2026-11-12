namespace Code.Run
{
    /// <summary>첫 판 안내 종류. 이름이 해 봤는지 기록의 키가 되므로 바꾸지 않는다.</summary>
    public enum RunCoachStep
    {
        None = 0,
        PowerTrap = 1,
        FireTrap = 2,
        SendMinion = 3,
        Pause = 4,
        /// <summary>준비 화면: 상인에서 살린 부하를 끌어 자리를 옮긴다(2026-11-12 사용자 결정).</summary>
        MoveMinion = 5,
    }
}
