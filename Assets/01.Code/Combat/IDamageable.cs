using _01.Code.Entities;

namespace _01.Code.Combat
{
    public interface IDamageable
    {
        public void ApplyDamage(float damage, Entity dealer);
    }
}