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

        /// <summary>
        /// 일반 적을 순서대로 이 수만큼 끊어 한 파티로 묶는다(2026-11-01 사용자 결정, 임시 3). 보스와 호위는 크기와 상관없이 한 파티다.
        /// 0이나 1이면 파티 없이 각자 움직인다(예전 동작).
        /// </summary>
        [field: SerializeField, Min(0)] public int PartySize { get; private set; } = 3;

        public bool HasBoss => Boss != null;

        private int GroupSize => PartySize > 1 ? PartySize : 1;
        private int RegularParties => ((Enemies?.Length ?? 0) + GroupSize - 1) / GroupSize;

        /// <summary>등장 순서 번호가 속한 파티 번호. 문 배정에 쓴다. 파티가 없으면 등장 순서 그대로다.</summary>
        public int PartyIndex(int index)
        {
            int regular = Enemies?.Length ?? 0;
            if (index < regular)
                return index / GroupSize;
            // 각자 움직이면 보스·호위도 한 명씩 문을 돈다(예전 동작)
            return GroupSize > 1 ? RegularParties : regular + (index - regular);
        }

        public bool StartsParty(int index) => index == 0 || PartyIndex(index) != PartyIndex(index - 1);

        /// <summary>이 등장 순서 번호가 속한 파티의 인원.</summary>
        public int PartyLength(int index)
        {
            int party = PartyIndex(index);
            int length = 0;
            for (int i = 0; i < Count; i++)
                if (PartyIndex(i) == party)
                    length++;
            return length;
        }


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

        /// <summary>보스를 뺀 정예 수. 정예는 카탈로그 목록(<see cref="StageCatalogSO.Elites"/>)이 정한다.</summary>
        public int EliteCount(StageCatalogSO catalog)
        {
            int count = 0;
            if (catalog == null)
                return count;
            for (int i = 0; i < Count; i++)
                if (catalog.IsElite(At(i, out bool isBoss)) && !isBoss)
                    count++;
            return count;
        }

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
