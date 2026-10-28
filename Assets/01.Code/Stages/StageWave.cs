using System;
using Code.Enemies;
using UnityEngine;

namespace Code.Stages
{
    [System.Serializable]
    public sealed class StageWave
    {
        [field: SerializeField] public EnemyDataSO[] Enemies { get; private set; }
        [field: SerializeField, Min(0.1f)] public float SpawnInterval { get; private set; }
        [field: SerializeField] public EnemyDataSO Boss { get; private set; }
        [field: SerializeField] public EnemyDataSO[] BossEscorts { get; private set; } = Array.Empty<EnemyDataSO>();
        [field: SerializeField, Min(1f)] public float BossHealthMultiplier { get; private set; } = 6f;
        [field: SerializeField, Min(1f)] public float BossAttackMultiplier { get; private set; } = 2f;
        [field: SerializeField, Min(1f)] public float BossScale { get; private set; } = 1.6f;
        /// <summary>일반 적 순서와 짝인 특수 규칙. 짧거나 비면 나머지는 규칙 없음.</summary>
        [field: SerializeField] public StageEnemyRule[] Rules { get; private set; } = Array.Empty<StageEnemyRule>();

        public bool HasBoss => Boss != null;

        public bool HasRules
        {
            get
            {
                if (Rules != null)
                    foreach (StageEnemyRule rule in Rules)
                        if (rule != StageEnemyRule.None)
                            return true;
                return false;
            }
        }

        /// <summary>일반 적 다음에 보스, 그 뒤 호위가 나온다.</summary>
        public int Count => (Enemies?.Length ?? 0) + (HasBoss ? 1 + (BossEscorts?.Length ?? 0) : 0);

        public StageWave(EnemyDataSO[] enemies, float interval, StageEnemyRule[] rules = null)
        {
            Enemies = enemies;
            SpawnInterval = interval;
            Rules = rules ?? Array.Empty<StageEnemyRule>();
        }

        public StageWave(EnemyDataSO[] enemies, float interval, EnemyDataSO boss, EnemyDataSO[] escorts,
            StageEnemyRule[] rules = null)
            : this(enemies, interval, rules)
        {
            Boss = boss;
            BossEscorts = escorts ?? Array.Empty<EnemyDataSO>();
        }

        public EnemyDataSO At(int index, out bool isBoss)
        {
            int regular = Enemies?.Length ?? 0;
            isBoss = HasBoss && index == regular;
            if (index < regular)
                return Enemies[index];
            return isBoss ? Boss : BossEscorts[index - regular - 1];
        }

        /// <summary>보스·호위는 규칙을 받지 않는다.</summary>
        public StageEnemyRule RuleAt(int index) =>
            Rules != null && index < Rules.Length && index < (Enemies?.Length ?? 0) ? Rules[index] : StageEnemyRule.None;
    }
}
