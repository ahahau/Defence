using UnityEngine;

namespace _01.Code.StatusEffects
{
    [CreateAssetMenu(
        fileName = "AttackIntervalStatusEffect",
        menuName = "Defence/Status Effects/Attack Interval Modifier")]
    public class AttackIntervalStatusEffectSO : StatusEffectSO
    {
        [SerializeField, Min(0.05f)] private float multiplier = 1f;

        public override float GetAttackIntervalMultiplier(StatusEffectContext context)
        {
            return Mathf.Max(0.05f, multiplier);
        }
    }
}
