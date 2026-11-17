using System.Collections.Generic;

namespace Code.Run
{
    /// <summary>진행 후 상태의 전이를 읽는다. 준비한 배열과 목록을 재사용해 감지 중 할당하지 않는다.</summary>
    public class RunCrisisMonitor
    {
        private readonly RunBattle _battle;
        private readonly int[] _sequences;
        private readonly bool[] _broken;
        private readonly bool[] _low;
        private readonly bool[] _stealing;
        private readonly bool[] _atHeart;
        private readonly int[] _alertedRoom;
        private readonly int[] _alertedDoor;
        private readonly List<RunCrisis> _events;

        public RunCrisisMonitor(RunBattle battle)
        {
            _battle = battle;
            int count = battle.Combatants.Count;
            _sequences = new int[count];
            _low = new bool[count];
            _stealing = new bool[count];
            _atHeart = new bool[count];
            _alertedRoom = new int[count];
            _alertedDoor = new int[count];
            for (int i = 0; i < count; i++) _alertedRoom[i] = _alertedDoor[i] = -1;
            _broken = new bool[battle.Rooms.Count];
            for (int i = 0; i < _broken.Length; i++) _broken[i] = battle.Rooms[i].IsBroken;
            _events = new List<RunCrisis>(count * 3 + battle.Rooms.Count);
        }

        public IReadOnlyList<RunCrisis> Events => _events;

        public void Scan()
        {
            _events.Clear();
            for (int i = 0; i < _battle.Combatants.Count; i++)
            {
                RunCombatant actor = _battle.Combatants[i];
                RunTelegraph warning = actor.Telegraph;
                if (warning.IsActive && warning.Sequence != _sequences[i])
                {
                    int room = warning.TargetRoom;
                    if (room < 0) room = _battle.RoomOf(actor);
                    _events.Add(new RunCrisis(RunCrisisKind.Telegraph, i, room, warning.Sequence));
                    _sequences[i] = warning.Sequence;
                }
                int current = _battle.RoomOf(actor);
                bool stealing = actor.IsOnField && actor.Definition.Goal == RunGoal.Vault && actor.Activity == RunActionKind.Steal;
                if (stealing && !_stealing[i]) _events.Add(new RunCrisis(RunCrisisKind.ThiefAtVault, i, _battle.VaultRoom));
                _stealing[i] = stealing;
                // 같은 적이 같은 방을 다시 칠 때(싸우다 돌아옴)는 다시 멈추지 않는다.
                bool hitting = actor.IsOnField && actor.Side == RunSide.Intruder && actor.Definition.Goal == RunGoal.Room
                    && actor.Activity == RunActionKind.AttackRoom;
                if (hitting && _alertedRoom[i] != current)
                {
                    _events.Add(new RunCrisis(RunCrisisKind.RoomUnderAttack, i, current));
                    _alertedRoom[i] = current;
                }
                // 샘·금고로 가는 잠긴 문을 치기 시작하면 멈춘다. 같은 적이 같은 문을 다시 칠 때는 다시 멈추지 않는다.
                if (actor.IsOnField && actor.Side == RunSide.Intruder && actor.Activity == RunActionKind.AttackDoor
                    && !actor.Position.IsInRoom && !actor.Position.IsAtEntrance && _alertedDoor[i] != actor.Position.Link)
                {
                    int link = actor.Position.Link;
                    int behind = GuardedRoomBehind(link);
                    if (behind >= 0) _events.Add(new RunCrisis(RunCrisisKind.DoorUnderAttack, i, behind, link));
                    _alertedDoor[i] = link;
                }
                bool low = actor.IsOnField && actor.Side == RunSide.Minion && actor.Health < actor.MaxHealth * _battle.Settings.RetreatHealthRatio;
                if (low && !_low[i]) _events.Add(new RunCrisis(RunCrisisKind.LowHealth, i, current));
                _low[i] = low;
                // 원정 진지에는 지킬 심장이 없다(대장실이 심장 자리를 빌린다). 대장이 제 방에 있는 것을 위기로 읽지 않는다.
                bool heart = !_battle.IsRaid && actor.Side == RunSide.Intruder && actor.HasEntered && current == _battle.HeartRoom;
                if (heart && !_atHeart[i]) _events.Add(new RunCrisis(RunCrisisKind.HeartEntered, i, current));
                _atHeart[i] = heart;
            }
            for (int i = 0; i < _broken.Length; i++)
            {
                bool broken = _battle.Rooms[i].IsBroken;
                if (broken && !_broken[i]) _events.Add(new RunCrisis(RunCrisisKind.RoomBroken, -1, i));
                _broken[i] = broken;
            }
        }

        // 문 제어실이 잠그는 문 너머의 치유 샘·금고. 심장 문이나 다른 문이면 -1.
        private int GuardedRoomBehind(int link)
        {
            if (_battle.Doors[link].ControllerRoom < 0) return -1;
            RunLink corridor = _battle.Links[link];
            if (IsGuarded(corridor.From)) return corridor.From;
            return IsGuarded(corridor.To) ? corridor.To : -1;
        }

        private bool IsGuarded(int room) => _battle.Rooms[room].Kind is RunRoomKind.HealingSpring or RunRoomKind.Vault;

        /// <summary>부하의 위험 여부는 예고 경로와 고정 대상으로 판정한다.</summary>
        public bool IsEndangered(RunCombatant minion, RunCrisis crisis)
        {
            if (!minion.IsOnField || minion.Side != RunSide.Minion || crisis.Kind == RunCrisisKind.DoorUnderAttack) return false;
            if (crisis.Kind != RunCrisisKind.Telegraph) return _battle.RoomOf(minion) == crisis.Room;
            RunTelegraph warning = _battle.Combatants[crisis.Actor].Telegraph;
            if (warning.Scope == RunTelegraphScope.Combatant) return warning.Target == minion;
            if (warning.Scope == RunTelegraphScope.Room) return _battle.RoomOf(minion) == warning.TargetRoom;
            for (int i = 0; i < warning.PathCount; i++)
            {
                RunPosition a = warning.PathStart(i);
                RunPosition z = warning.PathEnd(i);
                float distance = _battle.Distance(a, minion.Position) + _battle.Distance(minion.Position, z);
                if (distance <= _battle.Distance(a, z) + 0.001f) return true;
            }
            return false;
        }
    }
}
