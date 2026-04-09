using UnityEngine;

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

        public virtual Sprite GetIcon(TownCommandContext context)
        {
            return Icon;
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
