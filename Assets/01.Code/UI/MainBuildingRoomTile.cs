using UnityEngine;

namespace _01.Code.UI
{
    public class MainBuildingRoomTile : MonoBehaviour
    {
        private MainBuildingRoomWorld _owner;
        private SpriteRenderer _spriteRenderer;

        public Vector2Int Cell { get; private set; }

        public void Configure(MainBuildingRoomWorld owner, Vector2Int cell, SpriteRenderer spriteRenderer)
        {
            _owner = owner;
            Cell = cell;
            _spriteRenderer = spriteRenderer;
        }

        public void SetColor(Color color)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = color;
            }
        }

        private void OnMouseDown()
        {
            _owner?.HandleTileClicked(Cell);
        }
    }
}
