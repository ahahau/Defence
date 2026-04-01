using _01.Code.Entities;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class CommandCenter : Units.Unit
    {
        public override bool Initialize(Vector2Int position)
        {
            position = GameManager.Instance.GridManager.WorldToCell(transform.position);
            return base.Initialize(position);
        }
    }
}
