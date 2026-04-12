using UnityEngine;

namespace _01.Code.TownCommands
{
    public class BattleRemovePlacementCommandSO : TownCommandSO
    {
        public void ConfigureRuntime(int slot)
        {
            ConfigureRuntime("REMOVE", null, slot, false);
        }

        public override string GetDescription(TownCommandContext context)
        {
            return "Remove the selected object.";
        }

        public override bool CanExecute(TownCommandContext context)
        {
            return true;
        }

        public override bool Execute(TownCommandContext context)
        {
            return false;
        }
    }
}
