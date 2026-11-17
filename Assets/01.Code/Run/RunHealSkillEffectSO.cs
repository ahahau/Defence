using UnityEngine;

namespace Code.Run
{
    /// <summary>시전자 둘레(지원 거리)에서 체력이 가장 낮은 아군(자신 포함)을 회복한다.</summary>
    [CreateAssetMenu(menuName = "SO/Run/Skill Effect/Heal", fileName = "RunHealEffect")]
    public class RunHealSkillEffectSO : RunTurnSkillEffectSO
    {
        [field: SerializeField, Min(0)] public int Amount { get; private set; } = 1;

        public override void Apply(RunBattle battle, RunCombatant caster)
        {
            RunCombatant target = battle.LowestHealthAlly(caster);
            if (target != null)
                target.Heal(Amount);
        }
    }
}
