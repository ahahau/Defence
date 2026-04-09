using UnityEngine;

namespace _01.Code.Entities
{
    public class SpriteGlowEffect : MonoBehaviour
    {
        private static readonly int IntensityPropertyId = Shader.PropertyToID("_Intensity");

        [field: SerializeField] public SpriteRenderer TargetRenderer { get; private set; }
        [field: SerializeField] public Color GlowColor { get; private set; } = new(1f, 0.96f, 0.86f, 1f);
        [field: SerializeField] public float GlowIntensity { get; private set; } = 2.4f;
        [field: SerializeField] public float GlowScaleMultiplier { get; private set; } = 1f;
        [field: SerializeField] public int SortingOrderOffset { get; private set; } = 0;

        private SpriteRenderer _glowRenderer;
        private Material _glowMaterial;
        private MaterialPropertyBlock _propertyBlock;

        public void SetRuntimeValues(SpriteRenderer targetRenderer, Color glowColor, float glowIntensity, float glowScaleMultiplier, int sortingOrderOffset)
        {
            TargetRenderer = targetRenderer;
            GlowColor = glowColor;
            GlowIntensity = glowIntensity;
            GlowScaleMultiplier = glowScaleMultiplier;
            SortingOrderOffset = sortingOrderOffset;
            
            EnsureGlowRenderer();
            RefreshGlowRenderer();
        }

        private void Awake()
        {
            EnsureTargetRenderer();
            EnsureGlowRenderer();
            RefreshGlowRenderer();
        }

        private void OnEnable()
        {
            EnsureTargetRenderer();
            EnsureGlowRenderer();
            RefreshGlowRenderer();
        }

        private void LateUpdate()
        {
            RefreshGlowRenderer();
        }

        private void OnValidate()
        {
            EnsureTargetRenderer();
            if (Application.isPlaying)
            {
                EnsureGlowRenderer();
                RefreshGlowRenderer();
            }
        }

        private void OnDestroy()
        {
            if (_glowMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_glowMaterial);
                return;
            }

            DestroyImmediate(_glowMaterial);
        }

        private void EnsureTargetRenderer()
        {
            TargetRenderer ??= GetComponent<SpriteRenderer>();
        }

        private void EnsureGlowRenderer()
        {
            if (TargetRenderer == null)
            {
                return;
            }

            Transform glowTransform = transform.Find("Glow");
            if (glowTransform == null)
            {
                GameObject glowObject = new GameObject("Glow");
                glowObject.transform.SetParent(transform, false);
                glowTransform = glowObject.transform;
            }

            _glowRenderer = glowTransform.GetComponent<SpriteRenderer>();
            if (_glowRenderer == null)
            {
                _glowRenderer = glowTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            if (_glowMaterial == null)
            {
                Shader shader = Shader.Find("Custom/SpriteEmissionGlow");
                if (shader == null)
                {
                    return;
                }

                _glowMaterial = new Material(shader)
                {
                    name = $"{name}_GlowMaterial"
                };
            }

            _glowRenderer.sharedMaterial = _glowMaterial;
        }

        private void RefreshGlowRenderer()
        {
            if (TargetRenderer == null || _glowRenderer == null)
            {
                return;
            }

            Transform glowTransform = _glowRenderer.transform;
            glowTransform.localPosition = Vector3.zero;
            glowTransform.localRotation = Quaternion.identity;
            glowTransform.localScale = new Vector3(GlowScaleMultiplier, GlowScaleMultiplier, 1f);

            _glowRenderer.sprite = TargetRenderer.sprite;
            _glowRenderer.flipX = TargetRenderer.flipX;
            _glowRenderer.flipY = TargetRenderer.flipY;
            _glowRenderer.drawMode = TargetRenderer.drawMode;
            if (Application.isPlaying)
            {
                _glowRenderer.size = TargetRenderer.size;
            }
            _glowRenderer.sortingLayerID = TargetRenderer.sortingLayerID;
            _glowRenderer.sortingOrder = TargetRenderer.sortingOrder + SortingOrderOffset;
            _glowRenderer.maskInteraction = TargetRenderer.maskInteraction;
            _glowRenderer.enabled = TargetRenderer.enabled && TargetRenderer.sprite != null;

            Color resolvedGlowColor = GlowColor;
            resolvedGlowColor.a *= TargetRenderer.color.a;
            _glowRenderer.color = resolvedGlowColor;

            _propertyBlock ??= new MaterialPropertyBlock();
            _glowRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(IntensityPropertyId, GlowIntensity);
            _glowRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
