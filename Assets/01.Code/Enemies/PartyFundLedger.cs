using System.Collections.Generic;
using UnityEngine;

namespace Code.Enemies
{
    /// <summary>파티 공동 자금이 시작보다 얼마나 줄거나 늘었는지. 정확한 금액 대신 이 단계만 보여 준다.</summary>
    public enum PartyFundTier
    {
        Low,
        Normal,
        High
    }

    /// <summary>
    /// 모험가 파티의 공동 자금.
    ///
    /// 예전에는 모험가마다 예산을 따로 들고 와서, 쓰지 못한 돈은 죽든 나가든 그냥 사라졌다. 잡는 것과
    /// 버는 것이 이어지지 않았다. 파티가 지갑 하나를 함께 쓰고, 죽을 때마다 일부를 떨어뜨리고,
    /// 전멸하면 전부 남기게 하면 "어느 파티를 끝까지 잡을까"가 경영 판단이 된다.
    ///
    /// Unity 오브젝트를 모르는 장부다. 파티 번호는 <see cref="EnemyMover"/>의 점유 번호를 그대로 쓴다.
    /// </summary>
    public sealed class PartyFundLedger
    {
        /// <summary>파티원이 죽을 때 떨어뜨리는 공동 자금 비율.</summary>
        public const float DeathDropRate = 0.1f;

        /// <summary>시작 자금 대비 이 비율 아래면 "적음", 위면 "많음".</summary>
        public const float LowRatio = 0.5f;
        public const float HighRatio = 1.5f;

        private sealed class Party
        {
            public int Fund;
            public int StartingFund;
            public int AliveMembers;
        }

        private readonly Dictionary<int, Party> _parties = new();

        /// <summary>지금 영업에서 쓰는 장부. 웨이브가 시작될 때 비운다.</summary>
        public static PartyFundLedger Active { get; } = new();

        /// <summary>이번 영업에서 살아 나간 모험가가 들고 간 금화 합계. 정산 메모가 읽는다.</summary>
        public int EscapedTotal { get; private set; }

        /// <summary>이번 영업에서 떨어뜨리거나 전멸로 남긴 금화 합계.</summary>
        public int RecoveredTotal { get; private set; }

        /// <summary>파티원 한 명이 합류한다. 그 사람의 예산이 공동 자금에 더해진다.</summary>
        public void Join(int partyId, int budget)
        {
            var party = GetOrCreate(partyId);
            var amount = Mathf.Max(0, budget);
            party.Fund += amount;
            party.StartingFund += amount;
            party.AliveMembers++;
        }

        public int FundOf(int partyId) => _parties.TryGetValue(partyId, out var party) ? party.Fund : 0;

        public int AliveMembersOf(int partyId) =>
            _parties.TryGetValue(partyId, out var party) ? party.AliveMembers : 0;

        /// <summary>시설에서 쓴다. 공동 자금보다 많이 쓸 수는 없다. 실제로 낸 금액을 돌려준다.</summary>
        public int Spend(int partyId, int amount)
        {
            if (amount <= 0 || !_parties.TryGetValue(partyId, out var party))
                return 0;

            var paid = Mathf.Min(amount, party.Fund);
            party.Fund -= paid;
            return paid;
        }

        /// <summary>금고에서 훔친 돈을 들고 다닌다. 잡으면 되찾고, 놓치면 잃는다.</summary>
        public void AddLoot(int partyId, int amount)
        {
            if (amount <= 0 || !_parties.TryGetValue(partyId, out var party))
                return;

            party.Fund += amount;
        }

        /// <summary>
        /// 파티원이 죽었다. 마지막 한 명이면 남은 돈을 전부, 아니면 10%를 떨어뜨린다.
        /// </summary>
        /// <param name="wiped">이 죽음으로 파티가 전멸했는가.</param>
        /// <returns>플레이어가 줍는 금화.</returns>
        public int MemberDied(int partyId, out bool wiped)
        {
            wiped = false;
            if (!_parties.TryGetValue(partyId, out var party) || party.AliveMembers <= 0)
                return 0;

            party.AliveMembers--;
            if (party.AliveMembers == 0)
            {
                wiped = true;
                var remainder = party.Fund;
                _parties.Remove(partyId);
                RecoveredTotal += remainder;
                return remainder;
            }

            var drop = DeathDrop(party.Fund);
            party.Fund -= drop;
            RecoveredTotal += drop;
            return drop;
        }

        /// <summary>
        /// 파티원이 살아서 나갔다. 남은 인원으로 나눈 몫을 들고 나가므로 그만큼은 잃는다.
        /// 마지막 한 명이면 남은 돈을 전부 들고 나간다.
        /// </summary>
        /// <returns>들고 나가 사라진 금화.</returns>
        public int MemberEscaped(int partyId)
        {
            if (!_parties.TryGetValue(partyId, out var party) || party.AliveMembers <= 0)
                return 0;

            var share = party.Fund / party.AliveMembers;
            party.Fund -= share;
            party.AliveMembers--;
            if (party.AliveMembers == 0)
            {
                share += party.Fund;
                _parties.Remove(partyId);
            }

            EscapedTotal += share;
            return share;
        }

        public PartyFundTier TierOf(int partyId) =>
            _parties.TryGetValue(partyId, out var party) ? Tier(party.Fund, party.StartingFund) : PartyFundTier.Low;

        public void Clear()
        {
            _parties.Clear();
            EscapedTotal = 0;
            RecoveredTotal = 0;
        }

        /// <summary>죽을 때 떨어뜨리는 금액. 돈이 있으면 최소 1이다.</summary>
        public static int DeathDrop(int fund) =>
            fund <= 0 ? 0 : Mathf.Max(1, Mathf.FloorToInt(fund * DeathDropRate));

        public static PartyFundTier Tier(int fund, int startingFund)
        {
            if (startingFund <= 0)
                return fund > 0 ? PartyFundTier.High : PartyFundTier.Low;

            var ratio = fund / (float)startingFund;
            if (ratio < LowRatio)
                return PartyFundTier.Low;
            return ratio > HighRatio ? PartyFundTier.High : PartyFundTier.Normal;
        }

        public static string TierLabel(PartyFundTier tier) => tier switch
        {
            PartyFundTier.High => "많음",
            PartyFundTier.Normal => "보통",
            _ => "적음"
        };

        private Party GetOrCreate(int partyId)
        {
            if (!_parties.TryGetValue(partyId, out var party))
            {
                party = new Party();
                _parties[partyId] = party;
            }

            return party;
        }
    }
}
