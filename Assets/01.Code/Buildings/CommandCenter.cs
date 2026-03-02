using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Buildings
{
    public class CommandCenter : PlaceableEntity
    {
        
        public override void Initialize(Vector2Int position)
        {
            position = new Vector2Int((int)transform.position.x, (int)transform.position.y);
            base.Initialize(position);
            //entityHealth.Initialize(this);
        }
    }
}