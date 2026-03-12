using _01.Code.Core;

namespace _01.Code.Events
{
    public static class WaveEvents
    {
        public static WaveStartedEvent WaveStartedEvent = new WaveStartedEvent();
        public static WaveClearedEvent WaveClearedEvent = new WaveClearedEvent();
    }
    public class WaveStartedEvent : GameEvent
    {
    }

    public class WaveClearedEvent : GameEvent
    {
    }
}
