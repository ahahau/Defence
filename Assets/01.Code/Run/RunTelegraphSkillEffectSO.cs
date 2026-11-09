using UnityEngine;

namespace Code.Run
{
    /// <summary>기술의 예고 시간·피해 데이터. 발동까지의 상태와 판정은 순수 규칙이 맡는다.</summary>
    [CreateAssetMenu(menuName = "SO/Run/Skill Effect/Telegraph", fileName = "RunTelegraphEffect")]
    public class RunTelegraphSkillEffectSO : RunTurnSkillEffectSO
    {
        [field: SerializeField] public RunTelegraphKind Kind { get; private set; }
        [field: SerializeField, Min(0.05f)] public float WarningSeconds { get; private set; }
        [field: SerializeField, Min(0)] public int Damage { get; private set; }
        [field: SerializeField, Min(0)] public int SystemDamage { get; private set; }

        public RunTelegraphScope Scope => Kind == RunTelegraphKind.Charge ? RunTelegraphScope.StraightLine
            : Kind == RunTelegraphKind.AimedShot ? RunTelegraphScope.Combatant : RunTelegraphScope.Room;

        public override void Apply(RunBattle battle, RunCombatant caster) => battle.TryBeginTelegraph(caster, this);
    }
}
