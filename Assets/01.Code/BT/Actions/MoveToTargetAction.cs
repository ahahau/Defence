using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Move To Target",
        description: "Moves toward the current target until in attack range.",
        story: "[Agent] moves to target",
        category: "Action/Battle",
        id: "a8da9c61f96a4977aef54bb03f09174a")]
    public partial class MoveToTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        private BattleAgent _agent;

        protected override Status OnStart()
        {
            _agent = Resolve();
            return _agent == null ? Status.Failure : Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked || _agent.CurrentTarget == null)
                return Status.Failure;
            if (_agent.TargetInRange()) return Status.Success;

            _agent.MoveToTarget(Time.deltaTime);
            return Status.Running;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
