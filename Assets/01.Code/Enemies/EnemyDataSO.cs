using _01.Code.BT;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Enemies
{
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Defence/Enemy")]
    public class EnemyDataSO : EntityDataSO
    {
        [field: SerializeField]
        public string Name { get; private set; } = "Enemy";

        [field: SerializeField, Tooltip("전투 역할. 전열(Tank/Melee)/후열(Ranged)/지원(Support).")]
        public BattleRole Role { get; private set; } = BattleRole.Melee;

        [field: SerializeField, Tooltip("이 적 종류의 전용 프리팹. 비어 있으면 WaveManager의 기본 프리팹 사용.")]
        public Enemy Prefab { get; private set; }

        [field: SerializeField, Min(0)]
        public int Fear { get; private set; }

        [field: SerializeField, Min(0)]
        public int Greed { get; private set; }

        [field: SerializeField, Min(1)]
        public int MaxHealth { get; private set; } = 10;

        [field: SerializeField, Min(1)]
        public int AttackDamage { get; private set; } = 1;

        [field: SerializeField, Min(0.05f)]
        public float AttackInterval { get; private set; } = 1f;

        [field: SerializeField]
        public Sprite IdleSprite { get; private set; }

        [field: SerializeField]
        public Sprite AttackSprite { get; private set; }

        [field: SerializeField]
        public Sprite DefeatedSprite { get; private set; }
    }
}
