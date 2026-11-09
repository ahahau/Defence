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
        private readonly List<RunCrisis> _events;

        public RunCrisisMonitor(RunBattle battle)
        {
            _battle = battle;
            int count = battle.Combatants.Count;
            _sequences = new int[count];
            _low = new bool[count];
            _stealing = new bool[count];
            _atHeart = new bool[count];
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
                bool low = actor.IsOnField && actor.Side == RunSide.Minion && actor.Health < actor.MaxHealth * _battle.Settings.RetreatHealthRatio;
                if (low && !_low[i]) _events.Add(new RunCrisis(RunCrisisKind.LowHealth, i, current));
                _low[i] = low;
                bool heart = actor.Side == RunSide.Intruder && actor.HasEntered && current == _battle.HeartRoom;
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

        /// <summary>부하의 위험 여부는 예고 경로와 고정 대상으로 판정한다.</summary>
        public bool IsEndangered(RunCombatant minion, RunCrisis crisis)
        {
            if (!minion.IsOnField || minion.Side != RunSide.Minion) return false;
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
