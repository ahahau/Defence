using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.StatusEffects
{
    public class EnemyStatusController : MonoBehaviour
    {
        private readonly List<ActiveStatusEffect> _activeEffects = new();

        public void Apply(StatusEffectDataSO effect)
        {
            if (effect == null)
                return;

            var duration = Mathf.Max(1, effect.DurationNodeVisits);
            for (var i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Effect != effect)
                    continue;

                _activeEffects[i] = new ActiveStatusEffect(effect, duration);
                return;
            }

            _activeEffects.Add(new ActiveStatusEffect(effect, duration));
        }

        public void TickNodeVisit()
        {
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var activeEffect = _activeEffects[i];
                activeEffect.RemainingNodeVisits--;

                if (activeEffect.RemainingNodeVisits <= 0)
                    _activeEffects.RemoveAt(i);
                else
                    _activeEffects[i] = activeEffect;
            }
        }

        public float GetAttackIntervalMultiplier()
        {
            var multiplier = 1f;
            foreach (var activeEffect in _activeEffects)
                multiplier *= activeEffect.Effect.AttackIntervalMultiplier;

            return Mathf.Max(0.05f, multiplier);
        }

        public int ModifyTrapDamage(int baseDamage)
        {
            var multiplier = 1f;
            foreach (var activeEffect in _activeEffects)
                multiplier *= activeEffect.Effect.TrapDamageTakenMultiplier;

            return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
        }

        private struct ActiveStatusEffect
        {
            public readonly StatusEffectDataSO Effect;
            public int RemainingNodeVisits;

            public ActiveStatusEffect(StatusEffectDataSO effect, int remainingNodeVisits)
            {
                Effect = effect;
                RemainingNodeVisits = remainingNodeVisits;
            }
        }
    }
}
