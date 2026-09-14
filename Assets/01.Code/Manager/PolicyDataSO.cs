using UnityEngine;

namespace _01.Code.Manager
{
    [CreateAssetMenu(menuName = "SO/Management/Policy", fileName = "PolicyData")]
    public class PolicyDataSO : ScriptableObject
    {
        [SerializeField] private string displayName = "정책";
        [SerializeField, TextArea] private string description;
        [SerializeField, Range(-100, 100)] private int moraleDeltaOnSelect;
        [SerializeField] private int goldDeltaOnSelect;
        [SerializeField, Range(-20, 20)] private int dailyMoraleDelta;
        [SerializeField, Min(0)] private int durationDays = 1;
        [SerializeField] private bool canRepeat = true;

        [Header("Combat Tradeoff")]
        [SerializeField, Min(0.1f), Tooltip("정책이 유지되는 동안 모든 아군의 공격력 배율.")]
        private float unitDamageMultiplier = 1f;
        [SerializeField, Tooltip("정책이 유지되는 동안 모든 아군에게 더하는 방어.")]
        private int unitDefenseBonus;

        public string DisplayName => displayName;
        public string Description => description;
        public int MoraleDeltaOnSelect => moraleDeltaOnSelect;
        public int GoldDeltaOnSelect => goldDeltaOnSelect;
        public int DailyMoraleDelta => dailyMoraleDelta;
        public int DurationDays => durationDays;
        public bool CanRepeat => canRepeat;
        public float UnitDamageMultiplier => Mathf.Max(0.1f, unitDamageMultiplier);
        public int UnitDefenseBonus => unitDefenseBonus;
        public bool HasCombatEffect =>
            !Mathf.Approximately(UnitDamageMultiplier, 1f)
            || UnitDefenseBonus != 0;
    }
}
