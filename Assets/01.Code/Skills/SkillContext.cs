using System.Collections.Generic;
using _01.Code.BT;
using _01.Code.Combat;

namespace _01.Code.Skills
{
    /// <summary>스킬 실행 컨텍스트. ArtifactEffectContext와 동일 역할 — 시전자/타깃/전투필드 조회를 제공한다.</summary>
    public class SkillContext
    {
        public SkillContext(BattleAgent caster, Combatant casterCombatant, BattleAgent target)
        {
            Caster = caster;
            CasterCombatant = casterCombatant;
            Target = target;
        }

        public BattleAgent Caster { get; }
        public Combatant CasterCombatant { get; }
        public BattleAgent Target { get; }

        /// <summary>같은 전투필드의 적팀(살아있는).</summary>
        public IEnumerable<BattleAgent> EnemiesInField()
        {
            var field = Caster != null ? Caster.Battlefield : null;
            if (field == null) yield break;
            foreach (var a in field.Opponents(Caster.Team))
            {
                if (a != null && a.IsAlive)
                    yield return a;
            }
        }

        /// <summary>같은 전투필드의 아군(살아있는). includeSelf=false면 자신 제외.</summary>
        public IEnumerable<BattleAgent> AlliesInField(bool includeSelf = true)
        {
            var field = Caster != null ? Caster.Battlefield : null;
            if (field == null) yield break;
            foreach (var a in field.Allies(Caster.Team))
            {
                if (a == null || !a.IsAlive)
                    continue;
                if (!includeSelf && a == Caster)
                    continue;
                yield return a;
            }
        }
    }
}
