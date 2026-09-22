using System;
using System.Collections.Generic;
using Code.Units;
using UnityEngine;

namespace Code.Artifacts
{
    /// <summary>
    /// 두 유물을 함께 가졌을 때 따로 붙는 효과.
    ///
    /// 유물은 값이 무겁다 — 좋은 유닛 두 명 값이다. 그 값을 스탯 몇 점으로 정당화하려 들면
    /// 언제나 유닛이 이긴다. 그래서 유물이 사는 이유는 효율이 아니라 조합이어야 한다.
    /// 하나만 사면 비싼 스탯이지만, 짝을 맞추면 그때부터 판이 달라지는 식이다.
    ///
    /// 한 유물이 여러 조합에 들어갈 수 있다. 그래야 "무엇과 맞출까"가 갈림길이 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Artifact/Combo Catalog", fileName = "ArtifactComboCatalog")]
    public sealed class ArtifactComboCatalogSO : ScriptableObject
    {
        [SerializeField] private List<ArtifactCombo> combos = new();

        public IReadOnlyList<ArtifactCombo> Combos => combos;

        public void ReplaceCombos(List<ArtifactCombo> values) => combos = values ?? new List<ArtifactCombo>();

        /// <summary>가진 유물로 성립하는 조합들이 이 부하에게 얹는 몫.</summary>
        public ArtifactStatBonus CalculateBonus(IReadOnlyList<ArtifactDataSO> owned, Unit unit)
        {
            var bonus = new ArtifactStatBonus(0, 1f, 0, 1f);
            if (combos == null || owned == null || owned.Count < 2)
                return bonus;

            foreach (var combo in combos)
            {
                if (combo != null && combo.IsOwnedBy(owned) && combo.AppliesTo(unit))
                    bonus.Add(combo.StatBonus);
            }

            return bonus;
        }

        /// <summary>이 유물이 참여하는 조합들. 상점에서 "무엇과 맞물리는지" 보여 주는 데 쓴다.</summary>
        public void CollectCombosWith(ArtifactDataSO artifact, List<ArtifactCombo> target)
        {
            if (target == null)
                return;

            target.Clear();
            if (combos == null || artifact == null)
                return;

            foreach (var combo in combos)
            {
                if (combo != null && combo.GetPartnerOf(artifact) != null)
                    target.Add(combo);
            }
        }
    }

    [Serializable]
    public sealed class ArtifactCombo
    {
        [SerializeField] private string displayName = "조합";
        [SerializeField, TextArea] private string description;
        [SerializeField, Tooltip("이 둘을 함께 가지고 있어야 성립한다.")]
        private ArtifactDataSO first;
        [SerializeField] private ArtifactDataSO second;

        [Header("추가 효과")]
        [SerializeField, Tooltip("조합 효과가 닿는 대상. 두 유물의 대상과 별개로 정한다.")]
        private ArtifactTarget target = ArtifactTarget.AllUnits;
        [SerializeField] private int attackDamageBonus;
        [SerializeField, Min(0.05f)] private float attackDamageMultiplier = 1f;
        [SerializeField] private int maxHealthBonus;
        [SerializeField, Min(0.05f)] private float attackIntervalMultiplier = 1f;

        public string DisplayName => displayName;
        public string Description => description;
        public ArtifactDataSO First => first;
        public ArtifactDataSO Second => second;
        public ArtifactTarget Target => target;

        public ArtifactStatBonus StatBonus => new(
            attackDamageBonus,
            Mathf.Max(0.05f, attackDamageMultiplier),
            maxHealthBonus,
            Mathf.Max(0.05f, attackIntervalMultiplier));

        public ArtifactDataSO GetPartnerOf(ArtifactDataSO artifact)
        {
            if (artifact == null) return null;
            if (artifact == first) return second;
            if (artifact == second) return first;
            return null;
        }

        public bool IsOwnedBy(IReadOnlyList<ArtifactDataSO> owned)
        {
            if (first == null || second == null || first == second)
                return false;

            bool hasFirst = false, hasSecond = false;
            foreach (var artifact in owned)
            {
                if (artifact == first) hasFirst = true;
                else if (artifact == second) hasSecond = true;
            }

            return hasFirst && hasSecond;
        }

        public bool AppliesTo(Unit unit) => target switch
        {
            ArtifactTarget.PlayerOnly => unit is MainUnit,
            ArtifactTarget.HiredUnitsOnly => unit is not MainUnit,
            _ => true
        };
    }
}
