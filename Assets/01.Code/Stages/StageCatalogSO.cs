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

        [field: Header("유물 (임시 값, 2026-10-29 사용자 결정)")]
        [field: SerializeField] public StageArtifact[] Artifacts { get; private set; } = System.Array.Empty<StageArtifact>();
        [field: SerializeField] public Code.Artifacts.ArtifactComboCatalogSO ArtifactCombos { get; private set; }
        [field: SerializeField, Min(0)] public int ArtifactSlots { get; private set; } = 2;
        [field: SerializeField, Min(1)] public int ArtifactChoices { get; private set; } = 3;
        [field: SerializeField, Min(0), Tooltip("고립 늑대 부적: 이 칸 수 안에 다른 부하가 없어야 한다.")]
        public float LoneAllyRadius { get; private set; } = 2f;

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
