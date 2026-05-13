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
                effect.OnRefreshed(CreateContext(effect));
                return;
            }

            _activeEffects.Add(new ActiveStatusEffect(effect, duration));
            effect.OnApplied(CreateContext(effect));
        }

        public void TickNodeVisit()
        {
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var activeEffect = _activeEffects[i];
                activeEffect.RemainingNodeVisits--;

                if (activeEffect.RemainingNodeVisits <= 0)
                {
                    activeEffect.Effect?.OnExpired(CreateContext(activeEffect.Effect));
                    _activeEffects.RemoveAt(i);
                }
                else
                {
                    _activeEffects[i] = activeEffect;
                }
            }
        }

        private void OnDisable()
        {
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i].Effect;
                if (effect != null)
                    effect.OnExpired(CreateContext(effect));
            }

            _activeEffects.Clear();
        }

        public float GetAttackIntervalMultiplier()
        {
            var multiplier = 1f;
            foreach (var activeEffect in EnumerateActiveEffects())
                multiplier *= activeEffect.Effect.GetAttackIntervalMultiplier(activeEffect.Context);

            return Mathf.Max(0.05f, multiplier);
        }

        public int ModifyTrapDamage(int baseDamage)
        {
            var resolvedDamage = Mathf.Max(1, baseDamage);
            foreach (var activeEffect in EnumerateActiveEffects())
                resolvedDamage = activeEffect.Effect.ModifyTrapDamage(activeEffect.Context, resolvedDamage);

            return Mathf.Max(1, resolvedDamage);
        }

        private IEnumerable<ActiveStatusEffectView> EnumerateActiveEffects()
        {
            foreach (var activeEffect in _activeEffects)
            {
                if (activeEffect.Effect == null)
                    continue;

                yield return new ActiveStatusEffectView(
                    activeEffect.Effect,
                    CreateContext(activeEffect.Effect));
            }
        }

        private StatusEffectContext CreateContext(StatusEffectDataSO effect)
        {
            return new StatusEffectContext(effect, this);
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

        private readonly struct ActiveStatusEffectView
        {
            public readonly StatusEffectDataSO Effect;
            public readonly StatusEffectContext Context;

            public ActiveStatusEffectView(StatusEffectDataSO effect, StatusEffectContext context)
            {
                Effect = effect;
                Context = context;
            }
        }
    }
}
