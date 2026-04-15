using System;
using System.Collections.Generic;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.Buildings
{
    [Serializable]
    public class BattleBuildingUpgradeEntry
    {
        [field: SerializeField] public BattleTileBuildingDataSO BuildingData { get; private set; }
        // 현재 레벨에서 다음 레벨로 올라갈 때 지불하는 비용입니다.
        [field: SerializeField] public List<TownTileObjectDataSO.Entry> UpgradeCosts { get; private set; }
    }

    [CreateAssetMenu(fileName = "BattleBuildingUpgrade", menuName = "SO/Battle/Building Upgrade", order = 2)]
    public class BattleBuildingUpgradeLineSO : ScriptableObject
    {
        // 이 라인에 속한 건물을 처음 설치할 때 사용하는 비용입니다.
        [field: SerializeField] public List<TownTileObjectDataSO.Entry> BuildCosts { get; private set; } = new();
        // 각 엔트리는 "현재 단계 데이터"와 "다음 단계로 가는 비용"을 함께 가집니다.
        [field: SerializeField] public List<BattleBuildingUpgradeEntry> Levels { get; private set; } = new();

        public BattleTileBuildingDataSO GetNext(BattleTileBuildingDataSO current)
        {
            // 현재 단계의 바로 다음 엔트리를 다음 업그레이드 대상으로 사용합니다.
            int index = GetLevelIndex(current);
            if (index < 0 || index >= Levels.Count - 1)
            {
                return null;
            }

            return Levels[index + 1] != null ? Levels[index + 1].BuildingData : null;
        }

        public List<TownTileObjectDataSO.Entry> GetBuildCost(BattleTileBuildingDataSO current)
        {
            return BuildCosts;
        }

        public List<TownTileObjectDataSO.Entry> GetUpgradeCost(BattleTileBuildingDataSO current)
        {
            // 업그레이드 비용은 "다음 단계"가 아니라 "현재 단계 엔트리"에서 찾습니다.
            int index = GetLevelIndex(current);
            if (index < 0 || index >= Levels.Count)
            {
                return null;
            }

            return Levels[index] != null ? Levels[index].UpgradeCosts : null;
        }

        public int GetLevelIndex(BattleTileBuildingDataSO current)
        {
            if (current == null)
            {
                return -1;
            }

            // 라인 안에서 현재 건물 SO가 몇 번째 단계인지 찾습니다.
            for (int i = 0; i < Levels.Count; i++)
            {
                if (Levels[i] != null && Levels[i].BuildingData == current)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
