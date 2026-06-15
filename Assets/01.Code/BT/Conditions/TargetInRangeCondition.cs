using System;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Target In Range",
        category: "Conditions/Battle",
        story: "[Agent] target is in attack range",
        id: "05cb5377939f42db863975d5b68979e0")]
    public partial class TargetInRangeCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            return a != null && a.TargetInRange();
        }
    }
}
