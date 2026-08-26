using System;
using System.Collections.Generic;
using _01.Code.Units;
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

        [Header("편성 전력")]
        [SerializeField, Min(1), Tooltip("고용가 이만큼마다 전력 1. 값이 작을수록 비싼 부하가 더 크게 쳐진다.")]
        private int costPerPower = 10;

        [SerializeField, Range(0f, 1f), Tooltip("피로 100인 부하가 잃는 전력 비율. 0.6이면 탈진한 부하는 전력의 40%만 낸다.")]
        private float fatiguePowerPenalty = 0.6f;

        [SerializeField, Min(1), Tooltip("한 작전에 편성할 수 있는 최대 인원")]
        private int maxPartySize = 3;

        [Header("보상")]
        [SerializeField, Min(0f), Tooltip("일차마다 보상에 더해지는 비율. 0.1이면 10일차에 기준 보상의 1.9배.")]
        private float rewardGrowthPerDay = 0.1f;

        [SerializeField, Min(0), Tooltip("편성 인원 한 명마다 붙는 추가 보상(일차 보정을 함께 받는다)")]
        private int rewardBonusPerUnit = 8;

        [SerializeField, Range(0f, 1f), Tooltip("실패했을 때 회수하는 보상 비율")]
        private float failureRewardRatio = 0.33f;

        [Header("귀환")]
        [SerializeField, Min(0f), Tooltip("성공하고 돌아온 부하에게 쌓이는 피로")]
        private float successFatigue = 24f;

        [SerializeField, Min(0f), Tooltip("실패하고 돌아온 부하에게 쌓이는 피로")]
        private float failureFatigue = 42f;

        [SerializeField, Range(0, 100), Tooltip("작전 성공 시 오르는 장악도")]
        private int conquestPerSuccess = 25;

        public IReadOnlyList<ExpeditionVillageEntry> Villages => villages;

        public int Count => villages?.Count ?? 0;
        public int MaxPartySize => Mathf.Max(1, maxPartySize);
        public float SuccessFatigue => Mathf.Max(0f, successFatigue);
        public float FailureFatigue => Mathf.Max(0f, failureFatigue);
        public int ConquestPerSuccess => Mathf.Clamp(conquestPerSuccess, 0, 100);

        public ExpeditionVillageEntry Get(int index) =>
            villages != null && index >= 0 && index < villages.Count ? villages[index] : null;

        public void ReplaceEntries(List<ExpeditionVillageEntry> values)
        {
            villages = values ?? new List<ExpeditionVillageEntry>();
        }

        /// <summary>
        /// 부하 한 명이 작전에 보태는 전력. 값어치가 클수록 크게 치되 지쳐 있으면 그만큼 깎는다.
        /// 인원수만 세면 누구를 보내든 같아져서 편성이 결정이 되지 않는다.
        /// </summary>
        public int GetUnitPower(UnitDataSO unit, UnitConditionState condition)
        {
            if (unit == null)
                return 0;

            var basePower = Mathf.Max(1, Mathf.CeilToInt(unit.Cost / (float)Mathf.Max(1, costPerPower)));
            var worn = 1f - Mathf.Clamp01(condition.Fatigue / 100f) * fatiguePowerPenalty;
            return Mathf.Max(0, Mathf.RoundToInt(basePower * worn));
        }

        /// <summary>
        /// 전력이 난이도에 닿으면 확정 성공, 모자라면 모자란 만큼 확률이 떨어진다.
        /// 예전처럼 고정 확률로 뒤집히면 난이도를 올려도 결과가 달라지지 않는다.
        /// </summary>
        public static float GetSuccessChance(int power, int difficulty)
        {
            if (difficulty <= 0)
                return 1f;

            return Mathf.Clamp01(power / (float)difficulty);
        }

        /// <summary>일차가 오르면 웨이브 보상도 함께 오르므로 원정 보상도 따라 올라야 후반에 의미가 남는다.</summary>
        public int GetReward(ExpeditionVillageEntry village, int day, int partySize, bool success)
        {
            if (village == null)
                return 0;

            var baseReward = village.Reward + rewardBonusPerUnit * Mathf.Max(0, partySize);
            var scaled = baseReward * (1f + rewardGrowthPerDay * Mathf.Max(0, day - 1));
            if (!success)
                scaled *= failureRewardRatio;

            return Mathf.Max(0, Mathf.RoundToInt(scaled));
        }
    }

    [Serializable]
    public sealed class ExpeditionVillageEntry
    {
        [SerializeField, Tooltip("지도와 버튼에 표시할 이름")]
        private string displayName = "새 마을";

        [SerializeField, TextArea, Tooltip("작전 목적. 상세 패널에 이름 아래로 붙는다.")]
        private string purpose = string.Empty;

        [SerializeField, Min(0), Tooltip("1일차 기준 보상 금화. 일차가 오르면 카탈로그의 성장률만큼 함께 오른다.")]
        private int reward = 50;

        [SerializeField, Min(1), Tooltip("편성 전력이 이 값 이상이면 확정 성공. 모자라면 전력/난이도 비율만큼의 확률이 된다.")]
        private int difficulty = 4;

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
