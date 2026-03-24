using _01.Code.Core;
using _01.Code.Entities;
using _01.Code.Events;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Buildings
{
    // 웨이브 끝났을때 자원 생성
    // 건물 업그레이드?
    public class CollectibleBuilding : Building
    {
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private CollectableBuildingDataSO collectableBuildingData;
        
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
            if (collectableBuildingData == null)
            {
                GameManager.Instance.LogManager?.Building($"{name}: CollectableBuildingDataSO is missing.", LogLevel.Error);
                return;
            }

            costEventChannel.RaiseEvent(
                CostEvents.RefundCost.Initializer(collectableBuildingData.Type, collectableBuildingData.GainCost));
        }
    }
}
