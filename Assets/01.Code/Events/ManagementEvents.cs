using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Manager;

namespace _01.Code.Events
{
    public class MoraleChangedEvent : GameEvent
    {
        public MoraleChangedEvent(int currentMorale, int delta, string reason)
        {
            CurrentMorale = currentMorale;
            Delta = delta;
            Reason = reason;
        }

        public int CurrentMorale { get; }
        public int Delta { get; }
        public string Reason { get; }
    }

    public class MoraleChangeRequestedEvent : GameEvent
    {
        public MoraleChangeRequestedEvent(int delta, string reason)
        {
            Delta = delta;
            Reason = reason;
        }

        public int Delta { get; }
        public string Reason { get; }
    }

    public class PolicyChoicesOfferedEvent : GameEvent
    {
        public PolicyChoicesOfferedEvent(int day, IReadOnlyList<PolicyDataSO> choices)
        {
            Day = day;
            Choices = choices;
        }

        public int Day { get; }
        public IReadOnlyList<PolicyDataSO> Choices { get; }
    }

    public class PolicySelectedEvent : GameEvent
    {
        public PolicySelectedEvent(int day, PolicyDataSO policy)
        {
            Day = day;
            Policy = policy;
        }

        public int Day { get; }
        public PolicyDataSO Policy { get; }
    }

    /// <summary>시설이 문을 닫았을 때. 그날부터 벌이도 운영비도 소문도 멈춘다.</summary>
    public class FacilityClosedEvent : GameEvent
    {
        public FacilityClosedEvent(_01.Code.Buildings.Building building)
        {
            Building = building;
        }

        public _01.Code.Buildings.Building Building { get; }
    }

    /// <summary>닫아 둔 시설을 다시 열기로 하고 값을 치렀을 때. 아직 열리지는 않았다.</summary>
    public class FacilityReopenScheduledEvent : GameEvent
    {
        public FacilityReopenScheduledEvent(_01.Code.Buildings.Building building, int goldCost, int daysRemaining)
        {
            Building = building;
            GoldCost = goldCost;
            DaysRemaining = daysRemaining;
        }

        public _01.Code.Buildings.Building Building { get; }
        public int GoldCost { get; }
        public int DaysRemaining { get; }
    }

    /// <summary>예약한 날이 되어 시설이 실제로 다시 열렸을 때.</summary>
    public class FacilityReopenedEvent : GameEvent
    {
        public FacilityReopenedEvent(_01.Code.Buildings.Building building)
        {
            Building = building;
        }

        public _01.Code.Buildings.Building Building { get; }
    }
}
