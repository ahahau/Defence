using UnityEngine;

namespace _01.Code.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "SO/EnemyData", order = 0)]
    public class EnemyDataSO : ScriptableObject
    {
        [field:SerializeField] public Enemy EnemyPrefab { get; private set; }
        [field:SerializeField] public float Health { get; private set; } = 100f;
        [field:SerializeField] public float GrowthHealth { get; private set; } = 10f;
        [field: SerializeField] public float Damage { get; private set; } = 1f;
        
        [field: SerializeField] public float Speed { get; private set; } = 5f;
    }
}
