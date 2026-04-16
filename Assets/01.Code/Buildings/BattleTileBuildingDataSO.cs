using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Tiles;
using _01.Code.Cost;
using _01.Code.Core;
using UnityEngine;

namespace _01.Code.Buildings
{
    [CreateAssetMenu(fileName = "BattleTileBuildingData", menuName = "SO/Battle/Tile Building Data", order = 0)]
    public class BattleTileBuildingDataSO : TownTileObjectDataSO
    {
        // 어떤 자원/장애물 위에 지을 수 있는지 제한할 때 사용합니다.
        [field: SerializeField] public PlaceableEntity RequiredSourcePrefab { get; private set; }
        [field: SerializeField] public BattleBuildingRole Role { get; private set; } = BattleBuildingRole.None;
        // 배틀 건물의 설치/업그레이드 단계 정보는 이 라인 SO가 단일 소스입니다.
        [field: SerializeField] public BattleBuildingUpgradeLineSO UpgradeData { get; private set; }
        [field: SerializeField] public bool UseDefaultCost { get; private set; }
        [field: SerializeField] public CostDefinitionSO DefaultCollectCostType { get; private set; }
        [field: SerializeField] public CostDefinitionSO CollectCostType { get; private set; }
        [field: SerializeField] public int CollectAmountPerWave { get; private set; }

        public BattleTownBuilding BuildingPrefab
        {
            get { return Prefab as BattleTownBuilding; }
        }

        public override TownTileObjectDataSO GetResolvedNextUpgrade()
        {
            // 배틀 건물은 공통 NextUpgrade를 보지 않고 업그레이드 라인만 신뢰합니다.
            return UpgradeData != null ? UpgradeData.GetNext(this) : null;
        }

        public override List<TownTileObjectDataSO.Entry> GetResolvedBuildCosts()
        {
            // 설치 비용은 개별 단계 엔트리가 아니라 라인 본체에서 가져옵니다.
            return UpgradeData != null ? UpgradeData.GetBuildCost(this) : null;
        }

        public override List<TownTileObjectDataSO.Entry> GetResolvedUpgradeCosts()
        {
            // 업그레이드 비용은 현재 단계 엔트리에서 가져옵니다.
            return UpgradeData != null ? UpgradeData.GetUpgradeCost(this) : null;
        }

        public CostDefinitionSO ResolveCollectCostType()
        {
            return UseDefaultCost ? DefaultCollectCostType : CollectCostType;
        }
    }
}
