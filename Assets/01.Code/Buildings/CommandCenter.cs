using _01.Code.Entities;
using _01.Code.Manager;
using _01.Code.Unit;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class CommandCenter : Unit.Unit
    {
        public override bool Initialize(Vector2Int position)
        {
            position = GameManager.Instance.GridManager.WorldToCell(transform.position);
            return base.Initialize(position);
        }
    }
}
