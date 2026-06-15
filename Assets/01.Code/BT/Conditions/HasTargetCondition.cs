using System;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Has Target",
        category: "Conditions/Battle",
        story: "[Agent] has a target",
        id: "232eedb10f044071a94c60c79763061a")]
    public partial class HasTargetCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            return a != null && a.HasTarget();
        }
    }
}
