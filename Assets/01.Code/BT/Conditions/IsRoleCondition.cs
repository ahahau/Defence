using System;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    /// <summary>에이전트가 지정한 역할인지. 전열/후열/지원 그래프 분기에 사용.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Is Role",
        category: "Conditions/Battle",
        story: "[Agent] is [Role]",
        id: "c8f2d5b3e4015f9bad2c3e4f5061b722")]
    public partial class IsRoleCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("비교할 역할.")]
        [SerializeReference] public BlackboardVariable<BattleRole> Role = new(BattleRole.Tank);

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            if (a == null) return false;

            var role = Role != null ? Role.Value : BattleRole.Tank;
            return a.Role == role;
        }
    }
}
