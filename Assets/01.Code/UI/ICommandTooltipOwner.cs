using _01.Code.Commands;

namespace _01.Code.UI
{
    public interface ICommandTooltipOwner
    {
        void ShowTooltip(BaseCommandSO command, CommandContext context);
        void HideTooltip();
        bool ShouldSuppressTooltipThisFrame();
    }
}
