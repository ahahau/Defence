using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Attack Target",
        description: "Engages the current target while it is alive and in range.",
        story: "[Agent] attacks target",
        category: "Action/Battle",
        id: "8d396afafaab4fb3aca6c05899c0b8ab")]
    public partial class AttackTargetAction : Action
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
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked) return Status.Failure;
            if (_agent.CurrentTarget == null) return Status.Failure;     // 타깃 없음 → 다음 분기로
            if (!_agent.TargetInRange()) return Status.Failure;          // 사거리 밖 → 이동 노드로

            _agent.Attack();
            return Status.Running;
        }

        protected override void OnEnd() => _agent?.StopAttack();

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
