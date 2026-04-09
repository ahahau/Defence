using _01.Code.Core;
using _01.Code.TownCommands;

namespace _01.Code.Events
{
    public class TownCommandSelectedEvent : GameEvent
    {
        public TownCommandSO Command { get; private set; }

        public TownCommandSelectedEvent Initializer(TownCommandSO command)
        {
            Command = command;
            return this;
        }
    }
}
