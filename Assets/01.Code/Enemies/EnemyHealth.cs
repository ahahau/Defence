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
    }
}
