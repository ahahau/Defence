using UnityEngine;

namespace _01.Code.UI
{
    public class MainBuildingRoomTile : MonoBehaviour
    {
        private MainBuildingRoomWorld _owner;
        private Renderer _renderer;
        private MaterialPropertyBlock _propertyBlock;

        public Vector2Int Cell { get; private set; }

        public void Configure(MainBuildingRoomWorld owner, Vector2Int cell, Renderer tileRenderer)
        {
            _owner = owner;
            Cell = cell;
            _renderer = tileRenderer;
        }

        public void SetColor(Color color)
        {
            if (_renderer != null)
            {
                _propertyBlock ??= new MaterialPropertyBlock();
                _renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                _propertyBlock.SetColor("_Color", color);
                _renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void OnMouseDown()
        {
            _owner?.HandleTileClicked(Cell);
        }
    }
}
