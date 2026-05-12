using UnityEngine;

namespace _01.Code.StatusEffects
{
    [CreateAssetMenu(fileName = "StatusEffectData", menuName = "Defence/Status Effect")]
    public class StatusEffectDataSO : ScriptableObject
    {
        [SerializeField] private string displayName = "Status Effect";
        [SerializeField, TextArea] private string description;
        [SerializeField, Min(1)] private int durationNodeVisits = 3;
        [SerializeField, Min(0.05f)] private float attackIntervalMultiplier = 1f;
        [SerializeField, Min(0f)] private float trapDamageTakenMultiplier = 1f;

        public string DisplayName => displayName;
        public string Description => description;
        public int DurationNodeVisits => durationNodeVisits;
        public float AttackIntervalMultiplier => attackIntervalMultiplier;
        public float TrapDamageTakenMultiplier => trapDamageTakenMultiplier;
    }
}
