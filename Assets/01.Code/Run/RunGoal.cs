namespace Code.Run
{
    /// <summary>침입자가 향하는 목표. 직렬화되므로 값을 중간에 끼우지 않는다.</summary>
    public enum RunGoal
    {
        Heart = 0,
        Vault = 1,

        /// <summary><see cref="RunCombatantSO.GoalRoomKind"/> 방을 고장 낼 때까지 치고, 고장 나면 심장으로 간다.</summary>
        Room = 2,
    }
}
