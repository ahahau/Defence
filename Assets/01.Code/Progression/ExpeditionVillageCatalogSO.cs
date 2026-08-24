using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Progression
{
    /// <summary>
    /// 원정을 보낼 수 있는 마을 목록.
    /// 여기에는 변하지 않는 정의만 담는다. 장악도처럼 플레이 중 변하는 값은
    /// 런타임 상태로 따로 들고 있어야 에디터의 에셋이 플레이 중에 오염되지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "SO/Progression/Expedition Village Catalog", fileName = "ExpeditionVillageCatalog")]
    public sealed class ExpeditionVillageCatalogSO : ScriptableObject
    {
        [SerializeField] private List<ExpeditionVillageEntry> villages = new();

        public IReadOnlyList<ExpeditionVillageEntry> Villages => villages;

        public int Count => villages?.Count ?? 0;

        public ExpeditionVillageEntry Get(int index) =>
            villages != null && index >= 0 && index < villages.Count ? villages[index] : null;

        public void ReplaceEntries(List<ExpeditionVillageEntry> values)
        {
            villages = values ?? new List<ExpeditionVillageEntry>();
        }
    }

    [Serializable]
    public sealed class ExpeditionVillageEntry
    {
        [SerializeField, Tooltip("지도와 버튼에 표시할 이름")]
        private string displayName = "새 마을";

        [SerializeField, TextArea, Tooltip("작전 목적. 상세 패널에 이름 아래로 붙는다.")]
        private string purpose = string.Empty;

        [SerializeField, Min(0), Tooltip("성공 시 기준 보상 금화. 실패하면 이 값의 1/3만 회수한다.")]
        private int reward = 50;

        [SerializeField, Min(0), Tooltip("편성 전력이 이 값 이상이면 확정 성공한다.")]
        private int difficulty = 1;

        [SerializeField, Range(0, 100), Tooltip("판이 시작될 때의 장악도")]
        private int startingConquest;

        public string DisplayName => displayName;
        public string Purpose => purpose;
        public int Reward => reward;
        public int Difficulty => difficulty;
        public int StartingConquest => startingConquest;

        public ExpeditionVillageEntry() { }

        public ExpeditionVillageEntry(string displayName, string purpose, int reward, int difficulty, int startingConquest = 0)
        {
            this.displayName = displayName;
            this.purpose = purpose;
            this.reward = reward;
            this.difficulty = difficulty;
            this.startingConquest = startingConquest;
        }
    }
}
