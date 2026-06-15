using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>전열(Tank): 적 무리와 아군 후열 사이를 사수하며 후열을 가린다.
    /// 지정 시간 동안 포지셔닝 후 Success로 빠져 공격 분기로 넘어간다.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Guard Backline",
        description: "Moves between the enemies and own backline to shield allies.",
        story: "[Agent] guards the backline",
        category: "Action/Battle",
        id: "f1c508e617243802da5f617283940a5b")]
    public partial class GuardBacklineAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("이 시간(초) 동안 포지셔닝 후 Success.")]
        [SerializeReference] public BlackboardVariable<float> Duration = new(0.8f);

        private BattleAgent _agent;
        private float _timer;

        protected override Status OnStart()
        {
            _agent = Resolve();
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            _timer = Duration != null ? Duration.Value : 0.8f;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            _timer -= Time.deltaTime;
            _agent.GuardBackline(Time.deltaTime);
            return _timer > 0f ? Status.Running : Status.Success;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
