using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Manager;
using UnityEngine;
using CostType = _01.Code.Manager.CostType;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Buildings
{
    // 웨이브 끝났을때 자원 생성
    // 건물 업그레이드?
    public class CollectibleBuilding : Building
    {
        [SerializeField] private GameEventChannelSO waveEventChannel;
        [SerializeField] private GameEventChannelSO costEventChannel;
        
        [SerializeField] private CostType costType = CostType.Gold;
        
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
            costEventChannel.RaiseEvent(CostEvents.RefundCost.Initializer(costType, 10));
        }
    }
}