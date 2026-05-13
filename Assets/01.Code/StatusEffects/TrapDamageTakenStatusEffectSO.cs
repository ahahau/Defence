using UnityEngine;

namespace _01.Code.StatusEffects
{
    [CreateAssetMenu(
        fileName = "TrapDamageTakenStatusEffect",
        menuName = "Defence/Status Effects/Trap Damage Taken Modifier")]
    public class TrapDamageTakenStatusEffectSO : StatusEffectSO
    {
        [SerializeField, Min(0f)] private float multiplier = 1f;

        public override int ModifyTrapDamage(StatusEffectContext context, int damage)
        {
            return Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Max(0f, multiplier)));
        }
    }
}
