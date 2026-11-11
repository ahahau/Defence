namespace Code.Run
{
    /// <summary>방을 부하가 맡고 있는 정도. 맞는 방(<see cref="RunCombatantSO.FavoredRoom"/>)의 부하가 있으면 Favored다.</summary>
    public enum RunStaffing
    {
        None = 0,
        Staffed = 1,
        Favored = 2,
    }
}
