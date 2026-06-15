using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>제자리 대기. 공격/이동을 멈추고 가만히 있는다(Selector의 기본 폴백).</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Idle",
        description: "Stands still — stops attacking and does not move.",
        story: "[Agent] idles",
        category: "Action/Battle",
        id: "28340cba47a745c1b7aab2039b9c5de6")]
    public partial class IdleAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        private BattleAgent _agent;

        protected override Status OnStart()
        {
            _agent = Resolve();
            _agent?.StopAttack();
            return Status.Running;
        }

        protected override Status OnUpdate() => Status.Success;

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
