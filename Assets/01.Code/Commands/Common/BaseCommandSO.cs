using UnityEngine;
using _01.Code.Cost;

namespace _01.Code.Commands
{
    public abstract class BaseCommandSO : ScriptableObject
    {
        [field: SerializeField] public string DisplayName { get; private set; }
        [field: SerializeField] public Sprite Icon { get; private set; }
        [field: SerializeField] public int Slot { get; private set; }
        [field: SerializeField] public bool IsSingleUnitCommand { get; protected set; }
        [field: SerializeField] public bool RequireClickToActivate { get; private set; } = true;

        public virtual string GetDisplayName(CommandContext context)
        {
            return DisplayName;
        }

        public virtual string GetDescription(CommandContext context)
        {
            return string.Empty;
        }

        public virtual Sprite GetIcon(CommandContext context)
        {
            return Icon;
        }

        public virtual Sprite GetCostIcon(CommandContext context)
        {
            return null;
        }

        public virtual int GetCostAmount(CommandContext context)
        {
            return 0;
        }

        public virtual bool CanAfford(CommandContext context)
        {
            if (context == null || context.CostManager == null)
            {
                return true;
            }

            CostDefinitionSO primaryCost = context.CostManager.PrimarySpendCost;
            int costAmount = GetCostAmount(context);
            return primaryCost == null || context.CostManager.CanPay(primaryCost, costAmount);
        }

        public virtual bool IsAvailable(CommandContext context)
        {
            return true;
        }

        public abstract bool IsLocked(CommandContext context);
        public abstract bool CanHandle(CommandContext context);
        public abstract bool Handle(CommandContext context);

        public bool CanExecute(CommandContext context)
        {
            return IsAvailable(context) &&
                   !IsLocked(context) &&
                   CanHandle(context);
        }

        public bool Execute(CommandContext context)
        {
            return CanExecute(context) && Handle(context);
        }

        public void ConfigureRuntime(string displayName, Sprite icon, int slot, bool requireClickToActivate)
        {
            DisplayName = displayName;
            Icon = icon;
            Slot = slot;
            RequireClickToActivate = requireClickToActivate;
        }
    }
}
