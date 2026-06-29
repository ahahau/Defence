using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>아군 무리 쪽으로 후퇴한다(저체력 도주/재집결). 보통 Health Below 가드와 함께 쓴다.
    /// 지정 시간 동안 후퇴 후 Success로 빠져 다음 분기로 넘어간다.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Flee To Allies",
        description: "Retreats toward the allied group (low-HP escape / regroup).",
        story: "[Agent] flees to allies",
        category: "Action/Battle",
        id: "0f1e2d3c4b5a69788796a5b4c3d2e1f0")]
    public partial class FleeToAlliesAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("이 시간(초) 동안 아군 쪽으로 후퇴 후 Success.")]
        [SerializeReference] public BlackboardVariable<float> Duration = new(1f);

        private BattleAgent _agent;
        private float _timer;

        protected override Status OnStart()
        {
            _agent = Resolve();
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            _timer = Duration != null ? Duration.Value : 1f;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            _agent.RetreatTowardAllies(Time.deltaTime);
            _timer -= Time.deltaTime;
            return _timer > 0f ? Status.Running : Status.Success;
        }

        protected override void OnEnd() => _agent?.StopAttack();

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
