using System;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    /// <summary>이 전투필드에서 적 팀 인원이 아군 팀보다 많은지(수세 판단). 도주·재집결 분기 가드에 사용.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Is Outnumbered",
        category: "Conditions/Battle",
        story: "[Agent] is outnumbered",
        id: "9a8b7c6d5e4f30211f2e3d4c5b6a7980")]
    public partial class IsOutnumberedCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            return a != null && a.IsOutnumbered();
        }
    }
}
