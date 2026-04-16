using _01.Code.Core;
using _01.Code.Cost;
using _01.Code.Events;
using _01.Code.Manager;
using _01.Code.Tiles;

namespace _01.Code.Buildings
{
    public class CollectBattleTownBuildingBehavior : IBattleTownBuildingBehavior
    {
        private BattleTileBuildingDataSO _battleData;
        private GameEventChannelSO _waveEventChannel;
        private CostManager _costManager;
        private bool _isActive;

        public void Bind(TownTileObjectDataSO data)
        {
            _battleData = data as BattleTileBuildingDataSO;
        }

        public void Activate()
        {
            if (_isActive || _battleData == null)
            {
                return;
            }

            WaveManager waveManager = GameManager.Instance?.GetManager<WaveManager>();
            _costManager = GameManager.Instance?.GetManager<CostManager>();
            _waveEventChannel = waveManager != null ? waveManager.WaveEventChannel : null;
            _waveEventChannel?.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
            _waveEventChannel?.AddListener<WaveClearedEvent>(HandleWaveClearedEvent);
            _isActive = true;
        }

        public void Deactivate()
        {
            if (!_isActive)
            {
                return;
            }

            _waveEventChannel?.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
            _waveEventChannel = null;
            _isActive = false;
        }

        private void HandleWaveClearedEvent(WaveClearedEvent _)
        {
            CostDefinitionSO costType = _battleData != null
                ? _battleData.ResolveCollectCostType()
                : null;
            if (_battleData == null ||
                costType == null ||
                _battleData.CollectAmountPerWave <= 0 ||
                _costManager == null)
            {
                return;
            }

            _costManager.Add(costType, _battleData.CollectAmountPerWave);
        }
    }
}
