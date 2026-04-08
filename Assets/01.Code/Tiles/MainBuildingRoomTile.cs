using _01.Code.UI;
using UnityEngine;

namespace _01.Code.Tiles
{
    public class MainBuildingRoomTile : MonoBehaviour
    {
        private static readonly int IntensityPropertyId = Shader.PropertyToID("_Intensity");

        private MainBuildingRoomWorld _owner;
        private SpriteRenderer _spriteRenderer;
        private SpriteRenderer _glowRenderer;
        private MaterialPropertyBlock _glowPropertyBlock;
        public Vector2Int Cell { get; private set; }

        public void Initialize(MainBuildingRoomWorld owner, Vector2Int cell, SpriteRenderer spriteRenderer)
        {
            _owner = owner;
            Cell = cell;
            _spriteRenderer = spriteRenderer;
            _glowRenderer = InitializeGlowRenderer();
            SetVisualState(_spriteRenderer != null ? _spriteRenderer.color : Color.white, false);
        }

        public void SetVisualState(Color color, bool isSelected)
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = color;
            }

            if (_glowRenderer != null)
            {
                Color glowColor = _owner.GlowTint;
                float glowIntensity = isSelected
                    ? _owner.GlowIntensity * _owner.SelectedGlowIntensityMultiplier
                    : _owner.GlowIntensity;

                glowColor.a = color.a * _owner.GlowAlphaMultiplier;
                _glowRenderer.color = glowColor;
                ApplyGlowIntensity(glowIntensity);
            }
        }

        private void OnMouseDown()
        {
            _owner?.HandleTileClicked(Cell);
        }

        private SpriteRenderer InitializeGlowRenderer()
        {
            Transform glowTransform = transform.Find("Glow");
            GameObject glowObject;
            if (glowTransform == null)
            {
                glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(transform, false);
            }
            else
            {
                glowObject = glowTransform.gameObject;
            }

            SpriteRenderer glowRenderer = glowObject.GetComponent<SpriteRenderer>();
            if (glowRenderer == null)
            {
                glowRenderer = glowObject.AddComponent<SpriteRenderer>();
            }

            glowRenderer.sprite = _owner.TileSprite;
            glowRenderer.sharedMaterial = _owner.GlowMaterial;
            glowRenderer.sortingOrder = _owner.GlowSortingOrder;
            glowRenderer.color = _owner.GlowTint;
            glowObject.transform.localPosition = Vector3.zero;
            glowObject.transform.localScale = Vector3.one;
            return glowRenderer;
        }

        private void ApplyGlowIntensity(float intensity)
        {
            if (_glowRenderer == null)
            {
                return;
            }

            _glowPropertyBlock ??= new MaterialPropertyBlock();
            _glowRenderer.GetPropertyBlock(_glowPropertyBlock);
            _glowPropertyBlock.SetFloat(IntensityPropertyId, intensity);
            _glowRenderer.SetPropertyBlock(_glowPropertyBlock);
        }
    }
}
