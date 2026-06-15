using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>타깃 주위를 도는 견제 이동. 사거리 내일 때 바로 붙어 싸우지 않고 거리를 두며 움직인다.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Strafe Target",
        description: "Circles around the target at attack range instead of standing still.",
        story: "[Agent] strafes around target",
        category: "Action/Battle",
        id: "a1d4e11349734783a5849eff2e7fee7f")]
    public partial class StrafeTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("도는 방향(+1 시계, -1 반시계).")]
        [SerializeReference] public BlackboardVariable<float> Direction = new(1f);
        [Tooltip("이 시간(초) 동안 strafe 후 Success로 빠진다.")]
        [SerializeReference] public BlackboardVariable<float> Duration = new(1.2f);

        private BattleAgent _agent;
        private float _timer;

        protected override Status OnStart()
        {
            _agent = Resolve();
            if (_agent == null || !_agent.TargetInRange()) return Status.Failure; // 사거리 밖 → 추격으로
            _timer = Duration != null ? Duration.Value : 1.2f;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null || !_agent.TargetInRange()) return Status.Failure;

            _timer -= Time.deltaTime;
            _agent.StrafeTarget(Time.deltaTime, Direction != null ? Direction.Value : 1f);
            return _timer > 0f ? Status.Running : Status.Success;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
