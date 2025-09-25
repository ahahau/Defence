using _01.Code.Manager;
using _01.Code.System.Grid;
using UnityEngine;

namespace _01.Code.System.Grids
{
    public class Obstacle : MonoBehaviour
    {
        [SerializeField]private GridManager gridManager;
        [SerializeField]private GridTile tile;

        public void Create()
        {
            tile = gridManager.GridSystem.GetTile(new Vector2(transform.position.x, transform.position.y ));
            if (tile != null)
            {
                tile.IsOnGameObject = true;
                transform.position = new Vector3(0, 0, 0);
            }
        }
    }
}
