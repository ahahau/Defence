namespace Code.Run
{
    /// <summary>던전 방 종류. 직렬화되므로 새 값은 맨 끝에만 더한다.</summary>
    public enum RunRoomKind
    {
        /// <summary>침입자가 들어오는 자리. 방 시스템이 없다.</summary>
        Entrance = 0,
        Heart = 1,
        Vault = 2,
        Trap = 3,
        HealingSpring = 4,
        DoorControl = 5,
        MagicBarrier = 6,
        Watch = 7,
        Prison = 8,
        Teleporter = 9,

        /// <summary>층 사이를 잇는 길만 있는 방. 시스템·마력이 없고 층당 방 수에 세지 않는다.</summary>
        Stairs = 10,
    }
}
