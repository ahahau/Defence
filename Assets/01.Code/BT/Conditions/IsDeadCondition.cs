using System;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Is Dead",
        category: "Conditions/Battle",
        story: "[Agent] is dead",
        id: "5b0b9bd0142445099fe2907700606546")]
    public partial class IsDeadCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            return a != null && !a.IsAlive;
        }
    }
}
