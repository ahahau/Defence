using System;
using UnityEngine;

namespace _01.Code.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 10;

        private int currentHealth;

        public event Action<float> Changed;
        public event Action<int> Damaged;
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
            if (!IsAlive || damage <= 0)
                return;

            currentHealth = Mathf.Max(0, currentHealth - damage);
            Changed?.Invoke(Ratio);
            Damaged?.Invoke(damage);
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0)
                return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            Changed?.Invoke(Ratio);
        }

        public void Restore(int amount)
        {
            if (amount <= 0)
                return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            Changed?.Invoke(Ratio);
        }

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
            if (healAddedHealth)
                currentHealth += amount;

            currentHealth = Mathf.Min(currentHealth, maxHealth);
            Changed?.Invoke(Ratio);
        }

        private float Ratio => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }
}
