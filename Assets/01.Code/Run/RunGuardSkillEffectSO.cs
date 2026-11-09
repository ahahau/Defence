using UnityEngine;

namespace Code.Run
{
    /// <summary>시전자 둘레(지원 거리)의 아군(자신 포함)이 다음에 받는 피해를 줄인다.</summary>
    [CreateAssetMenu(menuName = "SO/Run/Skill Effect/Guard", fileName = "RunGuardEffect")]
    public class RunGuardSkillEffectSO : RunTurnSkillEffectSO
    {
        [field: SerializeField, Min(0)] public int Reduction { get; private set; } = 1;

        public override void Apply(RunBattle battle, RunCombatant caster)
        {
            foreach (RunCombatant ally in battle.AlliesNear(caster))
                ally.AddGuard(Reduction);
        }
    }
}
