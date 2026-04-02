using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Units
{
    // 웨이브 끝났을때 자원 생성
    // 건물 업그레이드?
    public class CollectibleUnit : Unit
    {
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private CollectableUnitDataSo collectableUnitData;
        
        protected override void Start()
        {
            base.Start();
            waveEventChannel.AddListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }

        private void OnDestroy()
        {
            waveEventChannel.RemoveListener<WaveClearedEvent>(HandleWaveClearedEvent);
        }

        private void HandleWaveClearedEvent(WaveClearedEvent _)
        {
            CollectCost();
        }

        private void CollectCost()
        {
            if (collectableUnitData == null)
            {
                LogManager?.Building($"{name}: CollectableBuildingDataSO is missing.", LogLevel.Error);
                return;
            }

            costEventChannel.RaiseEvent(
                CostEvents.RefundCostEvent.Initializer(collectableUnitData.Type, collectableUnitData.GainCost));
        }
    }
}
