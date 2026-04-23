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

        private void Awake()
        {
            currentHealth = maxHealth;
            Changed?.Invoke(Ratio);
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive)
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

        private float Ratio => maxHealth > 0 ? (float)currentHealth / maxHealth : 0f;
    }
}
