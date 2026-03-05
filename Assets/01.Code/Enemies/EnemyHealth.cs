using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyHealth : EntityHealth
    {
        public void Initialize(int health)
        {
            baseHealth = health;
        }
        public void SetHealth(int health)
        {
            currentHealth += health;
        }
    }
}