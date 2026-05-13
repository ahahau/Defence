using _01.Code.Enemies;
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
        [SerializeField] private StatusEffectSO[] effects;

        public string DisplayName => displayName;
        public string Description => description;
        public int DurationNodeVisits => durationNodeVisits;

        public bool TryApplyTo(Component target)
        {
            var statusController = ResolveStatusController(target);
            if (statusController == null)
                return false;

            statusController.Apply(this);
            return true;
        }

        public void OnApplied(StatusEffectContext context)
        {
            foreach (var effect in EnumerateEffects())
                effect.OnApplied(context);
        }

        public void OnRefreshed(StatusEffectContext context)
        {
            foreach (var effect in EnumerateEffects())
                effect.OnRefreshed(context);
        }

        public void OnExpired(StatusEffectContext context)
        {
            foreach (var effect in EnumerateEffects())
                effect.OnExpired(context);
        }

        public float GetAttackIntervalMultiplier(StatusEffectContext context)
        {
            var multiplier = Mathf.Max(0.05f, attackIntervalMultiplier);
            foreach (var effect in EnumerateEffects())
                multiplier *= effect.GetAttackIntervalMultiplier(context);

            return Mathf.Max(0.05f, multiplier);
        }

        public int ModifyTrapDamage(StatusEffectContext context, int damage)
        {
            var resolvedDamage = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Max(0f, trapDamageTakenMultiplier)));
            foreach (var effect in EnumerateEffects())
                resolvedDamage = effect.ModifyTrapDamage(context, resolvedDamage);

            return Mathf.Max(1, resolvedDamage);
        }

        private EnemyStatusController ResolveStatusController(Component target)
        {
            if (target == null)
                return null;

            if (target.TryGetComponent<EnemyStatusController>(out var statusController))
                return statusController;

            statusController = target.GetComponentInParent<EnemyStatusController>();
            if (statusController != null)
                return statusController;

            statusController = target.GetComponentInChildren<EnemyStatusController>();
            if (statusController != null)
                return statusController;

            return target.TryGetComponent<Enemy>(out _)
                ? target.gameObject.AddComponent<EnemyStatusController>()
                : null;
        }

        private System.Collections.Generic.IEnumerable<StatusEffectSO> EnumerateEffects()
        {
            if (effects == null)
                yield break;

            foreach (var effect in effects)
            {
                if (effect != null)
                    yield return effect;
            }
        }
    }
}
