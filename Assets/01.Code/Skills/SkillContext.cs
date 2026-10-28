using System.Collections.Generic;
using Code.BT;
using Code.Combat;

namespace Code.Skills
{
    /// <summary>스킬 실행 컨텍스트. ArtifactEffectContext와 동일 역할 — 시전자/타깃/전투필드 조회를 제공한다.</summary>
    public class SkillContext
    {
        public SkillContext(BattleAgent caster, Combatant casterCombatant, BattleAgent target, ISkillField field = null)
        {
            Caster = caster;
            CasterCombatant = casterCombatant;
            Target = target;
            Field = field;
        }

        public BattleAgent Caster { get; }
        public Combatant CasterCombatant { get; }
        public BattleAgent Target { get; }
        /// <summary>스테이지가 넘긴 대상 조회. null이면 시전자의 방 전투필드를 쓴다.</summary>
        public ISkillField Field { get; }

        /// <summary>같은 전투필드의 적팀(살아있는).</summary>
        public IEnumerable<BattleAgent> EnemiesInField()
        {
            if (Caster == null) yield break;
            IEnumerable<BattleAgent> roster = Field != null
                ? Field.Opponents(Caster.Team, Caster.transform.position)
                : Caster.Battlefield != null ? Caster.Battlefield.Opponents(Caster.Team) : null;
            if (roster == null) yield break;
            foreach (var a in roster)
            {
                if (a != null && a.IsAlive)
                    yield return a;
            }
        }

        /// <summary>같은 전투필드의 아군(살아있는). includeSelf=false면 자신 제외.</summary>
        public IEnumerable<BattleAgent> AlliesInField(bool includeSelf = true)
        {
            if (Caster == null) yield break;
            IEnumerable<BattleAgent> roster = Field != null
                ? Field.Allies(Caster.Team, Caster.transform.position)
                : Caster.Battlefield != null ? Caster.Battlefield.Allies(Caster.Team) : null;
            if (roster == null) yield break;
            foreach (var a in roster)
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
