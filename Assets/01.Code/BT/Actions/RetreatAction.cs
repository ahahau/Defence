using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>가장 가까운 적 반대 방향으로 노드 안에서 도망친다. 적이 없으면 Failure.
    /// Health Below 조건과 묶어 "저체력이면 도망" 같은 겁쟁이 행동에 쓴다.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Retreat",
        description: "Flees away from the nearest enemy, staying inside the node. Fails if no enemy.",
        story: "[Agent] retreats",
        category: "Action/Battle",
        id: "d5a1e3f94b7c4e20cbf8e6d37e492a06")]
    public partial class RetreatAction : Action
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
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;
            if (_agent.FindNearestEnemy() == null)
                return Status.Failure;

            _agent.RetreatFromTarget(Time.deltaTime);
            return Status.Running;
        }

        protected override void OnEnd() => _agent?.StopAttack();

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
