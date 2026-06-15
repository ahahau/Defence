using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>찾기→(원거리면 거리유지)→사거리면 공격, 아니면 접근 — 전투 전체를 노드 하나로.
    /// 적이 없으면 Failure(다음 분기 Idle/Traverse로). Sequence/Selector 배선 없이 이거 하나면 교전이 된다.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Engage Target",
        description: "Finds, approaches, and attacks the best target. One node = full combat. Fails when no enemy.",
        story: "[Agent] engages the enemy",
        category: "Action/Battle",
        id: "b3e9c1d72f5a4c08a9e6d4b15c270e84")]
    public partial class EngageTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("타깃 우선순위: Nearest/Focused/Frontline/Backline/LowestHealth.")]
        [SerializeReference] public BlackboardVariable<TargetPriority> Priority = new(TargetPriority.Nearest);

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
                return Status.Failure; // 적 없음 → 다음 분기(Idle/Traverse)로

            _agent.RegisterFocus();
            var dt = Time.deltaTime;

            // 원거리가 너무 가까우면 물러나며 쏨(근접/탱커는 false라 통과)
            if (_agent.MaintainRange(dt))
                return Status.Running;

            if (!_agent.TargetInRange())
                _agent.MoveToTarget(dt); // 사거리 밖 → 붙는다
            else
                _agent.Attack(); // 사거리 안 → 공격(쿨 도는 동안엔 공격 루프가 알아서 대기 = 제자리 대기)

            return Status.Running;
        }

        protected override void OnEnd() => _agent?.StopAttack();

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
