using System;
using Unity.Behavior;
using UnityEngine;

namespace _01.Code.BT
{
    /// <summary>사거리 내 같은 팀 아군이 있는지. WoundedOnly면 부상 아군만(서포터 힐 분기용).</summary>
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(
        name: "Has Ally In Range",
        category: "Conditions/Battle",
        story: "[Agent] has ally in range",
        id: "d9a3e6c4f5126f0cbe3d4f5061720833")]
    public partial class HasAllyInRangeCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<BattleAgent> Agent;
        [Tooltip("아군 탐지 범위.")]
        [SerializeReference] public BlackboardVariable<float> Range = new(4f);
        [Tooltip("켜면 부상당한(HP 미만) 아군만 센다.")]
        [SerializeReference] public BlackboardVariable<bool> WoundedOnly = new(true);

        public override bool IsTrue()
        {
            var a = Agent?.Value != null ? Agent.Value : GameObject?.GetComponentInParent<BattleAgent>();
            if (a == null || !a.IsAlive) return false;

            var range = Range != null ? Range.Value : 4f;
            var woundedOnly = WoundedOnly == null || WoundedOnly.Value;
            return a.HasAllyInRange(range, woundedOnly);
        }
    }
}
