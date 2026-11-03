using Code.Buildings;
using Code.Stages;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>준비 중 포인터 아래의 배치 대상 수치와 고정 환경 장치 설정을 읽는다.</summary>
    internal static class StageInspectionPresenter
    {
        internal static string MessageFor(StageSession session, Vector2Int cell)
        {
            if (session == null || session.Phase != StagePhase.Preparing || session.Board == null
                || session.Definition == null || session.Placement == null)
            {
                return string.Empty;
            }
            foreach (StageActor actor in session.Board.Actors)
            {
                if (actor == null || actor.IsEnemy || actor.Combat == null || actor.Combat.Health == null
                    || session.Board.Cell(actor.transform.position) != cell)
                {
                    continue;
                }
                return $"부하 · {actor.Option?.Label ?? "부하"}\n"
                    + $"체력 {actor.Combat.Health.CurrentHealth}/{actor.Combat.Health.MaxHealth} · 공격 {actor.Combat.AttackDamage}\n"
                    + $"공격 간격 {actor.Combat.AttackInterval:0.##}초 · 사거리 {actor.Combat.StrikeRange / session.Definition.CellSize:0.#}칸\n"
                    + $"명령 {StageUnitCommandRules.Label(actor.Command)}"
                    + (!actor.IsAlive ? " · 복구 대기" : string.Empty)
                    + "\n" + StageSkillDetailsPresenter.MessageFor(actor.Skills, session.Catalog.UsesTrapControls);
            }
            if (session.Board.Barricades.TryGetValue(cell, out StageBarricade wall) && wall != null && wall.IsAlive)
            {
                bool full = wall.Combat.Health.CurrentHealth >= wall.Combat.Health.MaxHealth;
                string recovery = full ? "회복 완료"
                    : session.Definition.RecoveryPerSecond <= 0f ? "자동 회복 없음"
                    : wall.RecoveryRemaining > 0f ? $"회복까지 {wall.RecoveryRemaining:0.0}초"
                    : $"회복 중 · 초당 {session.Definition.RecoveryPerSecond:0.##}";
                int refund = Mathf.FloorToInt(wall.PaidGold * Mathf.Clamp01(session.Definition.DemolishRefund));
                return $"바리케이드\n체력 {wall.Combat.Health.CurrentHealth}/{wall.Combat.Health.MaxHealth}\n"
                    + $"{recovery}\n설치비 {wall.PaidGold}G · 철거 환급 {refund}G";
            }
            Trap trap = session.Placement.TrapAt(cell);
            return trap != null ? TrapMessage(trap) : DeviceMessage(session.Definition.DeviceAt(cell));
        }

        private static string DeviceMessage(StageDevice device)
        {
            if (device == null)
                return string.Empty;
            // 주기형 시계는 -StartDelay에서 시작하고 주기의 마지막 ActiveSeconds 동안 켜진다.
            float firstActivation = device.StartDelay + device.Period - device.ActiveSeconds;
            return device.Kind switch
            {
                StageDeviceKind.FireVent => "환경 장치 · 불 구멍\n"
                    + $"{device.Period:0.##}초 주기 · {device.ActiveSeconds:0.##}초 분출 · 첫 분출은 전투 {firstActivation:0.##}초 후\n"
                    + $"분출 중 {device.TickInterval:0.##}초마다 피해 {device.Damage}\n"
                    + "중심 반경 0.5칸 · 적·아군 모두 피해",
                StageDeviceKind.Gate => "환경 장치 · 여닫는 문\n"
                    + $"{device.Period:0.##}초 주기 · {device.ActiveSeconds:0.##}초 닫힘\n"
                    + $"첫 닫힘은 전투 {firstActivation:0.##}초 후 · 닫히면 통행 차단\n"
                    + "칸이 점유되면 비킬 때까지 닫힘 대기",
                StageDeviceKind.Rockfall => "환경 장치 · 낙석\n전투 중 장치 칸 클릭\n"
                    + $"반경 {device.Radius:0.##}칸 · 적에게 피해 {device.Damage}\n"
                    + $"재사용 {device.Cooldown:0.##}초 · 아군 피해 없음",
                StageDeviceKind.Push => "수동 장치 · 밀치기\n전투 중 금색 화살표 칸 클릭\n"
                    + $"반경 {device.Radius:0.#}칸 적을 화살표 방향으로 최대 {device.PushCells}칸 이동\n"
                    + $"재사용 {device.Cooldown:0.#}초 · 벽·차단물 앞에서 멈춤",
                StageDeviceKind.Hazard => "위험 구역 · 주황 바닥\n통행 가능 · 설치 불가\n"
                    + $"{device.TickInterval:0.##}초마다 적·아군 피해 {device.Damage}\n밀치기로 적을 안에 머물게 하세요",
                StageDeviceKind.Pit => "구덩이 · 검은 바닥\n걸어서 지나가지 않음 · 설치 불가\n"
                    + "밀려 들어간 적은 떨어져 처치(금화 지급)\n보스는 가장자리에서 멈춤",
                _ => string.Empty
            };
        }

        private static string TrapMessage(Trap trap)
        {
            trap.CaptureStageTiming(out float cooldown, out float active, out _, out _);
            string mode = trap.TriggerMode switch
            {
                TrapTriggerMode.Step => "밟기",
                TrapTriggerMode.Area => "범위",
                TrapTriggerMode.Persistent => "지속",
                TrapTriggerMode.Projectile => "직선 투사체",
                _ => "함정"
            };
            string state = active > 0f ? $"작동 {active:0.0}초 남음"
                : cooldown > 0f ? $"대기 {cooldown:0.0}초 남음" : "발동 준비";
            string effect = trap.TriggerMode switch
            {
                TrapTriggerMode.Area => $"범위 +{trap.AreaRadiusCells:0.#}칸 · 적·아군 피해",
                TrapTriggerMode.Persistent => $"지속 {trap.PersistentSeconds:0.#}초 · 적·아군 피해",
                TrapTriggerMode.Projectile => "같은 가로줄의 첫 적에게 피해",
                _ => "밟은 적 한 명에게 피해"
            };
            string status = trap.TriggerMode != TrapTriggerMode.Persistent && trap.StatusEffect != null && trap.InjuryChance > 0f
                ? $"\n부가 {trap.StatusEffect.DisplayName} {Mathf.RoundToInt(Mathf.Clamp01(trap.InjuryChance) * 100f)}%" : string.Empty;
            string label = trap.Data != null ? trap.Data.DisplayName : "함정";
            return $"함정 · {label}\n"
                + $"내구도 {trap.CurrentDurability}/{trap.MaxDurability} · {mode} {trap.Footprint.x}×{trap.Footprint.y}\n"
                + $"재사용 {trap.CooldownSeconds:0.#}초 · {state}\n{effect}{status}";
        }
    }
}
