using _01.Code.Entities;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class CommandCenter : Building
    {
        public override void Initialize(Vector2Int position)
        {
            position = GameManager.Instance.GridManager.WorldToCell(transform.position);
            base.Initialize(position);
            //entityHealth.Initialize(this);
        }
    }
}
