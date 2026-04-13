using _01.Code.Entities;

namespace _01.Code.Units
{
    public class Unit : PlaceableEntity
    {
        public int level = 1;

        protected override int GetDefaultPathTraversalCost()
        {
            return 6;
        }
    }
}
