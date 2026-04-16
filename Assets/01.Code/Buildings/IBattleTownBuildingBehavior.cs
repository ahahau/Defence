using _01.Code.Tiles;

namespace _01.Code.Buildings
{
    public interface IBattleTownBuildingBehavior
    {
        void Bind(TownTileObjectDataSO data);
        void Activate();
        void Deactivate();
    }
}
