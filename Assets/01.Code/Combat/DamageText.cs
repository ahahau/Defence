using System.Collections;
using _01.Code.Manager;
using DG.Tweening;
using GondrLib.ObjectPool.Runtime;
using TMPro;
using UnityEngine;

namespace _01.Code.Combat
{
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageText : MonoBehaviour, IPoolable
    {
        [SerializeField] private PoolingItemSO poolingType;
        [SerializeField] private Vector3 worldOffset;
        [SerializeField] private float riseDistance = 0.75f;
        [SerializeField] private float lifetime = 0.7f;
        [SerializeField] private float fontSize = 4f;
        [SerializeField] private Color textColor = Color.red;

        private TextMeshPro _text;
        private Transform _followTarget;
        private Color _baseColor;
        private Vector3 _spawnPosition;
        private Pool _pool;
        private Coroutine _followRoutine;
        private Sequence _animationSequence;
        private float _riseProgress;

        public PoolingItemSO PoolingType => poolingType;
        public GameObject GameObject => gameObject;

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
            ApplyTextStyle();
        }

        private void OnValidate()
        {
            _text = GetComponent<TextMeshPro>();
            ApplyTextStyle();
        }

        public void SetUpPool(Pool pool)
        {
            _pool = pool;
        }

        public void ResetItem()
        {
            StopAnimation();
            _text = GetComponent<TextMeshPro>();
            _followTarget = null;
            _spawnPosition = Vector3.zero;
            _riseProgress = 0f;

            if (_text == null)
            {
                return;
            }

            _text.text = " ";
            _text.color = textColor;
            transform.position = Vector3.zero;
            ApplyTextStyle();
        }

        public void Initialize(float damage, Transform followTarget)
        {
            StopAnimation();
            _text = GetComponent<TextMeshPro>();
            if (_text == null)
            {
                GameManager.Instance.LogManager.System("DamageText requires a TextMeshPro component.", LogLevel.Error);
                return;
            }

            _followTarget = followTarget;
            _baseColor = textColor;
            _riseProgress = 0f;
            _spawnPosition = followTarget != null ? followTarget.position : transform.position;

            _text.font = TMP_Settings.defaultFontAsset;
            _text.text = Mathf.CeilToInt(damage).ToString();
            _text.color = _baseColor;
            ApplyTextStyle();
            SetPosition();
            StartAnimation();
            _followRoutine = StartCoroutine(FollowTargetRoutine());
        }

        private void SetPosition()
        {
            Vector3 basePosition = _followTarget != null ? _followTarget.position : _spawnPosition;
            transform.position = basePosition + worldOffset + (Vector3.up * (riseDistance * _riseProgress));
        }

        private IEnumerator FollowTargetRoutine()
        {
            while (_animationSequence != null && _animationSequence.IsActive() && _animationSequence.IsPlaying())
            {
                SetPosition();
                yield return null;
            }
        }

        private void StartAnimation()
        {
            _animationSequence = DOTween.Sequence();
            _animationSequence
                .Join(DOTween.To(() => _riseProgress, value => _riseProgress = value, 1f, lifetime).SetEase(Ease.OutQuad))
                .Join(_text.DOFade(0f, lifetime).SetEase(Ease.Linear))
                .OnComplete(ReturnToPool);
        }

        private void StopAnimation()
        {
            if (_followRoutine != null)
            {
                StopCoroutine(_followRoutine);
                _followRoutine = null;
            }

            if (_animationSequence != null)
            {
                _animationSequence.Kill();
                _animationSequence = null;
            }
        }

        private void ReturnToPool()
        {
            StopAnimation();

            if (_pool != null)
            {
                _pool.Push(this);
                return;
            }

            Destroy(gameObject);
        }


        private void ApplyTextStyle()
        {
            if (_text == null)
            {
                return;
            }

            _text.alignment = TextAlignmentOptions.Center;
            _text.fontSize = fontSize;
            _text.raycastTarget = false;
            _text.sortingOrder = 100;
            _text.outlineWidth = 0.2f;
        }

        private void OnDisable()
        {
            StopAnimation();
        }
    }
}
