using UnityEngine;
using _01.Code.Cost;

namespace _01.Code.TownCommands
{
    public abstract class TownCommandSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public int Slot { get; private set; }
        [field: SerializeField] public bool RequireTargetClick { get; private set; }

        public virtual string GetDisplayName(TownCommandContext context)
        {
            return DisplayName;
        }

        public virtual string GetDescription(TownCommandContext context)
        {
            return string.Empty;
        }

        public virtual Sprite GetIcon(TownCommandContext context)
        {
            return Icon;
        }

        public virtual Sprite GetCostIcon(TownCommandContext context)
        {
            return null;
        }

        public virtual int GetCostAmount(TownCommandContext context)
        {
            return 0;
        }

        public virtual bool CanAfford(TownCommandContext context)
        {
            if (context == null || context.CostManager == null)
            {
                return true;
            }

            CostDefinitionSO primaryCost = context.CostManager.PrimarySpendCost;
            int costAmount = GetCostAmount(context);
            return primaryCost == null || context.CostManager.CanPay(primaryCost, costAmount);
        }

        public void ConfigureRuntime(string displayName, Sprite icon, int slot, bool requireTargetClick)
        {
            DisplayName = displayName;
            Icon = icon;
            Slot = slot;
            RequireTargetClick = requireTargetClick;
        }

        public abstract bool CanExecute(TownCommandContext context);
        public abstract bool Execute(TownCommandContext context);
    }
}
