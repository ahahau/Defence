namespace _01.Code.Entities
{
    public interface IDamageable
    {
        public void ApplyDamage(float damage, Entity dealer);
    }
}