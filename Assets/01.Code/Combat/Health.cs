using System;
using UnityEngine;

namespace _01.Code.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 20;

        private int currentHealth;

        public event Action<float> Changed;
        public event Action<int> Damaged;
        /// <summary>피해량 + 크리티컬 여부. 연출(데미지 텍스트/화면 효과)이 크리를 구분할 때 사용.</summary>
        public event Action<int, bool> DamagedDetailed;
        public static event Action<Health, int, bool> AnyDamaged;
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

        public void TakeDamage(int damage)
        {
            TakeDamage(damage, false);
        }

        public void TakeDamage(int damage, bool isCritical)
        {
            if (!IsAlive || damage <= 0)
                return;

            var appliedDamage = Mathf.Min(currentHealth, damage);
            currentHealth = Mathf.Max(0, currentHealth - appliedDamage);
            Changed?.Invoke(Ratio);
            Damaged?.Invoke(damage);
            DamagedDetailed?.Invoke(damage, isCritical);
            AnyDamaged?.Invoke(this, appliedDamage, isCritical);
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

        public void SetMaxHealth(int value, bool restoreToFull)
        {
            maxHealth = Mathf.Max(1, value);
            currentHealth = restoreToFull ? maxHealth : Mathf.Min(currentHealth, maxHealth);
            Changed?.Invoke(Ratio);
        }

        public void AddMaxHealth(int amount, bool healAddedHealth)
        {
            if (amount <= 0)
                return;

            maxHealth += amount;
            // 사망(체력 0) 상태에선 최대치만 늘고 현재 체력은 회복되지 않는다(아티팩트로 부활 방지).
            if (healAddedHealth && IsAlive)
                currentHealth += amount;

            currentHealth = Mathf.Min(currentHealth, maxHealth);
            Changed?.Invoke(Ratio);
        }

        private float Ratio => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }
}
