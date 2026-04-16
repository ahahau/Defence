using _01.Code.Core;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class BattleTownBuilding : TownTileObject
    {
        [field: SerializeField] public int level { get; private set; } = 1;

        private IBattleTownBuildingBehavior _runtimeBehavior;

        protected override void Start()
        {
            base.Start();
            _runtimeBehavior?.Activate();
        }

        protected virtual void OnDestroy()
        {
            _runtimeBehavior?.Deactivate();
        }

        public override void BindData(TownTileObjectDataSO data)
        {
            base.BindData(data);
            RefreshRuntimeState();
        }

        private void RefreshRuntimeState()
        {
            BattleTileBuildingDataSO battleData = Data as BattleTileBuildingDataSO;
            level = ResolveLevel(battleData);
            ReplaceBehavior(CreateBehavior(battleData));
        }

        private int ResolveLevel(BattleTileBuildingDataSO battleData)
        {
            if (battleData == null || battleData.UpgradeData == null)
            {
                return 1;
            }

            int levelIndex = battleData.UpgradeData.GetLevelIndex(battleData);
            return levelIndex >= 0 ? levelIndex + 1 : 1;
        }

        private IBattleTownBuildingBehavior CreateBehavior(BattleTileBuildingDataSO battleData)
        {
            if (battleData == null)
            {
                return new NullBattleTownBuildingBehavior();
            }

            return battleData.Role switch
            {
                BattleBuildingRole.Collect => new CollectBattleTownBuildingBehavior(),
                _ => new NullBattleTownBuildingBehavior()
            };
        }

        private void ReplaceBehavior(IBattleTownBuildingBehavior behavior)
        {
            _runtimeBehavior?.Deactivate();
            _runtimeBehavior = behavior;
            _runtimeBehavior?.Bind(Data);
            if (Application.isPlaying)
            {
                _runtimeBehavior?.Activate();
            }
        }
    }
}
