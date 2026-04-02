using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.Units;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class CommandCenter : Unit
    {
        private GridManager _gridManager;

        public void BindGrid(GridManager gridManager)
        {
            _gridManager = gridManager;
        }

        public override bool Initialize(Vector2Int position)
        {
            if (_gridManager != null)
            {
                position = _gridManager.WorldToCell(transform.position);
            }

            return base.Initialize(position);
        }
    }
}
