using _01.Code.Core;
using _01.Code.MapCreateSystem;
using _01.Code.Units;

namespace _01.Code.Events
{
    public class MagicChangedEvent : GameEvent
    {
        public MagicChangedEvent(int usedMagic, int maxMagic)
        {
            UsedMagic = usedMagic;
            MaxMagic = maxMagic;
        }

        public int UsedMagic { get; }
        public int MaxMagic { get; }
    }

    public class UnitDeployMagicRequestedEvent : GameEvent
    {
        public UnitDeployMagicRequestedEvent(Node node, UnitDataSO unit, int magicAmount)
        {
            Node = node;
            Unit = unit;
            MagicAmount = magicAmount;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int MagicAmount { get; }
    }

    public class UnitDeployMagicPaidEvent : GameEvent
    {
        public UnitDeployMagicPaidEvent(Node node, UnitDataSO unit, int magicAmount, int usedMagic, int maxMagic)
        {
            Node = node;
            Unit = unit;
            MagicAmount = magicAmount;
            UsedMagic = usedMagic;
            MaxMagic = maxMagic;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int MagicAmount { get; }
        public int UsedMagic { get; }
        public int MaxMagic { get; }
    }

    public class UnitDeployMagicRejectedEvent : GameEvent
    {
        public UnitDeployMagicRejectedEvent(Node node, UnitDataSO unit, int magicAmount, int usedMagic, int maxMagic)
        {
            Node = node;
            Unit = unit;
            MagicAmount = magicAmount;
            UsedMagic = usedMagic;
            MaxMagic = maxMagic;
        }

        public Node Node { get; }
        public UnitDataSO Unit { get; }
        public int MagicAmount { get; }
        public int UsedMagic { get; }
        public int MaxMagic { get; }
    }
}
