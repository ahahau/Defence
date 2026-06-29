using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>도발(Tank): 사거리 내 적들이 잠시 나를 노리게 어그로를 끈다(후열 보호). 지정 시간 후 Success.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Taunt",
        description: "Forces nearby enemies to target this agent for a moment (tank peel).",
        story: "[Agent] taunts nearby enemies",
        category: "Action/Battle",
        id: "a1b2c3d4e5f60718293a4b5c6d7e8f90")]
    public partial class TauntAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("도발 범위(이 거리 안 적이 나를 노림).")]
        [SerializeReference] public BlackboardVariable<float> Range = new(5f);
        [Tooltip("이 시간(초) 동안 매 프레임 어그로를 끈 뒤 Success.")]
        [SerializeReference] public BlackboardVariable<float> Duration = new(0.5f);

        private BattleAgent _agent;
        private float _timer;

        protected override Status OnStart()
        {
            _agent = Resolve();
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            _timer = Duration != null ? Duration.Value : 0.5f;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked)
                return Status.Failure;

            _agent.TauntNearbyEnemies(Range != null ? Range.Value : 5f);
            _timer -= Time.deltaTime;
            return _timer > 0f ? Status.Running : Status.Success;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
