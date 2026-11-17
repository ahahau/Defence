using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 두 정책이 동시에 살아 있을 때 따로 붙는 효과.
    ///
    /// 정책은 지금까지 서로 곱해질 뿐 아무 관계도 없었다. 그러면 매번 "지금 제일 센 것"만
    /// 고르게 되고, 어제 무엇을 골랐는지가 오늘의 선택을 바꾸지 않는다.
    /// 조합은 그 기억을 만든다 — 계엄령이 아직 돌고 있으면 철벽 수비의 값이 달라진다.
    ///
    /// 효과는 이미 배선된 축(피해 배율·방어 가산·권능 배율·일일 민심)만 쓴다.
    /// 새 축을 만들면 소비처를 전부 찾아 고쳐야 하고, 그건 조합이 감당할 값이 아니다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Management/Policy Combo Catalog", fileName = "PolicyComboCatalog")]
    public sealed class PolicyComboCatalogSO : ScriptableObject
    {
        [SerializeField] private List<PolicyCombo> combos = new();

        public IReadOnlyList<PolicyCombo> Combos => combos;

        public void ReplaceCombos(List<PolicyCombo> values) => combos = values ?? new List<PolicyCombo>();

        /// <summary>지금 살아 있는 정책들로 성립하는 조합을 모은다.</summary>
        public void CollectActive(IReadOnlyList<PolicyDataSO> active, List<PolicyCombo> target)
        {
            if (target == null)
                return;

            target.Clear();
            if (combos == null || active == null || active.Count < 2)
                return;

            foreach (var combo in combos)
            {
                if (combo != null && combo.IsSatisfiedBy(active))
                    target.Add(combo);
            }
        }
    }

    [Serializable]
    public sealed class PolicyCombo
    {
        [SerializeField] private string displayName = "조합";
        [SerializeField, TextArea] private string description;
        [SerializeField, Tooltip("이 둘이 동시에 살아 있어야 성립한다.")]
        private PolicyDataSO first;
        [SerializeField] private PolicyDataSO second;

        [Header("추가 효과")]
        [SerializeField, Min(0.1f)] private float unitDamageMultiplier = 1f;
        [SerializeField] private int unitDefenseBonus;
        [SerializeField, Range(-20, 20)] private int dailyMoraleDelta;

        public string DisplayName => displayName;
        public string Description => description;
        public PolicyDataSO First => first;
        public PolicyDataSO Second => second;
        public float UnitDamageMultiplier => Mathf.Max(0.1f, unitDamageMultiplier);
        public int UnitDefenseBonus => unitDefenseBonus;
        public int DailyMoraleDelta => dailyMoraleDelta;

        /// <summary>짝의 다른 한쪽. 정책 하나만 알 때 "무엇과 묶이는지" 안내에 쓴다.</summary>
        public PolicyDataSO GetPartnerOf(PolicyDataSO policy)
        {
            if (policy == null) return null;
            if (policy == first) return second;
            if (policy == second) return first;
            return null;
        }

        public bool IsSatisfiedBy(IReadOnlyList<PolicyDataSO> active)
        {
            if (first == null || second == null || first == second)
                return false;

            bool hasFirst = false, hasSecond = false;
            foreach (var policy in active)
            {
                if (policy == first) hasFirst = true;
                else if (policy == second) hasSecond = true;
            }

            return hasFirst && hasSecond;
        }
    }
}
