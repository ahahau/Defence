using _01.Code.Audio;
using _01.Code.Combat;
using _01.Code.StatusEffects;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class Trap : Building
    {
        [SerializeField]
        private float triggerChance = 0.5f;

        [SerializeField]
        private int damage = 3;

        [SerializeField, Range(0f, 1f)]
        private float injuryChance = 0.15f;

        [SerializeField, Min(0)]
        private int bonusDamage;

        [SerializeField]
        private StatusEffectDataSO injuryStatusEffect;

        [Header("Hit Animation")]
        [SerializeField] private Transform hitAnimationTarget;
        [SerializeField, Min(0f)] private float hitShakeDistance = 0.16f;
        [SerializeField, Min(0.01f)] private float hitShakeDuration = 0.18f;
        [SerializeField, Min(1)] private int hitShakeSteps = 4;

        [field: SerializeField, Min(0)]
        public int DangerIncreaseOnTrigger { get; private set; } = 1;

        private Vector3 _hitAnimationBaseLocalPosition;
        private Tween _hitAnimationTween;

        public float TriggerChance => triggerChance;
        public int Damage => damage;
        public float InjuryChance => injuryChance;
        public int BonusDamage => bonusDamage;
        public StatusEffectDataSO StatusEffect => injuryStatusEffect;

        private void Awake()
        {
            _hitAnimationBaseLocalPosition = hitAnimationTarget.localPosition;
        }

        private void OnDisable()
        {
            StopHitAnimation(true);
        }

        public bool TryDamage(IDamageable target)
        {
            if (target == null || !target.IsAlive)
                return false;

            if (Random.value > triggerChance)
                return false;

            Component targetComponent = target as Component;
            var resolvedDamage = damage + bonusDamage;
            if (targetComponent != null && targetComponent.TryGetComponent<EnemyStatusController>(out var statusController))
                resolvedDamage = statusController.ModifyTrapDamage(resolvedDamage);
            
            target.TakeDamage(resolvedDamage);
            GameSfxPlayer.Play(GameSfxCue.Trap);
            PlayHitAnimation();
            TryApplyInjury(target, targetComponent);
            return true;
        }
        
        private void PlayHitAnimation()
        {
            if (hitAnimationTarget == null || hitShakeDistance <= 0f)
                return;
            
            StopHitAnimation(false);
            
            hitAnimationTarget.localPosition = _hitAnimationBaseLocalPosition;
            _hitAnimationTween = hitAnimationTarget
                .DOLocalMoveX(_hitAnimationBaseLocalPosition.x + hitShakeDistance, hitShakeDuration / hitShakeSteps)
                .SetEase(Ease.InOutSine)
                .SetLoops(hitShakeSteps, LoopType.Yoyo)
                .OnComplete(() => hitAnimationTarget.localPosition = _hitAnimationBaseLocalPosition);
        }

        private void StopHitAnimation(bool complete)
        {
            if (_hitAnimationTween != null && _hitAnimationTween.IsActive())
            {
                if (complete)
                    _hitAnimationTween.Complete();
                else
                    _hitAnimationTween.Kill();
            }
            
            _hitAnimationTween = null;
            
            if (hitAnimationTarget != null)
                hitAnimationTarget.localPosition = _hitAnimationBaseLocalPosition;
        }

        private void TryApplyInjury(IDamageable target, Component targetComponent)
        {
            if (injuryStatusEffect == null || targetComponent == null || !target.IsAlive)
                return;

            if (Random.value > injuryChance)
                return;

            injuryStatusEffect.TryApplyTo(targetComponent);
        }
    }
}
