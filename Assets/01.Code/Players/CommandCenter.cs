using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Players
{
    public class CommandCenter : Entity, IDamageable
    {
        public void ApplyDamage(int damage, Entity dealer)
        {
            _hp -= damage;
            dealer.OnDeath?.Invoke();
        }
    }
}