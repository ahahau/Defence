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

        [SerializeField, Min(0f),
         Tooltip("일차마다 더해지는 피해. 침입자 체력은 매일 늘어나므로 이게 없으면 초반에 지은 함정이 후반에 무의미해진다.")]
        private float damagePerDay = 0.5f;

        [SerializeField, Min(0),
         Tooltip("한 번 발동할 때마다 닳는 내구도. 0이면 영구히 작동한다.\n" +
                 "가격만으로는 후반을 못 잡는다 — 경제는 복리로 커지는데 가격은 고정이라\n" +
                 "어떤 값도 결국 공짜가 된다. 발동 횟수 상한은 금화 규모와 무관하게 걸린다.")]
        private int wearPerTrigger = 1;

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

        /// <summary>직전 발동에서 실제로 준 피해. 정산이 함정의 몫을 집계할 때 읽는다.</summary>
        public int LastTriggerDamage { get; private set; }

        /// <summary>
        /// 지금 일차 기준의 실제 피해.
        /// 침입자는 매일 단단해지므로 고정 피해로 두면 3일차에 지은 함정이 18일차엔 긁는 수준이 된다.
        /// </summary>
        public int CurrentDamage
        {
            get
            {
                var day = _01.Code.Manager.DayManager.Current != null
                    ? _01.Code.Manager.DayManager.Current.CurrentDay
                    : 0;
                return damage + bonusDamage + Mathf.FloorToInt(Mathf.Max(0, day) * damagePerDay);
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (hitAnimationTarget == null)
                hitAnimationTarget = transform;

            _hitAnimationBaseLocalPosition = hitAnimationTarget.localPosition;
        }

        private void OnDisable()
        {
            StopHitAnimation(true);
        }

        public bool TryDamage(IDamageable target)
        {
            if (target == null || !target.IsAlive || IsDestroyed)
                return false;

            if (Random.value > triggerChance)
                return false;

            Component targetComponent = target as Component;

            // 공병은 밟지 않고 뜯어낸다. 피해 판정보다 먼저 본다.
            if (TryDisarm(targetComponent))
                return false;

            var resolvedDamage = CurrentDamage;
            if (targetComponent != null && targetComponent.TryGetComponent<EnemyStatusController>(out var statusController))
                resolvedDamage = statusController.ModifyTrapDamage(resolvedDamage);

            LastTriggerDamage = resolvedDamage;
            target.TakeDamage(resolvedDamage);
            PlayHitAnimation();
            // 함정은 플레이어가 깔아 두고 잊는 물건이라, 언제 걸렸는지 소리로 짚어 줘야 한다.
            _01.Code.Audio.GameSfxPlayer.Play(_01.Code.Audio.GameSfxCue.Trap, transform.position);
            TryApplyInjury(target, targetComponent);

            // 발동한 만큼 닳는다. 다 닳으면 Building이 알아서 부서뜨린다.
            if (wearPerTrigger > 0)
                TakeBuildingDamage(wearPerTrigger);

            return true;
        }
        
        /// <summary>
        /// 함정을 해체할 줄 아는 상대면 피해 대신 함정이 부서진다.
        ///
        /// 확률을 두지 않았다. 공병이 지나가면 반드시 하나가 사라져야 플레이어가
        /// "저건 먼저 잡아야 한다"고 읽는다. 확률이면 운이 나빠서 뚫린 것처럼 보인다.
        /// </summary>
        private bool TryDisarm(Component targetComponent)
        {
            if (targetComponent == null)
                return false;

            var enemy = targetComponent.GetComponentInParent<_01.Code.Enemies.Enemy>();
            if (enemy == null || enemy.Data == null || !enemy.Data.DisarmsTraps)
                return false;

            LastTriggerDamage = 0;
            PlayHitAnimation();
            _01.Code.Audio.GameSfxPlayer.Play(_01.Code.Audio.GameSfxCue.Trap, transform.position);

            // 함정이 조용히 사라지면 무슨 일이 났는지 모른다. 해체한 쪽에 글자를 띄운다.
            var combatant = enemy.GetComponent<Combatant>();
            if (combatant != null)
                combatant.ShowCounterplayFeedback("함정 해체", 0, false, new Color(0.65f, 0.72f, 0.85f));

            BreakBuilding();
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
