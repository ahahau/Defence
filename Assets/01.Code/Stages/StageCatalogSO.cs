using UnityEngine;

namespace Code.Stages
{
    [CreateAssetMenu(menuName = "Defence/Stages/Catalog")]
    public sealed class StageCatalogSO : ScriptableObject
    {
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
