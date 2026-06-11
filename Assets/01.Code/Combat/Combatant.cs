using System;
using System.Collections;
using System.Reflection;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.StatusEffects;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Combat
{
    public class Combatant : MonoBehaviour
    {
        [SerializeField] private int attackDamage = 1;
        [SerializeField, Min(0)] private int defense;
        [SerializeField] private float attackInterval = 1f;
        [SerializeField] private float bodySlamDistance = 0.3f;
        [SerializeField] private float bodySlamDuration = 0.12f;
        [SerializeField, Range(0f, 1f)] private float evasionChance;
        [SerializeField, Min(0f)] private float dodgeBackDistance = 0.22f;
        [SerializeField, Min(0f)] private float dodgeBackDuration = 0.1f;
        [SerializeField, Min(0f)] private float dodgeReturnDuration = 0.12f;
        [SerializeField] private CombatBarsView barsView;
        [SerializeField] private MonoBehaviour attackFeelFeedbacks;
        [SerializeField] private ParticleSystem attackHitParticles;
        [SerializeField] private Color attackParticleColor = new(1f, 0.82f, 0.35f, 1f);
        [SerializeField, Min(1)] private int attackParticleBurstCount = 14;
        [SerializeField, Min(0f)] private float attackImpactOffset = 0.08f;
        [SerializeField] private int attackParticleSortingOrder = 75;
        [SerializeField] private Health health;
        [SerializeField] private DamageFeedback damageFeedback;

        private Coroutine _attackRoutine;
        private Tween _bodySlamTween;
        private Tween _dodgeTween;
        private MethodInfo _playFeelFeedbacksAtPosition;
        private bool _isAttacking;
        private int artifactAttackDamageBonus;
        private float artifactAttackDamageMultiplier = 1f;
        private GameEventChannelSO artifactEventChannel;
        private EnemyStatusController enemyStatusController;
        public bool IsAlive => health != null && health.IsAlive;
        public bool IsAttacking => _isAttacking;
        public Health Health => health;
        public int AttackDamage => ResolveAttackDamagePreview();
        public int Defense => Mathf.Max(0, defense);
        public float AttackInterval => ResolveAttackInterval();
        public float EvasionChance => evasionChance;

        public void AddAttackDamage(int amount)
        {
            if (amount > 0)
                attackDamage += amount;
        }

        public void SetAttackDamage(int value)
        {
            attackDamage = Mathf.Max(1, value);
        }

        public void SetDefense(int value)
        {
            defense = Mathf.Max(0, value);
        }

        public void SetEvasionChance(float value)
        {
            evasionChance = Mathf.Clamp01(value);
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

        public void SetAttackInterval(float value)
        {
            attackInterval = Mathf.Max(0.05f, value);
        }

        private void Awake()
        {
            enemyStatusController = GetComponent<EnemyStatusController>();
            EnsureAttackFeedbacks();
            if (health != null)
                health.Changed += RefreshHealthBar;
            RefreshBars(0f);
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Changed -= RefreshHealthBar;
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
            _dodgeTween?.Kill();
            RefreshBars(0f);
        }

        private IEnumerator AttackLoop(Combatant target, Action<Combatant> targetDefeated)
        {
            var attackTimer = 0f;
            RefreshBars(attackTimer);

            while (target != null && target.IsAlive && IsAlive)
            {
                var currentAttackInterval = ResolveAttackInterval();
                attackTimer += Time.deltaTime;
                RefreshAttackBar(attackTimer / currentAttackInterval);

                if (attackTimer >= currentAttackInterval && !_isAttacking)
                {
                    _isAttacking = true;
                    yield return PlayBodySlam(target);

                    if (target != null && target.Health != null)
                    {
                        if (target.TryDodgeAttack(transform.position))
                        {
                            attackTimer = 0f;
                            RefreshAttackBar(0f);
                            _isAttacking = false;
                            yield return null;
                            continue;
                        }

                        PlayAttackFeedback(transform.position, target.transform.position);
                        target.Health.TakeDamage(ResolveAttackDamage(target));
                    }

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

        private bool TryDodgeAttack(Vector3 attackerPosition)
        {
            if (!IsAlive || evasionChance <= 0f || UnityEngine.Random.value >= evasionChance)
                return false;

            PlayDodgeBack(attackerPosition);
            return true;
        }

        private void PlayDodgeBack(Vector3 attackerPosition)
        {
            if (dodgeBackDistance <= 0f)
                return;

            _dodgeTween?.Kill();

            var startPosition = transform.position;
            var direction = startPosition - attackerPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector3.right;

            var dodgePosition = startPosition + direction.normalized * dodgeBackDistance;
            _dodgeTween = DOTween.Sequence()
                .Append(transform.DOMove(dodgePosition, dodgeBackDuration).SetEase(Ease.OutQuad))
                .Append(transform.DOMove(startPosition, dodgeReturnDuration).SetEase(Ease.InQuad))
                .SetLink(gameObject);
        }

        private void RefreshBars(float attackRatio)
        {
            RefreshHealthBar(health != null ? health.CurrentRatio : 0f);
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
            if (artifactEventChannel != null)
            {
                var evt = new CombatDamageCalculatedEvent(this, target, damage);
                artifactEventChannel.RaiseEvent(evt);
                damage = evt.Damage;
            }

            return CalculateDamageAfterDefense(damage, target);
        }

        private int ResolveAttackDamagePreview()
        {
            var modifiedDamage = (attackDamage + artifactAttackDamageBonus) * artifactAttackDamageMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(modifiedDamage));
        }

        private int CalculateDamageAfterDefense(int damage, Combatant target)
        {
            if (target == null)
                return Mathf.Max(1, damage);

            var defense = target.Defense;
            if (defense <= 0)
                return Mathf.Max(1, damage);

            var reducedDamage = damage - damage * (defense / (defense + 100f));
            return Mathf.Max(1, Mathf.RoundToInt(reducedDamage));
        }

        private float ResolveAttackInterval()
        {
            var statusController = ResolveEnemyStatusController();
            var multiplier = statusController != null
                ? statusController.GetAttackIntervalMultiplier()
                : 1f;
            return Mathf.Max(0.05f, attackInterval * multiplier);
        }

        private EnemyStatusController ResolveEnemyStatusController()
        {
            if (enemyStatusController == null)
                enemyStatusController = GetComponent<EnemyStatusController>();

            return enemyStatusController;
        }

        private void EnsureAttackFeedbacks()
        {
            _playFeelFeedbacksAtPosition = attackFeelFeedbacks != null
                ? attackFeelFeedbacks.GetType().GetMethod("PlayFeedbacks", new[] { typeof(Vector3), typeof(float), typeof(bool) })
                : null;
        }

        private void PlayAttackFeedback(Vector3 attackerPosition, Vector3 targetPosition)
        {
            var direction = targetPosition - attackerPosition;
            direction.z = 0f;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
                direction = Vector3.right;

            var impactPosition = targetPosition - direction.normalized * attackImpactOffset;
            _playFeelFeedbacksAtPosition?.Invoke(attackFeelFeedbacks, new object[] { impactPosition, 1f, false });

            if (attackHitParticles == null)
                return;

            attackHitParticles.transform.position = impactPosition;
            attackHitParticles.transform.right = direction.normalized;
            attackHitParticles.Play(true);
        }

        private void EnsureDefaultAttackParticles()
        {
            if (attackHitParticles != null)
                return;

            var particleObject = new GameObject("AttackHitParticles");
            particleObject.transform.SetParent(transform);
            particleObject.transform.localPosition = Vector3.zero;
            particleObject.transform.localRotation = Quaternion.identity;
            particleObject.transform.localScale = Vector3.one;

            attackHitParticles = particleObject.AddComponent<ParticleSystem>();
            ConfigureAttackParticles(attackHitParticles);
        }

        private void ConfigureAttackParticles(ParticleSystem particles)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.22f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.11f);
            main.startColor = attackParticleColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)attackParticleBurstCount)
            });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 24f;
            shape.radius = 0.03f;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(attackParticleColor, 0f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.12f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = attackParticleSortingOrder;
        }
    }
}
