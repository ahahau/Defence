using System;

namespace Code.Run
{
    /// <summary>전투 전 부하 자리와 마력. 방에 놓기만 하며 시간이나 전투 상태를 바꾸지 않는다.</summary>
    public class RunDeployment
    {
        private readonly RunEncounterSO _encounter;
        private RunPlacement[] _roster;
        private int[] _rooms;
        private readonly int[] _mana;

        public RunDeployment(RunEncounterSO encounter)
        {
            _encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
            Capacity = encounter.Settings.StartingManaCapacity;
            _roster = encounter.Minions;
            _rooms = new int[_roster.Length];
            _mana = new int[encounter.GridLayout.Length];
            for (int i = 0; i < _rooms.Length; i++) _rooms[i] = _roster[i].Room;
        }

        public RunEncounterSO Encounter => _encounter;
        public int MinionCount => _rooms.Length;
        public int RoomCount => _mana.Length;
        public int Capacity { get; internal set; }
        public int ManaInUse
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _mana.Length; i++) total += _mana[i];
                return total;
            }
        }

        /// <summary>이번 런의 부하 목록. 시연 런은 새 부하·강화한 변형으로 에셋 목록과 다를 수 있다.</summary>
        internal RunPlacement[] Roster => _roster;

        public RunPlacement MinionAt(int minion) => _roster[minion];
        public int RoomOf(int minion) => _rooms[minion];
        public int ManaAt(int room) => _mana[room];

        /// <summary>부하 목록을 바꾼다. 있던 부하의 자리는 두고, 새로 생긴 부하는 목록의 시작 방에 둔다.</summary>
        internal void SetRoster(RunPlacement[] roster)
        {
            if (roster == null || roster == _roster) return;
            int[] rooms = new int[roster.Length];
            for (int i = 0; i < rooms.Length; i++) rooms[i] = i < _rooms.Length ? _rooms[i] : roster[i].Room;
            _roster = roster;
            _rooms = rooms;
        }

        /// <summary>한 방에 여러 명을 둘 수 있다. 층 간 배치는 아직 지원하지 않는다.</summary>
        public bool Place(int minion, int room)
        {
            if (minion < 0 || minion >= _rooms.Length || room < 0 || room >= _mana.Length) return false;
            if (_encounter.GridLayout[room].Floor != _encounter.GridLayout[_rooms[minion]].Floor) return false;
            _rooms[minion] = room;
            return true;
        }

        public int MaxMana(int room) => _encounter.GridLayout[room].Kind switch
        {
            RunRoomKind.Trap => 2,
            RunRoomKind.HealingSpring or RunRoomKind.DoorControl => 1,
            _ => 0,
        };

        public bool SetMana(int room, int amount)
        {
            if (room < 0 || room >= _mana.Length || amount < 0 || amount > MaxMana(room)) return false;
            if (ManaInUse - _mana[room] + amount > Capacity) return false;
            _mana[room] = amount;
            return true;
        }

        public RunDeployment Copy()
        {
            RunDeployment copy = new(_encounter);
            copy.Capacity = Capacity;
            copy.SetRoster(_roster);
            copy.Load(this);
            return copy;
        }

        /// <summary>동일한 구성의 배치만 복원한다. 복사하므로 이후 조작이 기억한 배치를 바꾸지 않는다.
        /// 부하 수가 다르면(새 부하를 들인 뒤) 겹치는 앞쪽만 옮긴다.</summary>
        public bool Load(RunDeployment other)
        {
            if (other == null || other._encounter != _encounter) return false;
            Array.Copy(other._rooms, _rooms, Math.Min(_rooms.Length, other._rooms.Length));
            Array.Copy(other._mana, _mana, _mana.Length);
            return true;
        }
    }
}
