using System.Collections.Generic;
using _01.Code.Commands;
using _01.Code.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _01.Code.Tiles
{
    public class MainBuildingRoomTile : MonoBehaviour
    {
        [field: SerializeField] public string CommandTitle { get; private set; } = "COMMAND";
        [field: SerializeField] public List<BaseCommandSO> Commands { get; private set; } = new();

        private MainBuildingRoomWorld _owner;
        private SpriteRenderer _spriteRenderer;
        public Vector2Int Cell { get; private set; }

        public bool HasCommands
        {
            get { return Commands != null && Commands.Count > 0; }
        }

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

        public void ShowCommands()
        {
            _owner?.ShowTileCommands(this);
        }

        private void OnMouseDown()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Vector3 mouseWorldPosition = Camera.main != null
                ? Camera.main.ScreenToWorldPoint(Input.mousePosition)
                : Input.mousePosition;
            Collider2D hitCollider = Physics2D.OverlapPoint(mouseWorldPosition);
            if (hitCollider != null && hitCollider.GetComponentInParent<TownTileObject>() != null)
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
