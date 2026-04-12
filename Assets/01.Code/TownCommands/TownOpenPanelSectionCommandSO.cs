using _01.Code.TownPanels;
using _01.Code.Tiles;
using UnityEngine;

namespace _01.Code.TownCommands
{
    public class TownOpenPanelSectionCommandSO : TownCommandSO
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

        public override string GetDisplayName(TownCommandContext context)
        {
            return Section != null ? Section.GetSectionTitle() : base.GetDisplayName(context);
        }

        public override string GetDescription(TownCommandContext context)
        {
            return Section != null ? Section.GetBodyText() : string.Empty;
        }

        public override Sprite GetIcon(TownCommandContext context)
        {
            return Section != null ? Section.GetSectionIcon() : base.GetIcon(context);
        }

        public override bool CanExecute(TownCommandContext context)
        {
            return context != null && context.World != null && Section != null;
        }

        public override bool Execute(TownCommandContext context)
        {
            return CanExecute(context) && context.World.ShowPanelSection(Section, SourceData);
        }
    }
}
