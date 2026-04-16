using _01.Code.TownPanels;
using _01.Code.Tiles;
using UnityEngine;
using _01.Code.Commands;

namespace _01.Code.Commands.Town
{
    public class TownOpenPanelSectionCommandSO : BaseCommandSO
    {
        [field: SerializeField] public TownObjectPanelSectionSO Section { get; private set; }
        [field: SerializeField] public TownTileObjectDataSO SourceData { get; private set; }

        public void ConfigureRuntime(TownObjectPanelSectionSO section, TownTileObjectDataSO sourceData, int slot)
        {
            Section = section;
            SourceData = sourceData;
            ConfigureRuntime(
                section != null ? section.GetSectionTitle() : "OPEN",
                section != null ? section.GetSectionIcon() : null,
                slot,
                false);
        }

        public override string GetDisplayName(CommandContext context)
        {
            return Section != null ? Section.GetSectionTitle() : base.GetDisplayName(context);
        }

        public override string GetDescription(CommandContext context)
        {
            return Section != null ? Section.GetBodyText() : string.Empty;
        }

        public override Sprite GetIcon(CommandContext context)
        {
            return Section != null ? Section.GetSectionIcon() : base.GetIcon(context);
        }

        public override bool IsLocked(CommandContext context)
        {
            return false;
        }

        public override bool CanHandle(CommandContext context)
        {
            return context != null && context.World != null && Section != null;
        }

        public override bool Handle(CommandContext context)
        {
            return context.World.ShowPanelSection(Section, SourceData);
        }
    }
}
