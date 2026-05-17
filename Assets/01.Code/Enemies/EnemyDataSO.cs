using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Defence/Enemy")]
    public class EnemyDataSO : EntityDataSO
    {
        [field: SerializeField]
        public string Name { get; private set; } = "Enemy";
    }
}
