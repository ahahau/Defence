using _01.Code.UI;
using UnityEngine;

namespace _01.Code.Tiles
{
    public class MainBuildingRoomTile : MonoBehaviour
    {
        private MainBuildingRoomWorld _owner;
        private SpriteRenderer _spriteRenderer;
        public Vector2Int Cell { get; private set; }

        public void Initialize(MainBuildingRoomWorld owner, Vector2Int cell, SpriteRenderer spriteRenderer)
        {
            _owner = owner;
            Cell = cell;
            _spriteRenderer = spriteRenderer;
            DisableGlowRenderer();
            SetVisualState(_spriteRenderer != null ? _spriteRenderer.color : Color.white, false);
        }

        public void SetVisualState(Color color, bool isSelected)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = color;
            }
        }

        private void OnMouseDown()
        {
            if (Application.isPlaying)
            {
                return;
            }

            _owner?.HandleTileClicked(Cell);
        }

        private void DisableGlowRenderer()
        {
            Transform glowTransform = transform.Find("Glow");
            if (glowTransform == null)
            {
                return;
            }

            glowTransform.gameObject.SetActive(false);
        }
    }
}
