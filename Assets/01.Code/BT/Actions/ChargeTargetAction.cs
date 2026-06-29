using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>타깃에게 빠르게 돌진해 거리를 좁힌다(근접 개시기). 사거리 안에 들면 Success(공격 분기로),
    /// 적이 없으면 Failure. 보통 Engage Target 앞에 두어 "달려들어 붙은 뒤 교전" 흐름을 만든다.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Charge Target",
        description: "Rushes toward the best target quickly. Succeeds when in range, fails when no enemy.",
        story: "[Agent] charges the target",
        category: "Action/Battle",
        id: "11223344556677889900aabbccddeeff")]
    public partial class ChargeTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("타깃 우선순위.")]
        [SerializeReference] public BlackboardVariable<TargetPriority> Priority = new(TargetPriority.Nearest);
        [Tooltip("돌진 속도 배수(이동 속도 × 이 값).")]
        [SerializeReference] public BlackboardVariable<float> SpeedMultiplier = new(1.8f);

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

            var priority = Priority != null ? Priority.Value : TargetPriority.Nearest;
            if (_agent.FindTarget(priority) == null)
                return Status.Failure;

            var mult = SpeedMultiplier != null ? SpeedMultiplier.Value : 1.8f;
            // 사거리에 들면 false → 교전(Engage)으로 넘긴다.
            return _agent.ChargeToTarget(Time.deltaTime, mult) ? Status.Running : Status.Success;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
