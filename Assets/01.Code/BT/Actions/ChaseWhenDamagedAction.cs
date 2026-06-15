using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>
    /// 최근 피해를 받았을 때만 타깃을 추격한다. 평소엔 Failure(다음 분기로) → "발견해도 바로 안 움직임".
    /// </summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Chase When Damaged",
        description: "Chases the target only if recently damaged; otherwise fails.",
        story: "[Agent] chases when damaged",
        category: "Action/Battle",
        id: "2d42ddede53a4bdfb77e6c4d8b866b86")]
    public partial class ChaseWhenDamagedAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("이 시간(초) 안에 피해를 받았으면 추격.")]
        [SerializeReference] public BlackboardVariable<float> Window = new(2.5f);

        private BattleAgent _agent;

        protected override Status OnStart()
        {
            _agent = Resolve();
            return _agent == null ? Status.Failure : Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (_agent == null) return Status.Failure;

            var window = Window != null ? Window.Value : 2.5f;
            if (!_agent.WasRecentlyDamaged(window)) return Status.Failure; // 안 맞았으면 추격 안 함

            if (_agent.FindNearestEnemy() == null) return Status.Failure;
            if (_agent.TargetInRange()) return Status.Success;

            _agent.MoveToTarget(Time.deltaTime);
            return Status.Running;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
