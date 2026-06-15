using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>지원(Support): 사거리 내 최저 HP 아군을 회복한다. 대상이 없으면 Failure.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Support Heal",
        description: "Heals the lowest-health ally in range. Fails if no wounded ally.",
        story: "[Agent] heals an ally",
        category: "Action/Battle",
        id: "e0b4f7d5061327a1cf4e50617283094a")]
    public partial class SupportHealAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        private BattleAgent _agent;

        protected override Status OnStart()
        {
            _agent = Resolve();
            if (_agent == null || !_agent.IsAlive) return Status.Failure;
            return _agent.SupportPulse() ? Status.Success : Status.Failure;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
