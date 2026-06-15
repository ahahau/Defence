using System;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    /// <summary>공격 쿨이 다 차서 지금 때릴 수 있는지. False면(공속 부족) 추격/재배치 분기로.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Attack Ready",
        category: "Conditions/Battle",
        story: "[Agent] attack is ready",
        id: "a1b2c3d4e5f60718293a4b5c6d7e8f90")]
    public partial class AttackReadyCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            return a != null && a.AttackReady;
        }
    }
}
