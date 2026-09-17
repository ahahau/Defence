using _01.Code.Core;
using _01.Code.MapCreateSystem;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Events
{
    public enum GoldChangeSource
    {
        General,
        WaveReward,
        Mine,
        Inn,
        Store,
        Exploitation,
        TreasuryLoot,
        Dialogue,
        Policy,
        Blacksmith,
        WaveObjective,
        Bounty
    }

    public class BuildCostRequestedEvent : GameEvent
    {
        public BuildCostRequestedEvent(Node node, int goldAmount)
        {
            Node = node;
            GoldAmount = goldAmount;
        }

        public Node Node { get; }
        public int GoldAmount { get; }
    }

    public class BuildCostPaidEvent : GameEvent
    {
        public BuildCostPaidEvent(Node node, int goldAmount, int remainingGold, float consumedDiscountRate = 0f)
        {
            Node = node;
            GoldAmount = goldAmount;
            RemainingGold = remainingGold;
            ConsumedDiscountRate = consumedDiscountRate;
        }

        public Node Node { get; }
        public int GoldAmount { get; }
        public int RemainingGold { get; }
        public float ConsumedDiscountRate { get; }
    }

    /// <summary>결제 후 설치가 실패했을 때만 발생한다. 금화와 이번 건설 할인을 함께 되돌린다.</summary>
    public class BuildCostRefundedEvent : GameEvent
    {
        public BuildCostRefundedEvent(Node node, int goldAmount, float restoredDiscountRate)
        {
            Node = node;
            GoldAmount = goldAmount;
            RestoredDiscountRate = restoredDiscountRate;
        }

        public Node Node { get; }
        public int GoldAmount { get; }
        public float RestoredDiscountRate { get; }
    }

    public class BuildCostRejectedEvent : GameEvent
    {
        public BuildCostRejectedEvent(Node node, int goldAmount, int currentGold)
        {
            Node = node;
            GoldAmount = goldAmount;
            CurrentGold = currentGold;
        }

        public Node Node { get; }
        public int GoldAmount { get; }
        public int CurrentGold { get; }
    }

    public class GoldChangedEvent : GameEvent
    {
        public GoldChangedEvent(int currentGold)
        {
            CurrentGold = currentGold;
        }

        public int CurrentGold { get; }
    }

    public class GoldEarnedEvent : GameEvent
    {
        public GoldEarnedEvent(int goldAmount)
            : this(goldAmount, GoldChangeSource.General)
        {
        }

        public GoldEarnedEvent(int goldAmount, GoldChangeSource source)
        {
            GoldAmount = goldAmount;
            Source = source;
        }

        public int GoldAmount { get; }
        public GoldChangeSource Source { get; }
    }

    public class ConstructionDiscountGrantedEvent : GameEvent
    {
        public ConstructionDiscountGrantedEvent(float discountRate)
        {
            DiscountRate = UnityEngine.Mathf.Clamp(discountRate, 0f, 0.9f);
        }

        public float DiscountRate { get; }
    }

    public class GoldLostEvent : GameEvent
    {
        public GoldLostEvent(int goldAmount)
            : this(goldAmount, GoldChangeSource.General)
        {
        }

        public GoldLostEvent(int goldAmount, GoldChangeSource source)
        {
            GoldAmount = goldAmount;
            Source = source;
        }

        public int GoldAmount { get; }
        public GoldChangeSource Source { get; }
    }

    /// <summary>
    /// 운영 금화와 분리된 금고 보관금을 침입자가 약탈했을 때 발생한다.
    /// 실제 보유 골드를 다시 차감하지 않고, 일일 정산 장부에만 기록한다.
    /// </summary>
    public class TreasuryRobbedEvent : GameEvent
    {
        public TreasuryRobbedEvent(int goldAmount)
        {
            GoldAmount = goldAmount;
        }

        public int GoldAmount { get; }
    }

    public class HireUnitCostRequestedEvent : GameEvent
    {
        public HireUnitCostRequestedEvent(Node node, UnitDataSO unit, int goldAmount)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
    }

    public class HireUnitCostPaidEvent : GameEvent
    {
        public HireUnitCostPaidEvent(Node node, UnitDataSO unit, int goldAmount, int remainingGold)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
            RemainingGold = remainingGold;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
        public int RemainingGold { get; }
    }

    public class HireUnitCostRejectedEvent : GameEvent
    {
        public HireUnitCostRejectedEvent(Node node, UnitDataSO unit, int goldAmount, int currentGold)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
            CurrentGold = currentGold;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int GoldAmount { get; }
        public int CurrentGold { get; }
    }

    public class UnitRecoveryCostRequestedEvent : GameEvent
    {
        public UnitRecoveryCostRequestedEvent(Node node, Unit unit, int goldAmount)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
        }

        public Node Node { get; }
        public Unit Unit { get; }
        public int GoldAmount { get; }
    }

    public class UnitRecoveryCostPaidEvent : GameEvent
    {
        public UnitRecoveryCostPaidEvent(Node node, Unit unit, int goldAmount, int remainingGold)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
            RemainingGold = remainingGold;
        }

        public Node Node { get; }
        public Unit Unit { get; }
        public int GoldAmount { get; }
        public int RemainingGold { get; }
    }

    public class UnitRecoveryCostRejectedEvent : GameEvent
    {
        public UnitRecoveryCostRejectedEvent(Node node, Unit unit, int goldAmount, int currentGold)
        {
            Node = node;
            Unit = unit;
            GoldAmount = goldAmount;
            CurrentGold = currentGold;
        }

        public Node Node { get; }
        public Unit Unit { get; }
        public int GoldAmount { get; }
        public int CurrentGold { get; }
    }

    /// <summary>상인에게 유물을 사겠다고 요청. CostManager가 금화를 확인하고 승인/거절한다.</summary>
    public class ArtifactPurchaseRequestedEvent : GameEvent
    {
        public ArtifactPurchaseRequestedEvent(Artifacts.ArtifactDataSO artifact, int goldAmount)
        {
            Artifact = artifact;
            GoldAmount = goldAmount;
        }

        public Artifacts.ArtifactDataSO Artifact { get; }
        public int GoldAmount { get; }
    }

    public class ArtifactPurchasePaidEvent : GameEvent
    {
        public ArtifactPurchasePaidEvent(Artifacts.ArtifactDataSO artifact, int goldAmount, int remainingGold)
        {
            Artifact = artifact;
            GoldAmount = goldAmount;
            RemainingGold = remainingGold;
        }

        public Artifacts.ArtifactDataSO Artifact { get; }
        public int GoldAmount { get; }
        public int RemainingGold { get; }
    }

    public class ArtifactPurchaseRejectedEvent : GameEvent
    {
        public ArtifactPurchaseRejectedEvent(Artifacts.ArtifactDataSO artifact, int goldAmount, int currentGold)
        {
            Artifact = artifact;
            GoldAmount = goldAmount;
            CurrentGold = currentGold;
        }

        public Artifacts.ArtifactDataSO Artifact { get; }
        public int GoldAmount { get; }
        public int CurrentGold { get; }
    }

    /// <summary>
    /// 아직 반영되지 않은 정산 예정액이 바뀌었을 때.
    /// 웨이브 중에는 금화가 움직이지 않으므로, 이 값이라도 보여줘야 화면이 멈춘 것처럼 보이지 않는다.
    /// </summary>
    public class SettlementPreviewChangedEvent : GameEvent
    {
        public SettlementPreviewChangedEvent(int pendingIncome, int pendingExpense)
        {
            PendingIncome = pendingIncome;
            PendingExpense = pendingExpense;
        }

        public int PendingIncome { get; }
        public int PendingExpense { get; }
        public int PendingNet => PendingIncome - PendingExpense;
    }

    /// <summary>빚이 늘거나 줄었을 때. 표시 갱신용.</summary>
    public class DebtChangedEvent : GameEvent
    {
        public DebtChangedEvent(int currentDebt, int weeklyDue, int delta)
        {
            CurrentDebt = currentDebt;
            WeeklyDue = weeklyDue;
            Delta = delta;
        }

        public int CurrentDebt { get; }

        /// <summary>청산일에 실제로 내야 하는 금액. 원금에 이자를 더한 값이다.</summary>
        public int WeeklyDue { get; }
        public int Delta { get; }
    }

    /// <summary>청산일에 빚을 다 갚지 못했을 때. 게임오버로 이어진다.</summary>
    public class BankruptcyEvent : GameEvent
    {
        public BankruptcyEvent(int owed, int gold)
        {
            Owed = owed;
            Gold = gold;
        }

        /// <summary>내야 했던 금액. 원금에 이자를 더한 값이다.</summary>
        public int Owed { get; }

        /// <summary>청산을 시도한 시점에 가지고 있던 금화.</summary>
        public int Gold { get; }
    }

    /// <summary>정산이 끝나 순액이 실제로 반영됐을 때. 정산 패널 표시용.</summary>
    public class SettlementAppliedEvent : GameEvent
    {
        public SettlementAppliedEvent(int net, int paidFromGold, int borrowed, int currentGold, int currentDebt)
        {
            Net = net;
            PaidFromGold = paidFromGold;
            Borrowed = borrowed;
            CurrentGold = currentGold;
            CurrentDebt = currentDebt;
        }

        /// <summary>수입 - 지출. 양수면 획득, 음수면 지불.</summary>
        public int Net { get; }
        /// <summary>보유 금화에서 실제로 빠져나간 금액.</summary>
        public int PaidFromGold { get; }
        /// <summary>금화가 모자라 부채로 넘어간 금액.</summary>
        public int Borrowed { get; }
        public int CurrentGold { get; }
        public int CurrentDebt { get; }
    }
}
