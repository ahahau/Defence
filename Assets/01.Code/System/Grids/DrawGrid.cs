using _01.Code.Manager;
using _01.Code.System.Grid;
using UnityEditor;
using UnityEngine;

namespace _01.Code.System.Grids
{
    public class DrawGrid : MonoBehaviour
    {
        [SerializeField] private GridManager gridManager;
        private GridSystem _gridSystem = new GridSystem(0,0,1);
        private void OnDrawGizmosSelected()
        {
            if (_gridSystem == null)
                return;
            Gizmos.color = Color.white;
            for (int x = 0; x < gridManager.Width; x++)
            {
                for (int y = 0; y < gridManager.Height; y++)
                {
                    GridTile gridTile = _gridSystem.GetTile(new Vector2Int(x, y));
                    Vector3 pos = new Vector3(0, 0, 0);
                    Gizmos.color = Color.gray;
                    Gizmos.DrawWireCube(pos, new Vector3(gridManager.CellSize, gridManager.CellSize, 0));

                    #if UNITY_EDITOR
                         Handles.color = Color.white;
                        Handles.Label(pos + Vector3.up * 0.3f, $"({x},{y})");
                    #endif
                }
            }
        }
        [ContextMenu("Reset Grid")]
        public void ResetGrid()
        {
            _gridSystem = new GridSystem(gridManager.Width, gridManager.Height, gridManager.CellSize);      
        }
    }

}