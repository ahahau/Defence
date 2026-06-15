using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>
    /// 후열(원거리) 카이팅: 선호 사거리를 유지한다. 멀면 접근, 너무 가까우면 물러난다.
    /// 타깃이 없으면 Failure(다음 분기로).
    /// </summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Maintain Range",
        description: "Keeps preferred attack range from the target (ranged kiting).",
        story: "[Agent] keeps range from target",
        category: "Action/Battle",
        id: "3851129608424b16904dc965e7d20eff")]
    public partial class MaintainRangeAction : Action
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
            if (_agent == null || !_agent.IsAlive || _agent.IsTraversalLocked || _agent.CurrentTarget == null)
                return Status.Failure;

            // 너무 가까울 때만 물러남(Running). 적정 거리면 Failure → 공격 노드로.
            return _agent.MaintainRange(Time.deltaTime) ? Status.Running : Status.Failure;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
