namespace Code.Run
{
    public enum RunBattleOutcome
    {
        Ongoing = 0,

        /// <summary>침입자가 모두 쓰러지거나 빠졌다(30라운드 도주 포함).</summary>
        Victory = 1,

        /// <summary>심장 체력이 0이 됐다.</summary>
        Defeat = 2,
        Retreat = 3,
    }
}
