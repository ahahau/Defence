using System.Collections.Generic;
using Code.Core;
using Code.Enemies;
using Code.Events;
using Code.Manager;
using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Buildings
{
    /// <summary>
    /// 교회. 관을 끌고 온 파티가 공동 자금을 내고 쓰러진 동료를 되살린다.
    ///
    /// 부활비는 이 던전 주인의 수입이 된다. 대신 잡았던 적이 다시 일어서므로, 교회는 벌이와 위험을
    /// 맞바꾸는 시설이다. 손님이 머무는 시설처럼 이 방에는 유닛·함정을 둘 수 없다(데이터의 DwellSeconds).
    /// </summary>
    public sealed class Church : Building
    {
        [SerializeField] private GameEventChannelSO costEventChannel;

        /// <summary>되살릴 관을 모으는 임시 목록. 매번 새로 만들지 않는다.</summary>
        private readonly List<PartyCoffin> _coffinBuffer = new();
        private bool _reviving;

        /// <summary>
        /// 파티원이 교회에 들어왔다. 그 파티의 관을 하나씩, 공동 자금에서 값을 받고 되살린다.
        /// 돈이 모자라면 가진 만큼만 받고 낮은 체력으로 살린다.
        /// </summary>
        /// <returns>되살린 수.</returns>
        public int ReviveParty(int partyId, Node node)
        {
            // 되살아난 모험가가 이 방에 서면서 다시 여기로 들어온다. 그때는 이미 도는 중이므로 넘긴다.
            if (_reviving || !IsOperating || node == null || WaveManager.Current == null)
                return 0;

            _reviving = true;
            try
            {
                return ReviveAll(partyId, node);
            }
            finally
            {
                _reviving = false;
            }
        }

        private int ReviveAll(int partyId, Node node)
        {
            PartyCoffin.CollectFor(partyId, _coffinBuffer);
            var revived = 0;
            foreach (var coffin in _coffinBuffer)
            {
                if (coffin == null)
                    continue;

                var cost = ChurchRevivalRules.Cost(coffin.Level);
                var paid = PartyFundLedger.Active.Spend(partyId, cost);
                var ratio = ChurchRevivalRules.HealthRatio(paid, cost);
                var enemy = WaveManager.Current.ReviveFromCoffin(
                    coffin.Data, coffin.Level, partyId, coffin.Purpose, node, ratio);
                if (enemy == null)
                {
                    // 되살리지 못했으면 받은 돈을 돌려준다.
                    PartyFundLedger.Active.AddLoot(partyId, paid);
                    continue;
                }

                if (paid > 0)
                    costEventChannel?.RaiseEvent(new GoldEarnedEvent(paid, GoldChangeSource.Church));
                coffin.Consume();
                revived++;
            }

            _coffinBuffer.Clear();
            return revived;
        }
    }
}
