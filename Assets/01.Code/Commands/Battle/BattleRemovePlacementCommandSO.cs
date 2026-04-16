using UnityEngine;
using _01.Code.Commands;

namespace _01.Code.Commands.Battle
{
    public class BattleRemovePlacementCommandSO : BaseCommandSO
    {
        public void ConfigureRuntime(int slot)
        {
            ConfigureRuntime("REMOVE", null, slot, false);
        }

        public override string GetDescription(CommandContext context)
        {
            return "Remove the selected object.";
        }

        public override bool IsLocked(CommandContext context)
        {
            return false;
        }

        public override bool CanHandle(CommandContext context)
        {
            return context != null && context.CanRemoveSelectedEntity();
        }

        public override bool Handle(CommandContext context)
        {
            return context.TryRemoveSelectedEntity();
        }
    }
}
