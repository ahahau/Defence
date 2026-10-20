using UnityEngine;

namespace Code.Manager
{
    /// <summary>상황 사건 종류. 값이 작을수록 위험하다(먼저 뜬다).</summary>
    public enum SituationKind
    {
        None = 0,

        /// <summary>내일이 청산일인데 금화가 최소 상환액보다 적다. 긴급 사건이라 운영 방침보다 먼저 뜬다.</summary>
        SettlementCrisis = 1,

        /// <summary>빚이 부채 한도에 바짝 붙었거나 신용 경고가 높다.</summary>
        CreditDanger = 2,

        /// <summary>내구도가 바닥나 영업을 멈춘 시설이 있다.</summary>
        BrokenFacilities = 3
    }

    /// <summary>영업 종료 정산이 끝난 뒤의 운영 상태. 상황 사건 판정의 입력이다.</summary>
    public readonly struct SituationSnapshot
    {
        public SituationSnapshot(
            bool tomorrowIsSettlement,
            int gold,
            int weeklyDue,
            int debt,
            int creditLimit,
            int creditWarnings,
            int wornOutFacilities,
            int wornOutRepairCost)
        {
            TomorrowIsSettlement = tomorrowIsSettlement;
            Gold = gold;
            WeeklyDue = weeklyDue;
            Debt = debt;
            CreditLimit = creditLimit;
            CreditWarnings = creditWarnings;
            WornOutFacilities = wornOutFacilities;
            WornOutRepairCost = wornOutRepairCost;
        }

        public bool TomorrowIsSettlement { get; }
        public int Gold { get; }
        public int WeeklyDue { get; }
        public int Debt { get; }
        public int CreditLimit { get; }
        public int CreditWarnings { get; }
        public int WornOutFacilities { get; }

        /// <summary>영업을 멈춘 시설을 지금 정상 수리하면 드는 금화 합.</summary>
        public int WornOutRepairCost { get; }

        /// <summary>청산일에 모자라는 금화.</summary>
        public int SettlementShortfall => Mathf.Max(0, WeeklyDue - Gold);
    }

    /// <summary>
    /// 상황 사건(기획서: "현재 가장 위험한 운영 문제")의 판정과 선택지 수치.
    ///
    /// 일반 사건은 운에 맡긴 이야기라 위기에 처해도 아무 말이 없었다. 청산 전날 돈이 모자라거나
    /// 빚이 한도에 붙거나 시설이 멈춰도 정산 메모 한 줄뿐이라, 무엇을 할 수 있는지 고를 기회가 없었다.
    /// 가장 위험한 문제 하나를 골라 그 상황에 맞는 금액으로 선택지를 만든다.
    /// </summary>
    public static class SituationEventRules
    {
        /// <summary>빚이 한도의 이 비율 이상이면 부채 한도 위험이다.</summary>
        public const float CreditDangerDebtRatio = 0.9f;

        /// <summary>신용 경고가 이 단계 이상이면 부채 한도 위험이다.</summary>
        public const int CreditDangerWarnings = 3;

        /// <summary>급전: 모자란 금화를 받고 그 1.3배를 빚으로 진다.</summary>
        public const int EmergencyLoanDebtPercent = 130;

        /// <summary>상납금: 모자란 금화의 절반을 민심으로 짜낸다.</summary>
        public const float TributeShare = 0.5f;
        public const int TributeMoralePenalty = 12;

        /// <summary>조기 상환: 빚의 20%까지, 갚는 원금의 10%를 수수료로 더 낸다.</summary>
        public const int EarlyRepayDebtSharePercent = 20;
        public const int EarlyRepayFeePercent = 10;

        /// <summary>기관 접대: 30G + 경고 단계 × 20G를 내고 신용 경고를 한 단계 내린다.</summary>
        public const int BribeBaseCost = 30;
        public const int BribeCostPerWarning = 20;

        /// <summary>긴급 수리는 정상 수리비의 1.5배, 일꾼 징발은 절반에 민심을 깎는다. 둘 다 즉시 끝난다.</summary>
        public const float RushRepairCostRate = 1.5f;
        public const float ConscriptRepairCostRate = 0.5f;
        public const int ConscriptMoralePenalty = 8;

        /// <summary>지금 가장 위험한 문제 하나. 없으면 <see cref="SituationKind.None"/>.</summary>
        public static SituationKind Detect(in SituationSnapshot snapshot)
        {
            if (snapshot.TomorrowIsSettlement && snapshot.SettlementShortfall > 0)
                return SituationKind.SettlementCrisis;

            if (IsCreditDanger(snapshot))
                return SituationKind.CreditDanger;

            if (snapshot.WornOutFacilities > 0)
                return SituationKind.BrokenFacilities;

            return SituationKind.None;
        }

        /// <summary>운영 방침보다 먼저 뜨는 긴급 사건인가. 쉬는 시간과도 무관하다.</summary>
        public static bool IsUrgent(SituationKind kind) => kind == SituationKind.SettlementCrisis;

        public static bool IsCreditDanger(in SituationSnapshot snapshot)
        {
            if (snapshot.Debt <= 0)
                return false;

            return snapshot.CreditWarnings >= CreditDangerWarnings
                   || (snapshot.CreditLimit > 0 && snapshot.Debt >= snapshot.CreditLimit * CreditDangerDebtRatio);
        }

        public static int EmergencyLoanDebt(int cash) =>
            cash <= 0 ? 0 : CeilPercent(cash, EmergencyLoanDebtPercent);

        /// <summary>
        /// 급전으로 받을 금화. 빚이 늘면 내일 최소 상환액도 늘어나므로, 늘어난 청구액까지 채우는 금액을 찾는다.
        /// 금화 1당 청구액 증가가 1보다 작아(이자+원금 비율 × 1.3) 몇 번이면 수렴한다.
        /// </summary>
        /// <param name="dueWithExtraDebt">빚이 그만큼 늘었을 때의 최소 상환액.</param>
        public static int EmergencyLoanCash(int gold, System.Func<int, int> dueWithExtraDebt)
        {
            if (dueWithExtraDebt == null)
                return 0;

            var cash = Mathf.Max(0, dueWithExtraDebt(0) - gold);
            for (var i = 0; i < 20 && cash > 0; i++)
            {
                var needed = dueWithExtraDebt(EmergencyLoanDebt(cash)) - gold;
                if (cash >= needed)
                    break;

                cash = needed;
            }

            return cash;
        }

        public static int TributeGold(int shortfall) =>
            shortfall <= 0 ? 0 : Mathf.CeilToInt(shortfall * TributeShare);

        /// <summary>조기 상환으로 갚을 원금. 수수료까지 금화로 낼 수 있는 만큼만.</summary>
        public static int EarlyRepayPrincipal(int debt, int gold)
        {
            if (debt <= 0 || gold <= 0)
                return 0;

            // 금화 계산이 1G씩 어긋나지 않게 정수 백분율로 센다(0.1f 같은 실수는 올림에서 1이 튄다).
            var wanted = CeilPercent(debt, EarlyRepayDebtSharePercent);
            var affordable = gold * 100 / (100 + EarlyRepayFeePercent);
            while (affordable + 1 + EarlyRepayFee(affordable + 1) <= gold)
                affordable++;
            while (affordable > 0 && affordable + EarlyRepayFee(affordable) > gold)
                affordable--;

            return Mathf.Clamp(wanted, 0, Mathf.Min(debt, affordable));
        }

        public static int EarlyRepayFee(int principal) =>
            principal <= 0 ? 0 : CeilPercent(principal, EarlyRepayFeePercent);

        private static int CeilPercent(int value, int percent) => (value * percent + 99) / 100;

        public static int BribeCost(int warnings) => BribeBaseCost + BribeCostPerWarning * Mathf.Max(0, warnings);

        public static int ScaledRepairCost(int repairCost, float rate) =>
            repairCost <= 0 ? 0 : Mathf.CeilToInt(repairCost * Mathf.Max(0f, rate));
    }
}
