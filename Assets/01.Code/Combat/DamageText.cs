using _01.Code.Manager;
using GondrLib.ObjectPool.Runtime;
using TMPro;
using UnityEngine;

namespace _01.Code.Combat
{
    public class DamageText : MonoBehaviour, IPoolable
    {
        [SerializeField] private PoolingItemSO poolingType;
        [SerializeField] private Vector3 worldOffset = new(0f, 1.2f, 0f);
        [SerializeField] private float riseDistance = 0.75f;
        [SerializeField] private float lifetime = 0.7f;
        [SerializeField] private float fontSize = 4f;
        [SerializeField] private Color textColor = Color.red;

        private TextMeshPro _text;
        private Transform _followTarget;
        private float _elapsed;
        private Color _baseColor;
        private Vector3 _spawnPosition;
        private Pool _pool;

        public PoolingItemSO PoolingType => poolingType;
        public GameObject GameObject => gameObject;

        private void Awake()
        {
            EnsureTextComponent();
        }

        public void SetUpPool(Pool pool)
        {
            _pool = pool;
        }

        public void ResetItem()
        {
            _elapsed = 0f;
            _followTarget = null;
            _spawnPosition = Vector3.zero;
            EnsureTextComponent();
            _text.text = string.Empty;
            _text.color = textColor;
        }

        public void Initialize(float damage, Transform followTarget)
        {
            _elapsed = 0f;
            _followTarget = followTarget;
            _baseColor = textColor;
            _spawnPosition = followTarget != null ? followTarget.position : transform.position;
            EnsureTextComponent();

            _text.font = TMP_Settings.defaultFontAsset;
            _text.text = Mathf.CeilToInt(damage).ToString();
            _text.color = _baseColor;
            UpdatePosition(0f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(_elapsed / lifetime);
            UpdatePosition(normalizedTime);

            Color currentColor = _baseColor;
            currentColor.a = 1f - normalizedTime;
            _text.color = currentColor;

            if (normalizedTime >= 1f)
            {
                ReturnToPool();
            }
        }

        private void UpdatePosition(float normalizedTime)
        {
            Vector3 basePosition = _followTarget != null ? _followTarget.position : _spawnPosition;
            transform.position = basePosition + worldOffset + (Vector3.up * (riseDistance * normalizedTime));
        }

        private void ReturnToPool()
        {
            if (_pool != null)
            {
                _pool.Push(this);
                return;
            }

            Destroy(gameObject);
        }

        private void EnsureTextComponent()
        {
            if (_text == null)
            {
                _text = GetComponent<TextMeshPro>();
                if (_text == null)
                {
                    _text = gameObject.AddComponent<TextMeshPro>();
                }
            }

            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = fontSize;
            _text.raycastTarget = false;
            _text.sortingOrder = 100;
            _text.outlineWidth = 0.2f;
        }
    }
}
