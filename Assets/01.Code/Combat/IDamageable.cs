namespace _01.Code.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(int damage);
    }
}
