using Code.Buildings;
using Code.Stages;
using GameLib.Entity.Stats;
using UnityEngine;

namespace Code.UI.Stages
{
    /// <summary>
    /// 편성 창 상세 칸의 짧은 설명. 핵심 수치 두 줄과 강화·지원이 있을 때만 한 줄을 더한다
    /// (2026-11-04 사용자 "글자가 너무 많아"). 긴 설명은 편성 줄·함정 메뉴가 그대로 쓴다.
    /// </summary>
    internal static class StageLoadoutDetailPresenter
    {
        internal static string BaseFor(StageOption option)
        {
            if (option?.Unit != null)
            {
                StatModule stats = option.Unit.Prefab != null ? option.Unit.Prefab.GetComponent<StatModule>() : null;
                return $"체력 {Stat(stats, StatIndex.MaxHealth)} · 공격 {Stat(stats, StatIndex.AttackDamage)}\n"
                    + $"간격 {Stat(stats, StatIndex.AttackInterval)}초 · 방어 {option.Unit.Defense}";
            }
            if (option?.WallTrap != null)
                return option.WallTrap.Describe() + "\n벽·바리케이드에 붙임 · R 회전";
            if (option?.Trap?.Prefab is not Trap trap)
                return string.Empty;
            string effect = trap.TriggerMode switch
            {
                TrapTriggerMode.Area => $"범위 +{trap.AreaRadiusCells:0.#}칸",
                TrapTriggerMode.Persistent => $"지속 {trap.PersistentSeconds:0.#}초",
                TrapTriggerMode.Projectile => "같은 줄 첫 적",
                _ => "밟은 적"
            };
            string status = trap.TriggerMode != TrapTriggerMode.Persistent && trap.StatusEffect != null && trap.InjuryChance > 0f
                ? $" · {trap.StatusEffect.DisplayName}" : string.Empty;
            return $"피해 {trap.Damage + trap.BonusDamage} · 재사용 {trap.CooldownSeconds:0.#}초\n{effect}{status}";
        }

        /// <summary>영구 강화가 있을 때만 한 줄. 없으면 빈 글.</summary>
        internal static string ProgressFor(StageSession session, StageOption option)
        {
            if (session?.Profile == null || session.Catalog == null || option == null)
                return string.Empty;
            int level = session.Profile.Level(option.Id);
            if (level <= 0)
                return string.Empty;
            float damage = 1f + level * session.Catalog.DamagePerUpgrade;
            return $"강화 {level} · 피해 ×{damage:0.##}";
        }

        private static string Stat(StatModule stats, int index) => stats != null && stats.TryGetBaseValue(index, out float value)
            ? value.ToString("0.##") : "-";
    }
}
