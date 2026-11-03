using Code.Buildings;
using Code.Skills;
using Code.Stages;
using GameLib.Entity.Stats;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>후보 기준값은 행 연결 시 읽고, 진행 상태만 현재 프로필로 보충한다.</summary>
    internal static class StageOptionDetailsPresenter
    {
        internal static string BaseMessageFor(StageOption option, bool automaticUltimate = false)
        {
            if (option?.Unit != null)
            {
                StatModule stats = option.Unit.Prefab != null ? option.Unit.Prefab.GetComponent<StatModule>() : null;
                SkillCaster skills = option.Unit.Prefab != null ? option.Unit.Prefab.GetComponent<SkillCaster>() : null;
                return $"기본 체력 {StatText(stats, StatIndex.MaxHealth)} · 공격 {StatText(stats, StatIndex.AttackDamage)}\n"
                    + $"공격 간격 {StatText(stats, StatIndex.AttackInterval)}초 · 방어 {option.Unit.Defense} · 회피 {Mathf.RoundToInt(option.Unit.EvasionChance * 100f)}%\n"
                    + StageSkillDetailsPresenter.MessageFor(skills, automaticUltimate);
            }
            if (option?.WallTrap != null)
                return $"벽 함정 · 벽이나 바리케이드에 붙임 · 적만 피해\n{option.WallTrap.Describe()}\n준비 단계에 R로 방향 회전";
            if (option?.Trap?.Prefab is not Trap trap)
                return "기준 데이터 미등록";
            string effect = trap.TriggerMode switch
            {
                TrapTriggerMode.Area => $"범위 +{trap.AreaRadiusCells:0.#}칸 · 적·아군 피해",
                TrapTriggerMode.Persistent => $"지속 {trap.PersistentSeconds:0.#}초 · 적·아군 피해",
                TrapTriggerMode.Projectile => "같은 가로줄의 첫 적에게 피해",
                _ => "밟은 적 한 명에게 피해"
            };
            string status = trap.TriggerMode != TrapTriggerMode.Persistent && trap.StatusEffect != null && trap.InjuryChance > 0f
                ? $" · {trap.StatusEffect.DisplayName} {Mathf.RoundToInt(Mathf.Clamp01(trap.InjuryChance) * 100f)}%" : string.Empty;
            return $"기본 피해 {trap.Damage + trap.BonusDamage} · 발동 {Mathf.RoundToInt(Mathf.Clamp01(trap.TriggerChance) * 100f)}%\n"
                + $"차지 {trap.Footprint.x}×{trap.Footprint.y} · 재사용 {trap.CooldownSeconds:0.#}초\n{effect}{status}";
        }

        internal static string MessageFor(StageSession session, StageOption option, string baseline)
        {
            if (session?.Profile == null || session.Catalog == null || option == null)
                return string.Empty;
            int level = session.Profile.Level(option.Id);
            string unlock = session.Profile.IsUnlocked(option) ? "해금됨"
                : $"{session.Catalog.StageLabel(option.RequiredClear)} 클리어 시 해금";
            return $"구매 전 기준 · {option.Label} · {option.Cost}G · {unlock}\n{baseline}\n"
                + $"영구 강화 {level} · 피해 +{Mathf.RoundToInt(level * session.Catalog.DamagePerUpgrade * 100f)}% 별도 적용";
        }

        private static string StatText(StatModule stats, int index) => stats != null && stats.TryGetBaseValue(index, out float value)
            ? value.ToString("0.##") : "미등록";
    }
}
