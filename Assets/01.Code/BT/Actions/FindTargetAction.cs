using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Find Target",
        description: "Finds the nearest enemy battle agent in sense range.",
        story: "[Agent] finds nearest enemy",
        category: "Action/Battle",
        id: "1fc23821ccf7489689e8cfffb3b82b96")]
    public partial class FindTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("타깃 우선순위: 최근접 / 최저체력 / 후열.")]
        [SerializeReference] public BlackboardVariable<TargetPriority> Priority = new(TargetPriority.Nearest);

        private BattleAgent _agent;

        protected override Status OnStart()
        {
            _agent = Resolve();
            return _agent == null ? Status.Failure : Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null) return Status.Failure;
            var priority = Priority != null ? Priority.Value : TargetPriority.Nearest;
            if (_agent.FindTarget(priority) == null) return Status.Failure;

            // 집중공격: 확정한 타깃을 팀 공유 포커스로 등록해 아군이 합류하게 한다.
            _agent.RegisterFocus();
            return Status.Success;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
