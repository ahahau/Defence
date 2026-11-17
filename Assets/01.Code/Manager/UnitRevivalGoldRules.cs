using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 아군이 쓰러질 때 내는 부활비 금화와 그 금화를 받을 모험가 파티.
    ///
    /// 예전에는 부활에 마력만 들어서 부하가 쓰러져도 금화로는 아무 일이 없었고, 잡는 것과 지키는 것이
    /// 따로 놀았다. 쓰러진 부하의 값이 쓰러뜨린 파티의 지갑으로 들어가면, 그 파티를 끝까지 잡아야
    /// 돈을 되찾는다(낙전·전멸). 놓치면 들고 나간다.
    /// </summary>
    public static class UnitRevivalGoldRules
    {
        /// <summary>부활비 = 고용 정가의 이 백분율(올림) + 레벨 1 위로 레벨당 금화.</summary>
        public const int HireCostPercent = 50;
        public const int GoldPerLevel = 5;

        /// <summary>마지막 공격이 이 시간(게임 초) 안이어야 그 파티가 쓰러뜨린 것으로 본다. 오래전 상처로 쓰러지면 방 기준으로 정한다.</summary>
        public const float AttributionSeconds = 5f;

        public static int GoldCost(int hireCost, int level) =>
            (Mathf.Max(0, hireCost) * HireCostPercent + 99) / 100 + GoldPerLevel * Mathf.Max(0, level - 1);

        /// <summary>
        /// 부활비를 받을 파티. 마지막으로 때린 파티가 살아 있으면 그 파티, 아니면 같은 방에서 살아 있는
        /// 인원이 가장 많은 파티(같으면 먼저 센 파티). 아무도 없으면 -1 — 금화를 내지 않는다.
        /// </summary>
        /// <param name="lastAttackerParty">최근에 때린 파티. 없으면 -1.</param>
        /// <param name="roomMemberParties">같은 방에 있는 모험가마다 그 파티 번호.</param>
        public static int ResolveReceivingParty(
            int lastAttackerParty,
            IReadOnlyList<int> roomMemberParties,
            Func<int, bool> isPartyAlive)
        {
            if (lastAttackerParty >= 0 && (isPartyAlive == null || isPartyAlive(lastAttackerParty)))
                return lastAttackerParty;

            if (roomMemberParties == null)
                return -1;

            var best = -1;
            var bestCount = 0;
            for (var i = 0; i < roomMemberParties.Count; i++)
            {
                var party = roomMemberParties[i];
                if (party < 0 || party == best || (isPartyAlive != null && !isPartyAlive(party)))
                    continue;

                var count = 0;
                for (var j = 0; j < roomMemberParties.Count; j++)
                {
                    if (roomMemberParties[j] == party)
                        count++;
                }

                if (count > bestCount)
                {
                    best = party;
                    bestCount = count;
                }
            }

            return best;
        }
    }
}
