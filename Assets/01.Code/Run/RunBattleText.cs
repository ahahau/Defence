namespace Code.Run
{
    /// <summary>던전 FTL 전투 화면에 쓰는 한국어 문구. 판단은 하지 않고 이름·하는 일을 글로 옮긴다.</summary>
    public static class RunBattleText
    {
        public static string RoomName(RunRoomKind kind) => kind switch
        {
            RunRoomKind.Entrance => "입구",
            RunRoomKind.Heart => "심장",
            RunRoomKind.Vault => "금고",
            RunRoomKind.Trap => "함정 방",
            RunRoomKind.HealingSpring => "치유 샘",
            RunRoomKind.DoorControl => "문 제어실",
            RunRoomKind.MagicBarrier => "마법 방벽",
            RunRoomKind.Watch => "감시실",
            RunRoomKind.Prison => "굴 감옥",
            RunRoomKind.Teleporter => "전송 방",
            RunRoomKind.Stairs => "계단방",
            _ => kind.ToString(),
        };

        /// <summary>정보 창에 쓰는 지금 하는 일 한 줄.</summary>
        public static string Activity(RunBattle battle, RunCombatant combatant)
        {
            if (combatant == null) return string.Empty;
            if (!combatant.HasEntered && combatant.IsActive) return "입구 밖에서 기다림";
            return combatant.Activity switch
            {
                RunActionKind.Move when combatant.IsRetreating => $"{Toward(Where(battle, combatant.MoveTarget))} 물러나는 중",
                RunActionKind.Move when combatant.HasMoveOrder => $"{Toward(Where(battle, combatant.MoveTarget))} 가는 중",
                RunActionKind.Move when combatant.Side == RunSide.Minion && combatant.Target != null =>
                    $"{combatant.Target.Definition.DisplayName}에게 접근 중",
                RunActionKind.Move when combatant.Side == RunSide.Minion =>
                    $"{Toward(Where(battle, combatant.HomeRoom))} 복귀 중",
                RunActionKind.Move => $"{Toward(Where(battle, battle.GoalRoomOf(combatant)))} 가는 중",
                RunActionKind.Attack or RunActionKind.Skill when combatant.Target != null =>
                    $"{With(combatant.Target.Definition.DisplayName)} 교전 중",
                RunActionKind.AttackDoor => "잠긴 문을 부수는 중",
                RunActionKind.AttackRoom => "방 시스템을 부수는 중",
                RunActionKind.Steal => $"금화를 훔치는 중 ({combatant.CarriedGold})",
                RunActionKind.Escape => $"금화 {combatant.CarriedGold} 들고 도망치는 중",
                RunActionKind.Repair => "방을 고치는 중",
                _ when combatant.IsRetreating => "치유 샘에서 회복 중",
                _ => "대기",
            };
        }

        public static string Where(RunBattle battle, int room) =>
            room >= 0 && room < battle.Rooms.Count ? RoomName(battle.Rooms[room].Kind) : "복도";

        /// <summary>"샘으로"·"금고로"처럼 받침에 맞춰 방향 조사를 붙인다(ㄹ 받침은 "로").</summary>
        public static string Toward(string word)
        {
            int final = FinalConsonant(word);
            return word + (final > 0 && final != 8 ? "으로" : "로");
        }

        /// <summary>"검사와"·"도적과"처럼 받침에 맞춰 조사를 붙인다.</summary>
        public static string With(string word) => word + (FinalConsonant(word) > 0 ? "과" : "와");

        /// <summary>마지막 글자의 받침 번호(없으면 0, 한글이 아니면 0).</summary>
        private static int FinalConsonant(string word)
        {
            if (string.IsNullOrEmpty(word)) return 0;
            char last = word[word.Length - 1];
            if (last < '가' || last > '힣') return 0;
            return (last - '가') % 28;
        }

        public static string SkillName(RunCombatant combatant) =>
            combatant.Definition.Skill != null ? combatant.Definition.Skill.DisplayName : "스킬";
    }
}
