using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace _01.Code.BT
{
    /// <summary>체력이 가장 낮은 적을 팀 공유 포커스로 지정한다(협동 집중공격 설정). 한 번에 Success.
    /// Engage Target(Priority=Focused) 앞에 두면 팀이 같은 처치 가능 대상을 함께 노린다.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [NodeDescription(
        name: "Focus Fire Lowest",
        description: "Marks the lowest-health enemy as the team focus target (coordinated burst).",
        story: "[Agent] calls focus fire on the weakest enemy",
        category: "Action/Battle",
        id: "ffeeddccbbaa00998877665544332211")]
    public partial class FocusFireLowestAction : Action
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        protected override Status OnStart()
        {
            var agent = Resolve();
            if (agent == null || !agent.IsAlive || agent.IsTraversalLocked)
                return Status.Failure;

            // 지정 성공 여부와 무관하게 Success로 빠져 뒤 분기(Engage Focused)가 이어지게 한다.
            agent.RegisterFocusLowestHealth();
            return Status.Success;
        }

        private BattleAgent Resolve() =>
            Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
    }
}
