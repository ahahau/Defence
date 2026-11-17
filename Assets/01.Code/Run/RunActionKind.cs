namespace Code.Run
{
    /// <summary>인물이 지금 하는 일. 화면 표시(정보 창·예고)에 쓴다.</summary>
    public enum RunActionKind
    {
        Idle = 0,
        Move = 1,
        Attack = 2,
        Skill = 3,
        AttackDoor = 4,
        AttackRoom = 5,
        Steal = 6,
        HitHeart = 7,
        Escape = 8,
        Repair = 9,
    }
}
