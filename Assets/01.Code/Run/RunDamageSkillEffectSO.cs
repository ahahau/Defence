using UnityEngine;

namespace Code.Run
{
    /// <summary>시전자 공격 거리 안의 상대에게 피해를 준다. 대상은 가까운 순으로 고른다.</summary>
    [CreateAssetMenu(menuName = "SO/Run/Skill Effect/Damage", fileName = "RunDamageEffect")]
    public class RunDamageSkillEffectSO : RunTurnSkillEffectSO
    {
        [field: SerializeField, Min(0)] public int Amount { get; private set; } = 1;
        [field: SerializeField] public RunTargetRule Targets { get; private set; }

        [field: SerializeField, Min(1), Tooltip("AllInRange가 아니면 최대 몇 명을 칠지.")]
        public int MaxTargets { get; private set; } = 1;

        public override void Apply(RunBattle battle, RunCombatant caster)
        {
            foreach (RunCombatant target in battle.PickOpponents(caster, Targets, MaxTargets))
                battle.DealDamage(caster, target, Amount);
        }
    }
}
