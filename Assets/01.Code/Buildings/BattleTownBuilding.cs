using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Tiles;

namespace _01.Code.Buildings
{
    public class BattleTownBuilding : TownTileObject
    {
        private GameEventChannelSO _waveEventChannel;
        private CostManager _costManager;

        protected override void Start()
        {
            base.Start();
            SubscribeWaveEvent();
        }

        protected virtual void OnDestroy()
        {
            UnsubscribeWaveEvent();
        }

        private void SubscribeWaveEvent()
        {
            BattleTileBuildingDataSO battleData = Data as BattleTileBuildingDataSO;
            if (battleData == null || battleData.Role != BattleBuildingRole.Collect)
            {
                return;
            }

            WaveManager waveManager = GameManager.Instance?.GetManager<WaveManager>();
            _costManager = GameManager.Instance?.GetManager<CostManager>();
            _waveEventChannel = waveManager != null ? waveManager.WaveEventChannel : null;
            _waveEventChannel?.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
            _waveEventChannel?.AddListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }

        private void UnsubscribeWaveEvent()
        {
            _waveEventChannel?.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }

        private void HandleWaveClearedEvent(WaveClearedEvent _)
        {
            BattleTileBuildingDataSO battleData = Data as BattleTileBuildingDataSO;
            CostDefinitionSO costType = battleData != null
                ? battleData.ResolveCollectCostType()
                : null;
            if (battleData == null ||
                battleData.Role != BattleBuildingRole.Collect ||
                costType == null ||
                battleData.CollectAmountPerWave <= 0 ||
                _costManager == null)
            {
                return;
            }

            _costManager.Add(costType, battleData.CollectAmountPerWave);
        }
    }
}
