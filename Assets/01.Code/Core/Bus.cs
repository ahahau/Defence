namespace _01.Code.Core
{
    public static class Bus<T> where T : IEvent
    {
        public delegate void Event(T evt);

        public static Event OnEvent;
        public static void Raise(T evt) => OnEvent?.Invoke(evt);
        
    }
}