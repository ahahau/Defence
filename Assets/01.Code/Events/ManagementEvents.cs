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
}
