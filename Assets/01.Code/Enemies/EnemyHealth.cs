using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyHealth : EntityHealth
    {
        public void Initialize(EnemyDataSO data, int level = 0)
        {
            baseHealth = data.Health + data.GrowthHealth * level;
            currentHealth = baseHealth;
        }

        protected override bool ShouldShowDamageText()
        {
            return true;
        }

        protected override Vector3 GetDamageTextPosition()
        {
            return transform.position + Vector3.up * 0.2f;
        }

        protected override void HandleDeath()
        {
            if (_entity is Enemy enemy)
            {
                enemy.ReturnToPool();
                return;
            }

            base.HandleDeath();
        }
    }
}
