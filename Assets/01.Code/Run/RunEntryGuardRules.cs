namespace Code.Run
{
    /// <summary>
    /// 적이 처음 들어오는 방을 부하가 지키는지 본다. 부하가 있는 방은 적이 지나가지 못하므로, 이 방이 비면
    /// 적이 함정 방에 묶이지 않고 안쪽으로 들어온다(2026-11-17 사용자 목업 승인 "A + B").
    /// </summary>
    public static class RunEntryGuardRules
    {
        /// <summary>그 방에 머무는 부하나 그 방으로 가는 중인 부하가 있는가.</summary>
        public static bool IsHeld(RunBattle battle, int room)
        {
            if (battle == null || room < 0) return false;
            foreach (RunCombatant minion in battle.Combatants)
            {
                if (minion.Side != RunSide.Minion || !minion.IsActive) continue;
                if (minion.HasMoveOrder ? minion.MoveTarget == room : battle.RoomOf(minion) == room) return true;
            }
            return false;
        }

        /// <summary>준비 중에는 평면도가 배치대로 그리고 전투 인물은 움직이지 않으므로 배치에서 본다.</summary>
        public static bool IsHeld(RunDeployment deployment, RunBattle battle, int room)
        {
            if (deployment == null || battle == null || room < 0) return false;
            for (int i = 0; i < deployment.MinionCount && i < battle.Combatants.Count; i++)
            {
                RunCombatant minion = battle.Combatants[i];
                if (minion.Side == RunSide.Minion && minion.IsActive && deployment.RoomOf(i) == room) return true;
            }
            return false;
        }
    }
}
