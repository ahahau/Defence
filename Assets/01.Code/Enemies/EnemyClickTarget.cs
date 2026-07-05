using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyClickTarget : MonoBehaviour
    {
        [SerializeField] private Enemy enemy;

        public Enemy Target => enemy;

        public void Initialize(Enemy targetEnemy)
        {
            enemy = targetEnemy;
        }

        private void Awake()
        {
            if (enemy == null)
                enemy = GetComponent<Enemy>();

            if (!TryGetComponent<Collider2D>(out _))
                Debug.LogError($"{nameof(EnemyClickTarget)} requires a Collider2D configured on the prefab.", this);
        }
    }
}
