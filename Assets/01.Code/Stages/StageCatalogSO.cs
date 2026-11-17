using UnityEngine;

namespace Code.Stages
{
    [CreateAssetMenu(menuName = "Defence/Stages/Catalog")]
    public sealed class StageCatalogSO : ScriptableObject
    {
        [field: SerializeField] public bool IsTrapCampaign { get; private set; }
        [field: SerializeField, Range(0.1f, 1f)] public float HeavyPushMultiplier { get; private set; } = 1f;
        [field: SerializeField, Range(0.1f, 1f)] public float BossPushMultiplier { get; private set; } = 1f;
        public bool UsesTrapControls => IsTrapCampaign;
        [field: SerializeField] public StageDefinitionSO[] Stages { get; private set; }
        [field: SerializeField] public StageOption[] Options { get; private set; }
        [field: SerializeField, Min(1)] public int UpgradeBaseCost { get; private set; }
        [field: SerializeField, Min(1)] public int UpgradeLimit { get; private set; }
        [field: SerializeField, Min(0)] public float DamagePerUpgrade { get; private set; }
        [field: SerializeField, Min(1)] public int BaseLoadoutSlots { get; private set; } = 4;
        [field: SerializeField] public string[] SlotRewardStages { get; private set; } = System.Array.Empty<string>();

        [field: Header("실패 보상 (임시 값, 2026-11-02 사용자 결정)")]
        [field: SerializeField, Min(0), Tooltip("코어 0 패배 때 끝까지 막은 웨이브 하나당 강화 재화.")]
        public int FailureRewardPerWave { get; private set; } = 1;
        [field: SerializeField, Range(0f, 1f), Tooltip("실패 보상 상한 = 초회 클리어 보상 × 이 비율(내림).")]
        public float FailureRewardCapRatio { get; private set; } = 0.5f;

        /// <summary>아직 깨지 못한 스테이지에서 코어 0으로 졌을 때 줄 강화 재화.</summary>
        public int FailureReward(StageDefinitionSO definition, int defendedWaves)
        {
            if (definition == null || defendedWaves <= 0)
                return 0;
            int cap = Mathf.FloorToInt(Mathf.Max(0, definition.ClearReward) * FailureRewardCapRatio);
            return Mathf.Clamp(defendedWaves * FailureRewardPerWave, 0, cap);
        }

        [field: Header("유물 (임시 값, 2026-10-29 사용자 결정)")]
        [field: SerializeField] public StageArtifact[] Artifacts { get; private set; } = System.Array.Empty<StageArtifact>();
        [field: SerializeField] public Code.Artifacts.ArtifactComboCatalogSO ArtifactCombos { get; private set; }
        [field: SerializeField, Min(0)] public int ArtifactSlots { get; private set; } = 2;
        [field: SerializeField, Min(1)] public int ArtifactChoices { get; private set; } = 3;
        [field: SerializeField, Min(0), Tooltip("고립 늑대 부적: 이 칸 수 안에 다른 부하가 없어야 한다.")]
        public float LoneAllyRadius { get; private set; } = 2f;

        [field: Header("지형 칸 (임시 값, 2026-11-02 사용자 결정)")]
        [field: SerializeField] public StageTerrainRule[] TerrainRules { get; private set; } = System.Array.Empty<StageTerrainRule>();

        [field: Header("적 역할 규칙 (임시 값, 2026-11-02 사용자 결정)")]
        [field: SerializeField, Tooltip("바리케이드를 때릴 때 공격력이 커지는 적(공병).")]
        public Code.Enemies.EnemyDataSO[] BarricadeBreakers { get; private set; } = System.Array.Empty<Code.Enemies.EnemyDataSO>();
        [field: SerializeField, Min(1f)] public float BarricadeBreakerMultiplier { get; private set; } = 3f;
        [field: SerializeField, Tooltip("적을 맞히면 이동을 늦추는 함정(속박 수정).")]
        public Code.Buildings.BuildingDataSO[] SlowingTraps { get; private set; } = System.Array.Empty<Code.Buildings.BuildingDataSO>();
        [field: SerializeField, Range(0.05f, 1f)] public float TrapSlowMultiplier { get; private set; } = 0.6f;
        [field: SerializeField, Min(0f)] public float TrapSlowSeconds { get; private set; } = 2f;

        public bool IsBarricadeBreaker(Code.Enemies.EnemyDataSO enemy) =>
            enemy != null && BarricadeBreakers != null && System.Array.IndexOf(BarricadeBreakers, enemy) >= 0;

        public bool IsSlowingTrap(Code.Buildings.BuildingDataSO trap) =>
            trap != null && SlowingTraps != null && System.Array.IndexOf(SlowingTraps, trap) >= 0;

        [field: Header("전투 중 함정 운용 (임시 값, 2026-11-03 사용자 결정)")]
        [field: SerializeField, Min(1), Tooltip("판 안 강화 최고 단계. 설치하면 1단계다.")]
        public int TrapMaxLevel { get; private set; } = 3;
        [field: SerializeField, Min(0f), Tooltip("다음 단계 비용 = 설치비 × 이 비율 × 현재 단계(올림).")]
        public float TrapLevelCostRatio { get; private set; } = 0.75f;
        [field: SerializeField, Min(0f), Tooltip("단계마다 더하는 피해 배율.")]
        public float TrapLevelDamage { get; private set; } = 0.5f;
        [field: SerializeField, Range(0f, 1f), Tooltip("전투 중 철거는 스테이지 철거 환급률에 이 비율을 더 곱한다.")]
        public float FightingRefundRatio { get; private set; } = 0.5f;

        /// <summary>함정만 웨이브 중에도 설치·강화·철거한다. 바리케이드·부하는 준비 단계 전용(2026-11-03 사용자 결정, 함정 본편만).</summary>
        public bool EditsTrapsInWaves => IsTrapCampaign;

        public int TrapLevelCost(int installCost, int level) => Mathf.CeilToInt(Mathf.Max(0, installCost) * TrapLevelCostRatio * level);

        public float TrapLevelMultiplier(int level) => 1f + TrapLevelDamage * Mathf.Max(0, level - 1);

        [field: Header("스테이지 상인 (임시 값, 2026-11-02 사용자 결정)")]
        [field: SerializeField] public StageMerchantItem[] MerchantItems { get; private set; } = System.Array.Empty<StageMerchantItem>();

        /// <summary>
        /// 스테이지에서 정예로 보는 적(2026-11-01 사용자 결정). 일시정지 잠금·등장 안내·준비 예고가 이 목록만 본다.
        /// 등급만 보면 일반 검사·성기사도 정예와 같은 4라 따로 둔다.
        /// </summary>
        [field: Header("정예")]
        [field: SerializeField] public Code.Enemies.EnemyDataSO[] Elites { get; private set; } = System.Array.Empty<Code.Enemies.EnemyDataSO>();

        public bool IsElite(Code.Enemies.EnemyDataSO enemy)
        {
            if (enemy == null || Elites == null)
                return false;
            foreach (Code.Enemies.EnemyDataSO elite in Elites)
                if (elite == enemy)
                    return true;
            return false;
        }

        public StageArtifact FindArtifact(string id)
        {
            if (string.IsNullOrEmpty(id) || Artifacts == null)
                return null;
            foreach (StageArtifact artifact in Artifacts)
                if (artifact != null && artifact.Id == id && artifact.Artifact != null)
                    return artifact;
            return null;
        }

        /// <summary>해금 조건 스테이지의 화면 이름. 카탈로그에 아직 없는 스테이지는 ID를 그대로 쓴다.</summary>
        public string StageLabel(string id)
        {
            for (int i = 0; i < Stages.Length; i++)
                if (Stages[i] != null && Stages[i].Id == id)
                    return $"{i + 1} 스테이지";
            return id;
        }
    }
}
