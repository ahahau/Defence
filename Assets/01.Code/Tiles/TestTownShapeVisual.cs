using UnityEngine;

namespace _01.Code.Tiles
{
    public enum TestTownShapeType
    {
        Square = 0,
        Triangle = 1
    }

    public class TestTownShapeVisual : MonoBehaviour
    {
        [field: SerializeField] public SpriteRenderer TargetRenderer { get; private set; }
        [field: SerializeField] public TestTownShapeType ShapeType { get; private set; } = TestTownShapeType.Square;
        [field: SerializeField] public Color ShapeColor { get; private set; } = Color.white;
        [field: SerializeField] public int Resolution { get; private set; } = 32;
        [field: SerializeField] public float PixelsPerUnit { get; private set; } = 32f;

        private Sprite _generatedSprite;
        private Texture2D _generatedTexture;

        private void Awake()
        {
            EnsureRenderer();
            ApplyVisual();
        }

        private void OnEnable()
        {
            EnsureRenderer();
            ApplyVisual();
        }

        private void OnValidate()
        {
            EnsureRenderer();
            ApplyVisual();
        }

        private void OnDestroy()
        {
            if (_generatedSprite != null)
            {
                DestroyGeneratedObject(_generatedSprite);
            }

            if (_generatedTexture != null)
            {
                DestroyGeneratedObject(_generatedTexture);
            }
        }

        private void EnsureRenderer()
        {
            TargetRenderer ??= GetComponent<SpriteRenderer>();
        }

        private void ApplyVisual()
        {
            if (TargetRenderer == null)
            {
                return;
            }

            RebuildSprite();
            TargetRenderer.sprite = _generatedSprite;
            TargetRenderer.color = ShapeColor;
        }

        private void RebuildSprite()
        {
            if (_generatedSprite != null)
            {
                DestroyGeneratedObject(_generatedSprite);
            }

            if (_generatedTexture != null)
            {
                DestroyGeneratedObject(_generatedTexture);
            }

            int safeResolution = Mathf.Max(8, Resolution);
            _generatedTexture = new Texture2D(safeResolution, safeResolution, TextureFormat.RGBA32, false);
            _generatedTexture.name = $"{name}_{ShapeType}_Texture";
            _generatedTexture.filterMode = FilterMode.Point;
            _generatedTexture.wrapMode = TextureWrapMode.Clamp;

            Color fillColor = Color.white;
            Color clearColor = new Color(1f, 1f, 1f, 0f);

            for (int y = 0; y < safeResolution; y++)
            {
                for (int x = 0; x < safeResolution; x++)
                {
                    bool filled = ShapeType == TestTownShapeType.Square
                        ? true
                        : IsTrianglePixelFilled(x, y, safeResolution);

                    _generatedTexture.SetPixel(x, y, filled ? fillColor : clearColor);
                }
            }

            _generatedTexture.Apply();
            _generatedSprite = Sprite.Create(
                _generatedTexture,
                new Rect(0f, 0f, safeResolution, safeResolution),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(1f, PixelsPerUnit));
            _generatedSprite.name = $"{name}_{ShapeType}_Sprite";
        }

        private bool IsTrianglePixelFilled(int x, int y, int resolution)
        {
            float normalizedY = y / (float)(resolution - 1);
            float halfWidth = normalizedY * 0.5f;
            float center = 0.5f;
            float normalizedX = x / (float)(resolution - 1);
            return normalizedX >= center - halfWidth && normalizedX <= center + halfWidth;
        }

        private void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
                return;
            }

            DestroyImmediate(target);
        }
    }
}
