using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using UnityEngine;
using CostType = _01.Code.Manager.CostType;

namespace _01.Code.Buildings
{
    // 웨이브 끝났을때 자원 생성
    // 건물 업그레이드?
    public class CollectibleBuilding : Building
    {
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        [SerializeField] private CollectableBuildingDataSO collectableBuildingData;
        [SerializeField] private CostType costType = CostType.Gold;
        [SerializeField] private int gainCost = 10;
        
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
            costEventChannel.RaiseEvent(CostEvents.RefundCost.Initializer(GetCostType(), GetGainCost()));
        }

        private CostType GetCostType()
        {
            return collectableBuildingData != null ? collectableBuildingData.Type : costType;
        }

        private int GetGainCost()
        {
            return collectableBuildingData != null ? collectableBuildingData.GainCost : gainCost;
        }
    }
}
