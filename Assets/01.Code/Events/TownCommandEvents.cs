using _01.Code.Core;
using _01.Code.Commands;

namespace _01.Code.Events
{
    public class TownCommandSelectedEvent : GameEvent
    {
        public BaseCommandSO Command { get; private set; }

        public TownCommandSelectedEvent Initializer(BaseCommandSO command)
        {
            Command = command;
            return this;
        }
    }
}
