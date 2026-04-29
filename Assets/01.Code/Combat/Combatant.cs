using System;
using System.Collections;
using _01.Code.Core;
using _01.Code.Events;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Combat
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(DamageFeedback))]
    public class Combatant : MonoBehaviour
    {
        [SerializeField] private int attackDamage = 1;
        [SerializeField] private float attackInterval = 1f;
        [SerializeField] private float bodySlamDistance = 0.3f;
        [SerializeField] private float bodySlamDuration = 0.12f;
        [SerializeField] private CombatBarsView barsView;

        private Coroutine _attackRoutine;
        private Tween _bodySlamTween;
        private Health _health;
        private DamageFeedback _damageFeedback;
        private bool _isAttacking;
        private int artifactAttackDamageBonus;
        private float artifactAttackDamageMultiplier = 1f;
        private GameEventChannelSO artifactEventChannel;

        public bool IsAlive => _health != null && _health.IsAlive;
        public bool IsAttacking => _isAttacking;
        public Health Health => _health;

        public void AddAttackDamage(int amount)
        {
            if (amount > 0)
                attackDamage += amount;
        }

        public void SetArtifactAttackModifier(int damageBonus, float damageMultiplier)
        {
            artifactAttackDamageBonus = damageBonus;
            artifactAttackDamageMultiplier = Mathf.Max(0.05f, damageMultiplier);
        }

        public void SetArtifactEventChannel(GameEventChannelSO eventChannel)
        {
            artifactEventChannel = eventChannel;
        }

        public void MultiplyAttackInterval(float multiplier)
        {
            if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
                return;

            attackInterval = Mathf.Max(0.05f, attackInterval * multiplier);
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _damageFeedback = GetComponent<DamageFeedback>();
            _health.Changed += RefreshHealthBar;
            RefreshBars(0f);
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.Changed -= RefreshHealthBar;
        }

        public void BeginCombat(Combatant target, Action<Combatant> targetDefeated)
        {
            if (target == null || !target.IsAlive || !IsAlive)
                return;

            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);

            _attackRoutine = StartCoroutine(AttackLoop(target, targetDefeated));
        }

        public void StopCombat()
        {
            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);

            _attackRoutine = null;
            _isAttacking = false;
            _bodySlamTween?.Kill();
            RefreshBars(0f);
        }

        private IEnumerator AttackLoop(Combatant target, Action<Combatant> targetDefeated)
        {
            var attackTimer = 0f;
            RefreshBars(attackTimer);

            while (target != null && target.IsAlive && IsAlive)
            {
                attackTimer += Time.deltaTime;
                RefreshAttackBar(attackTimer / attackInterval);

                if (attackTimer >= attackInterval && !_isAttacking)
                {
                    _isAttacking = true;
                    yield return PlayBodySlam(target);

                    if (target != null && target.Health != null)
                        target.Health.TakeDamage(ResolveAttackDamage(target));

                    attackTimer = 0f;
                    RefreshAttackBar(0f);
                    _isAttacking = false;

                    if (target == null || !target.IsAlive)
                    {
                        targetDefeated?.Invoke(target);
                        break;
                    }
                }

                yield return null;
            }

            StopCombat();
        }

        private IEnumerator PlayBodySlam(Combatant target)
        {
            if (target == null)
                yield break;

            _bodySlamTween?.Kill();

            var startPosition = transform.position;
            var targetPos = target != null ? target.transform.position : startPosition;
            var direction = targetPos - startPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector3.right;

            var hitPosition = startPosition + direction.normalized * bodySlamDistance;
            _bodySlamTween = DOTween.Sequence()
                .Append(transform.DOMove(hitPosition, bodySlamDuration).SetEase(Ease.OutQuad))
                .Append(transform.DOMove(startPosition, bodySlamDuration).SetEase(Ease.InQuad))
                .SetLink(gameObject);

            yield return _bodySlamTween.WaitForCompletion();
        }

        private void RefreshBars(float attackRatio)
        {
            RefreshHealthBar(_health != null ? _health.CurrentRatio : 0f);
            RefreshAttackBar(attackRatio);
        }

        private void RefreshHealthBar(float ratio)
        {
            barsView.SetHealthRatio(ratio);
        }

        private void RefreshAttackBar(float ratio)
        {
            barsView.SetAttackRatio(ratio);
        }

        private int ResolveAttackDamage(Combatant target)
        {
            var modifiedDamage = (attackDamage + artifactAttackDamageBonus) * artifactAttackDamageMultiplier;
            var damage = Mathf.Max(1, Mathf.RoundToInt(modifiedDamage));
            if (artifactEventChannel == null)
                return damage;

            var evt = new CombatDamageCalculatedEvent(this, target, damage);
            artifactEventChannel.RaiseEvent(evt);
            return evt.Damage;
        }
    }
}
