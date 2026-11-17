using System;
using GameLib.Entity.Stats;
using UnityEngine;

namespace GameLib.Entity
{
    /// <summary>
    /// 체력을 들고 있는 모듈. 최대 체력은 스탯 표(<see cref="StatIndex.MaxHealth"/>)가 진짜 값이고,
    /// 스탯이 없는 상대(코드로 만든 테스트 요원 등)에게만 직렬화 필드가 폴백으로 쓰인다.
    ///
    /// Core에 있는 이유는 하나다 — <see cref="Entity"/>가 체력을 알아야 하는데,
    /// 예전처럼 Combat에 있으면 Core가 Combat을 참조하게 되어 의존 방향이 역류한다.
    /// 전투 쪽 사정(피격 방송 등)은 Combat의 파생 클래스가 맡는다.
    ///
    /// 직접 붙이는 컴포넌트가 아니라서 abstract다. 실제로 프리팹에 붙는 것은 Combat.Health다.
    /// </summary>
    public abstract class HealthModule : MonoBehaviour, IModule, IAfterInitModule
    {
        [SerializeField] private int maxHealth = 20;

        private int currentHealth;
        private IStatModule stats;
        private StatSO maxHealthStat;
        private bool suppressStatCallback;

        public event Action<float> Changed;
        public event Action<int> Damaged;
        /// <summary>피해량 + 크리티컬 여부. 연출(데미지 텍스트/화면 효과)이 크리를 구분할 때 사용.</summary>
        public event Action<int, bool> DamagedDetailed;
        /// <summary>실제 회복된 양(최대치 초과분 제외). 연출(회복 텍스트/이펙트)용.</summary>
        public event Action<int> Healed;
        public bool IsAlive => currentHealth > 0;
        public float CurrentRatio => Ratio;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            currentHealth = maxHealth;
            Changed?.Invoke(Ratio);
        }

        public void Initialize(ModuleOwner owner)
        {
            stats = owner.GetModule<IStatModule>();
        }

        public void AfterInitialize()
        {
            if (maxHealthStat != null)
                maxHealthStat.ValueChanged -= HandleMaxHealthStatChanged;

            maxHealthStat = null;
            if (stats == null || !stats.TryGetStat(StatIndex.MaxHealth, out maxHealthStat))
                return;

            maxHealthStat.ValueChanged += HandleMaxHealthStatChanged;
            ApplyMaximum(maxHealthStat.Value, true, true);
        }

        private void OnDestroy()
        {
            if (maxHealthStat != null)
                maxHealthStat.ValueChanged -= HandleMaxHealthStatChanged;
        }

        /// <summary>
        /// 피해가 실제로 들어간 뒤 불린다. 전투 쪽 전역 방송처럼 Core가 알 필요 없는 일을
        /// 파생 클래스가 여기에 얹는다.
        /// </summary>
        protected virtual void OnDamageApplied(int appliedDamage, bool isCritical)
        {
        }

        /// <summary>
        /// 들어온 피해를 실제로 깎기 전에 고친다. 피해는 평타·스킬·함정 모두 여기를 지나므로,
        /// "지금은 덜 아프다" 같은 상태를 파생 클래스가 한 곳에서 걸 수 있다.
        /// </summary>
        protected virtual int ModifyIncomingDamage(int damage) => damage;

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, false);
        }

        public void TakeDamage(int damage, bool isCritical)
        {
            if (!IsAlive || damage <= 0)
                return;

            damage = ModifyIncomingDamage(damage);
            if (damage <= 0)
                return;

            var appliedDamage = Mathf.Min(currentHealth, damage);
            currentHealth = Mathf.Max(0, currentHealth - appliedDamage);
            Changed?.Invoke(Ratio);
            Damaged?.Invoke(damage);
            DamagedDetailed?.Invoke(damage, isCritical);
            OnDamageApplied(appliedDamage, isCritical);
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0)
                return;

            var healed = Mathf.Min(maxHealth, currentHealth + amount) - currentHealth;
            if (healed <= 0)
                return;

            currentHealth += healed;
            Changed?.Invoke(Ratio);
            Healed?.Invoke(healed);
        }

        public void Restore(int amount)
        {
            // 사망(체력 0) 상태는 일반 회복으로 살릴 수 없다. 부활은 RestoreToFull(명시적 회복 시스템)만 가능.
            if (!IsAlive || amount <= 0)
                return;

            var healed = Mathf.Min(maxHealth, currentHealth + amount) - currentHealth;
            if (healed <= 0)
                return;

            currentHealth += healed;
            Changed?.Invoke(Ratio);
            Healed?.Invoke(healed);
        }

        /// <summary>명시적 부활/완전 회복. 기절 유닛 회복(Unit.RecoverFromIncapacitated) 전용 —
        /// 일반 힐(Heal/Restore)은 사망 상태를 살릴 수 없다.</summary>
        public void RestoreToFull()
        {
            currentHealth = maxHealth;
            Changed?.Invoke(Ratio);
        }

        public void SetCurrentRatio(float ratio)
        {
            currentHealth = Mathf.Clamp(Mathf.RoundToInt(maxHealth * Mathf.Clamp01(ratio)), 0, maxHealth);
            Changed?.Invoke(Ratio);
        }

        public void SetMaxHealth(int value, bool restoreToFull)
        {
            value = Mathf.Max(1, value);
            if (maxHealthStat == null)
            {
                ApplyMaximum(value, false, restoreToFull);
                return;
            }

            MutateMaxHealthStat(() => maxHealthStat.BaseValue = value, false, restoreToFull);
        }

        public void AddMaxHealth(int amount, bool healAddedHealth)
        {
            if (amount == 0)
                return;

            if (maxHealthStat == null)
            {
                ApplyMaximum(maxHealth + amount, healAddedHealth, false);
                return;
            }

            MutateMaxHealthStat(() => maxHealthStat.BaseValue += amount, healAddedHealth, false);
        }

        /// <summary>
        /// 최대 체력 보정을 출처별로 갱신한다. 같은 key로 다시 호출하면 이전 보정만 교체되고,
        /// 다른 시스템이 붙인 웨이브·보스·유물 보정은 유지된다.
        /// </summary>
        public void SetMaxHealthModifier(
            object key,
            float additive,
            float multiplier = 1f,
            bool healAddedHealth = true)
        {
            if (key == null || maxHealthStat == null)
                return;

            MutateMaxHealthStat(
                () => maxHealthStat.SetModifier(key, additive, Mathf.Max(0.05f, multiplier)),
                healAddedHealth,
                false);
        }

        public void RemoveMaxHealthModifier(object key)
        {
            if (key == null || maxHealthStat == null)
                return;

            MutateMaxHealthStat(() => maxHealthStat.RemoveModifier(key), false, false);
        }

        private void MutateMaxHealthStat(Action mutation, bool healAddedHealth, bool restoreToFull)
        {
            suppressStatCallback = true;
            try
            {
                mutation();
            }
            finally
            {
                suppressStatCallback = false;
            }

            ApplyMaximum(maxHealthStat.Value, healAddedHealth, restoreToFull);
        }

        private void HandleMaxHealthStatChanged(StatSO stat, float current, float previous)
        {
            if (!suppressStatCallback)
                ApplyMaximum(current, true, false);
        }

        private void ApplyMaximum(float value, bool healAddedHealth, bool restoreToFull)
        {
            var previousMaximum = maxHealth;
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(value));

            if (restoreToFull)
            {
                currentHealth = maxHealth;
            }
            else if (healAddedHealth && IsAlive && maxHealth > previousMaximum)
            {
                currentHealth += maxHealth - previousMaximum;
            }

            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            Changed?.Invoke(Ratio);
        }

        private float Ratio => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }
}
