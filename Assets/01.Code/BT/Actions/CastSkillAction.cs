using System;
using _01.Code.Skills;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>준비된 스킬/궁극기를 시전한다(SkillCaster 필요). 시전 실패면 Failure(다음 분기로).</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Cast Skill",
        description: "Casts a ready skill or ultimate via SkillCaster. Fails if none ready.",
        story: "[Agent] casts a skill",
        category: "Action/Battle",
        id: "f8d3b5a1e7c94f20ba4d6c38e7921a53")]
    public partial class CastSkillAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        protected override Status OnStart()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            if (a == null) return Status.Failure;

            var caster = a.GetComponent<SkillCaster>();
            if (caster == null) return Status.Failure;

            return caster.TryCast() ? Status.Success : Status.Failure;
        }
    }
}
