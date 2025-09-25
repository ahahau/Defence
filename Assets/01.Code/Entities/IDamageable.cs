namespace _01.Code.Entities
{
    public interface IDamageable
    {
        public void ApplyDamage(int damage, Entity dealer);
    }
}