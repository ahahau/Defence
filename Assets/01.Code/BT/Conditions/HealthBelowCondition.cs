using System;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    /// <summary>자신의 HP 비율이 임계값 미만인지. 후퇴/궁여지책 분기에 사용.</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Health Below",
        category: "Conditions/Battle",
        story: "[Agent] health below [Threshold]",
        id: "b7e1c4a2d3f04e8a9c1b2d3e4f50a611")]
    public partial class HealthBelowCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("HP 비율 임계값(0~1). 이 값 미만이면 true.")]
        [SerializeReference] public BlackboardVariable<float> Threshold = new(0.5f);

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            if (a == null || !a.IsAlive) return false;

            var threshold = Threshold != null ? Threshold.Value : 0.5f;
            return a.HealthRatio < threshold;
        }
    }
}
