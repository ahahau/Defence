using System;

namespace Code.Run
{
    /// <summary>
    /// 첫 판 안내 중 지금 띄울 하나를 전투 상태로 고른다(2026-11-12 사용자 목업 승인).
    /// 한 번 해 본 안내는 다시 고르지 않고, 여럿이면 함정 발동 → 부하 보내기 → 멈춤 순으로 앞의 것을 고른다.
    /// </summary>
    public static class RunCoachRules
    {
        /// <summary>전투 시작 뒤 멈춤 안내를 보이는 시간(초).</summary>
        public const float PauseHintSeconds = 8f;

        public static RunCoachHint Pick(RunBattle battle, bool preparing, int trapRoom, Predicate<RunCoachStep> done)
        {
            // 원정에는 따로 할 일 안내가 있다.
            if (battle == null || battle.IsRaid || done == null) return default;
            bool hasTrap = trapRoom >= 0 && trapRoom < battle.Rooms.Count;
            if (preparing)
            {
                if (hasTrap && !done(RunCoachStep.PowerTrap) && battle.Rooms[trapRoom].Mana == 0)
                    return new RunCoachHint(RunCoachStep.PowerTrap, trapRoom, null);
                return default;
            }
            if (battle.Outcome != RunBattleOutcome.Ongoing) return default;
            if (hasTrap && !done(RunCoachStep.FireTrap) && battle.CanActivateTrap(trapRoom))
                return new RunCoachHint(RunCoachStep.FireTrap, trapRoom, null);
            if (!done(RunCoachStep.SendMinion))
            {
                int room = UnguardedTarget(battle);
                RunCombatant minion = room >= 0 ? NearestMinion(battle, room) : null;
                if (minion != null) return new RunCoachHint(RunCoachStep.SendMinion, room, minion);
            }
            if (!done(RunCoachStep.Pause) && battle.Elapsed < PauseHintSeconds)
                return new RunCoachHint(RunCoachStep.Pause, -1, null);
            return default;
        }

        /// <summary>적이 닿았는데 지키는 부하가 없는 금고·샘·심장. 없으면 -1.</summary>
        private static int UnguardedTarget(RunBattle battle)
        {
            foreach (RunCombatant intruder in battle.Combatants)
            {
                if (intruder.Side != RunSide.Intruder || !intruder.IsOnField) continue;
                int room = battle.RoomOf(intruder);
                if (room < 0) continue;
                RunRoomKind kind = battle.Rooms[room].Kind;
                if (kind != RunRoomKind.Vault && kind != RunRoomKind.HealingSpring && kind != RunRoomKind.Heart) continue;
                if (!battle.IsDefended(room)) return room;
            }
            return -1;
        }

        private static RunCombatant NearestMinion(RunBattle battle, int room)
        {
            RunCombatant best = null;
            float bestDistance = float.PositiveInfinity;
            RunPosition center = battle.CenterOf(room);
            foreach (RunCombatant minion in battle.Combatants)
            {
                if (minion.Side != RunSide.Minion || !minion.IsOnField || battle.RoomOf(minion) == room) continue;
                float distance = battle.Distance(minion.Position, center);
                if (distance >= bestDistance) continue;
                best = minion;
                bestDistance = distance;
            }
            return best;
        }
    }
}
