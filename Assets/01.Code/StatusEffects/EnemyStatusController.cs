using System.Collections.Generic;
using _01.Code.Combat;
using UnityEngine;

namespace _01.Code.StatusEffects
{
    public class EnemyStatusController : MonoBehaviour
    {
        private readonly List<ActiveStatusEffect> _activeEffects = new();
        private readonly List<ActiveStatusEffectSnapshot> _activeEffectSnapshots = new();

        private Combatant _combatant;
        private bool _combatantResolved;
        public bool HasActiveEffects => _activeEffects.Count > 0;

        /// <summary>같은 오브젝트의 전투 담당. 없을 수도 있으므로 처음 필요할 때 찾는다.</summary>
        private Combatant Combatant
        {
            get
            {
                if (_combatantResolved)
                    return _combatant;

                _combatant = GetComponent<Combatant>();
                _combatantResolved = true;
                return _combatant;
            }
        }

        /// <summary>
        /// 공격 주기 배율을 스탯 표에 얹는다. 효과 자산 자체가 출처 열쇠다 —
        /// 같은 효과가 다시 걸리면 갱신되고, 만료되면 그 열쇠만 걷힌다.
        /// </summary>
        private void PushAttackIntervalModifier(StatusEffectDataSO effect)
        {
            if (effect == null)
                return;

            Combatant?.SetAttackIntervalModifier(effect, effect.GetAttackIntervalMultiplier(CreateContext(effect)));
        }

        private void PullAttackIntervalModifier(StatusEffectDataSO effect)
        {
            if (effect != null)
                Combatant?.RemoveModifier(_01.Code.Core.Stats.StatIndex.AttackInterval, effect);
        }

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
                PushAttackIntervalModifier(effect);
                return;
            }

            _activeEffects.Add(new ActiveStatusEffect(effect, duration));
            effect.OnApplied(CreateContext(effect));
            PushAttackIntervalModifier(effect);
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
                    PullAttackIntervalModifier(activeEffect.Effect);
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
                {
                    effect.OnExpired(CreateContext(effect));
                    PullAttackIntervalModifier(effect);
                }
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

        public IReadOnlyList<ActiveStatusEffectSnapshot> GetActiveEffects()
        {
            _activeEffectSnapshots.Clear();

            foreach (var activeEffect in _activeEffects)
            {
                if (activeEffect.Effect == null)
                    continue;

                _activeEffectSnapshots.Add(new ActiveStatusEffectSnapshot(
                    activeEffect.Effect,
                    activeEffect.RemainingNodeVisits));
            }

            return _activeEffectSnapshots;
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

        public readonly struct ActiveStatusEffectSnapshot
        {
            public readonly StatusEffectDataSO Effect;
            public readonly int RemainingNodeVisits;

            public ActiveStatusEffectSnapshot(StatusEffectDataSO effect, int remainingNodeVisits)
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
