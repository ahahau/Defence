using System;

namespace Code.Manager.Economy
{
    /// <summary>지금 파산으로 이어질 수 있는 사유. 여러 개가 겹칠 수 있다.</summary>
    [Flags]
    public enum BankruptcyRisk
    {
        None = 0,

        /// <summary>부채가 한도를 넘었다. 영업이 끝날 때 긴급 상환으로도 못 메우면 파산한다.</summary>
        OverCreditLimit = 1,

        /// <summary>신용 경고가 마지막 단계다. 한도를 한 번 더 넘으면 파산한다.</summary>
        FinalWarning = 2,

        /// <summary>오늘이나 내일이 청산일인데 운영 자금이 최소 상환액보다 적다.</summary>
        SettlementShortfall = 4
    }
}
