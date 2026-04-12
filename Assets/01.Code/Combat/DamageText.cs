using System.Collections;
using DG.Tweening;
using GondrLib.ObjectPool.Runtime;
using TMPro;
using UnityEngine;

namespace _01.Code.Combat
{
    public class DamageText : MonoBehaviour, IPoolable
    {
        [field: SerializeField] public PoolingItemSO PoolingType { get; private set; }
        [SerializeField] private Vector3 worldOffset;
        [SerializeField] private float riseDistance = 0.75f;
        [SerializeField] private float lifetime = 0.7f;
        [SerializeField] private Color textColor = Color.red;

        private TextMeshPro _text;
        private Transform _followTarget;
        private Vector3 _spawnPosition;
        private Pool _pool;
        private Coroutine _followRoutine;
        private Sequence _animationSequence;
        private float _riseProgress;

        public GameObject GameObject
        {
            get { return gameObject; }
        }

        private void Awake()
        {
            _text = GetComponent<TextMeshPro>();
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
        }

        public void Initialize(float damage, Transform followTarget)
        {
            StopAnimation();
            _text = GetComponent<TextMeshPro>();
            if (_text == null)
            {
                Debug.LogError("DamageText requires a TextMeshPro component.", this);
                return;
            }

            _followTarget = followTarget;
            _riseProgress = 0f;
            _spawnPosition = followTarget != null ? followTarget.position : transform.position;

            _text.text = Mathf.CeilToInt(damage).ToString();
            _text.color = textColor;
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
        private void OnDisable()
        {
            StopAnimation();
        }
    }
}
