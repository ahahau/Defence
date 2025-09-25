using _01.Code.System.Grids;

namespace _01.Code.System.Grid
{
    public interface IDeployable
    {
        public GridTile _gridTile { get; }
        public void SetTile(GridTile gridTile);
    }
}