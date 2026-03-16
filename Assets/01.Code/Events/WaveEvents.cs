using _01.Code.Core;

namespace _01.Code.Events
{
    public static class WaveEvents
    {
        public static readonly WaveStartedEvent WaveStartedEvent = new WaveStartedEvent();
        public static readonly WaveClearedEvent WaveClearedEvent = new WaveClearedEvent();
        public static readonly WaveStartRequestedEvent WaveStartRequestedEvent = new WaveStartRequestedEvent();
    }

    public class WaveStartRequestedEvent : GameEvent
    {
    }

    public class WaveStartedEvent : GameEvent
    {
    }

    public class WaveClearedEvent : GameEvent
    {
    }
}
