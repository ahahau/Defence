using UnityEngine;

namespace _01.Code.StatusEffects
{
    public abstract class StatusEffectSO : ScriptableObject
    {
        public virtual void OnApplied(StatusEffectContext context)
        {
        }

        public virtual void OnRefreshed(StatusEffectContext context)
        {
        }

        public virtual void OnExpired(StatusEffectContext context)
        {
        }

        public virtual float GetAttackIntervalMultiplier(StatusEffectContext context)
        {
            return 1f;
        }

        public virtual int ModifyTrapDamage(StatusEffectContext context, int damage)
        {
            return damage;
        }
    }
}
