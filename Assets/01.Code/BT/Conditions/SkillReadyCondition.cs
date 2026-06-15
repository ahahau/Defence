using System;
using _01.Code.Skills;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    /// <summary>사용 가능한 스킬/궁극기가 있는지(SkillCaster 필요).</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Skill Ready",
        category: "Conditions/Battle",
        story: "[Agent] has a skill ready",
        id: "e7c2a4f0d6b84e19af3c5b27d6810f42")]
    public partial class SkillReadyCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            if (a == null) return false;

            var caster = a.GetComponent<SkillCaster>();
            return caster != null && caster.HasReadySkill;
        }
    }
}
