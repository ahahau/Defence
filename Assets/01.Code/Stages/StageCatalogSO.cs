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
