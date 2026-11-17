namespace Code.Run
{
    /// <summary>
    /// 실시간 전투 한 판 동안의 방 상태. 값은 `RunBattle`만 바꾼다.
    /// 층 안의 행·열과 방 내부 길이를 가진다.
    /// </summary>
    public class RunRoom
    {
        public RunRoom(int index, RunRoomLayout layout, float length, int durability, int maxMana)
        {
            Index = index;
            Kind = layout.Kind;
            Floor = layout.Floor;
            Row = layout.Row;
            Column = layout.Column;
            TrapAffinity = layout.TrapAffinity;
            Length = length;
            MaxDurability = HasSystemFor(Kind) ? durability : 0;
            Durability = MaxDurability;
            MaxMana = maxMana;
        }

        public int Index { get; }
        public RunRoomKind Kind { get; }

        /// <summary>층 번호(0이 1층). 행과는 별개이며 층 사이 길은 아직 없다.</summary>
        public int Floor { get; }

        public int Row { get; }
        public int Column { get; }
        public RunAffinity TrapAffinity { get; }
        public float Center => Length * 0.5f;
        public float Length { get; }

        /// <summary>내구가 있어 고장 날 수 있는 방 시스템인가. 입구·심장·금고·계단방은 아니다.</summary>
        public bool HasSystem => MaxDurability > 0;

        public float Durability { get; private set; }
        public int MaxDurability { get; }

        /// <summary>내구가 0이 되면 고장 나고, 가득 찰 때까지 고쳐야 다시 작동한다.</summary>
        public bool IsBroken { get; private set; }

        public bool IsDamaged => HasSystem && Durability < MaxDurability;

        public int Mana { get; private set; }
        public int MaxMana { get; }

        /// <summary>함정이 다시 터질 때까지 남은 초.</summary>
        public float TrapCooldown { get; private set; }

        internal float SpringTimer { get; set; }

        public static bool HasSystemFor(RunRoomKind kind) =>
            kind != RunRoomKind.Entrance && kind != RunRoomKind.Heart && kind != RunRoomKind.Vault && kind != RunRoomKind.Stairs;

        internal void SetMana(int amount) => Mana = amount;

        internal void SetInitialCondition(float durability, bool broken)
        {
            Durability = durability;
            IsBroken = broken;
        }

        internal void Damage(int amount)
        {
            if (!HasSystem || amount <= 0) return;
            Durability = System.Math.Max(0f, Durability - amount);
            if (Durability <= 0f)
                IsBroken = true;
        }

        internal void RepairBy(float amount)
        {
            if (!HasSystem || amount <= 0f) return;
            Durability = System.Math.Min(MaxDurability, Durability + amount);
            if (Durability >= MaxDurability)
                IsBroken = false;
        }

        internal void SetTrapCooldown(float seconds) => TrapCooldown = System.Math.Max(0f, seconds);

        /// <summary>함정이 다 찬 뒤 더 모은 초. 단계 계산에 쓰고 발동하면 0으로 돌아간다.</summary>
        public float TrapStoredSeconds { get; private set; }

        internal void SetTrapStored(float seconds) => TrapStoredSeconds = System.Math.Max(0f, seconds);
    }
}
