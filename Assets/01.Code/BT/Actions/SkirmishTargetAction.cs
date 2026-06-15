using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>치고 빠지기(hit &amp; run): 접근 → 잠깐 공격 → 노드 안에서 후퇴 → 반복.
    /// 붙어서 평타만 하는 Engage Target과 다른 전투 스타일. 적 없으면 Failure.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Skirmish Target",
        description: "Hit-and-run: approach, attack briefly, then retreat inside the node, repeat.",
        story: "[Agent] skirmishes the enemy",
        category: "Action/Battle",
        id: "c4f0d2e83a6b4d19baf7e5c26d381f95")]
    public partial class SkirmishTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("타깃 우선순위.")]
        [SerializeReference] public BlackboardVariable<TargetPriority> Priority = new(TargetPriority.Nearest);
        [Tooltip("사거리 안에서 공격을 지속하는 시간(초).")]
        [SerializeReference] public BlackboardVariable<float> AttackTime = new(0.8f);
        [Tooltip("공격 후 물러나는 시간(초).")]
        [SerializeReference] public BlackboardVariable<float> RetreatTime = new(0.7f);

        private enum Phase { Approach, Attack, Retreat }

        private BattleAgent _agent;
        private Phase _phase;
        private float _timer;

        protected override Status OnStart()
        {
            _agent = Resolve();
            if (_agent == null) return Status.Failure;
            _phase = Phase.Approach;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            var priority = Priority != null ? Priority.Value : TargetPriority.Nearest;
            if (_agent.FindTarget(priority) == null)
                return Status.Failure;

            _agent.RegisterFocus();
            var dt = Time.deltaTime;

            switch (_phase)
            {
                case Phase.Approach:
                    if (_agent.TargetInRange())
                    {
                        _phase = Phase.Attack;
                        _timer = AttackTime != null ? AttackTime.Value : 0.8f;
                    }
                    else
                    {
                        _agent.MoveToTarget(dt);
                    }
                    break;

                case Phase.Attack:
                    if (_agent.TargetInRange()) _agent.Attack();
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _agent.StopAttack();
                        _phase = Phase.Retreat;
                        _timer = RetreatTime != null ? RetreatTime.Value : 0.7f;
                    }
                    break;

                case Phase.Retreat:
                    _agent.RetreatFromTarget(dt);
                    _timer -= dt;
                    if (_timer <= 0f)
                        _phase = Phase.Approach;
                    break;
            }

            return Status.Running;
        }

        protected override void OnEnd() => _agent?.StopAttack();

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
