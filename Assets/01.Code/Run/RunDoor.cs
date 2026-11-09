namespace Code.Run
{
    /// <summary>연결 통로 가운데의 문. 문 제어실이 켜져 있으면 잠긴다.</summary>
    public class RunDoor
    {
        public RunDoor(int index, RunPosition position, int durability, int controllerRoom)
        {
            Index = index;
            Position = position;
            MaxDurability = durability;
            Durability = durability;
            ControllerRoom = controllerRoom;
        }

        public int Index { get; }

        public RunPosition Position { get; }

        public int Durability { get; private set; }
        public int MaxDurability { get; }

        /// <summary>이 문을 잠그는 문 제어실 방 번호. 없으면 -1.</summary>
        public int ControllerRoom { get; }

        /// <summary>적이 부숴 이번 전투 동안 열린 채다.</summary>
        public bool IsBrokenOpen { get; private set; }

        internal void Damage(int amount)
        {
            if (IsBrokenOpen || amount <= 0) return;
            Durability = System.Math.Max(0, Durability - amount);
            if (Durability == 0)
                IsBrokenOpen = true;
        }
    }
}
