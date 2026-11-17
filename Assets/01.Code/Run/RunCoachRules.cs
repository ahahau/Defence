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

        /// <param name="revived">상인에서 살린 부하인가(부하 번호). 준비 화면 자리 안내에 쓴다. 없으면 null.</param>
        /// <param name="paused">전투가 멈춰 있는가. 멈춰 있으면 멈춤 안내를 고르지 않는다.</param>
        public static RunCoachHint Pick(RunBattle battle, bool preparing, int trapRoom, Predicate<RunCoachStep> done,
            Predicate<int> revived = null, bool paused = false)
        {
            // 원정에는 따로 할 일 안내가 있다.
            if (battle == null || battle.IsRaid || done == null) return default;
            bool hasTrap = trapRoom >= 0 && trapRoom < battle.Rooms.Count;
            if (preparing)
            {
                if (hasTrap && !done(RunCoachStep.PowerTrap) && battle.Rooms[trapRoom].Mana == 0)
                    return new RunCoachHint(RunCoachStep.PowerTrap, trapRoom, null);
                // 살린 부하는 예전 자리에 그대로 놓여 같은 자리에서 다시 쓰러지기 쉽다(2026-11-12 사용자 "준비 화면에서 자리 안내").
                if (revived != null && !done(RunCoachStep.MoveMinion))
                    foreach (RunCombatant minion in battle.Combatants)
                        if (minion.Side == RunSide.Minion && minion.IsActive && revived(minion.Id))
                            return new RunCoachHint(RunCoachStep.MoveMinion, -1, minion);
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
            // 멈춤 버튼 말고 다른 경로로 멈춰도(지금은 없다) 멈춘 화면에 "멈추고 명령"이 뜨지 않게 한다.
            if (!paused && !done(RunCoachStep.Pause) && battle.Elapsed < PauseHintSeconds)
                return new RunCoachHint(RunCoachStep.Pause, -1, null);
            return default;
        }

        /// <summary>
        /// 적이 닿았는데 지키는 부하가 없거나, 지키는 부하가 모두 체력 1/3 아래인 금고·샘·심장. 없으면 -1.
        /// 버티던 부하가 쓰러지는 동안에도 도움을 보내라고 알린다(2026-11-12 사용자 선택 "버티는 부하가 위험하면 안내").
        /// </summary>
        private static int UnguardedTarget(RunBattle battle)
        {
            foreach (RunCombatant intruder in battle.Combatants)
            {
                if (intruder.Side != RunSide.Intruder || !intruder.IsOnField) continue;
                int room = battle.RoomOf(intruder);
                if (room < 0) continue;
                RunRoomKind kind = battle.Rooms[room].Kind;
                if (kind != RunRoomKind.Vault && kind != RunRoomKind.HealingSpring && kind != RunRoomKind.Heart) continue;
                if (!HasSturdyDefender(battle, room)) return room;
            }
            return -1;
        }

        /// <summary>이동 명령 없이 그 방을 지키는 부하 중 체력이 1/3 이상인 부하가 있는가.</summary>
        private static bool HasSturdyDefender(RunBattle battle, int room)
        {
            foreach (RunCombatant minion in battle.Combatants)
            {
                if (minion.Side != RunSide.Minion || !minion.IsOnField || minion.HasMoveOrder || battle.RoomOf(minion) != room) continue;
                if (minion.Health * 3 >= minion.MaxHealth) return true;
            }
            return false;
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
