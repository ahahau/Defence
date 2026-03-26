using System.Collections.Generic;
using _01.Code.Entities;
using DG.Tweening;
using GondrLib.ObjectPool.Runtime;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Enemies
{
    public class Enemy : Entity, IPoolable
    {
        [SerializeField] private PoolingItemSO poolingType;

        private EnemyMovement _movement;
        private EnemyHealth _enemyHealth;
        private Rigidbody2D _rigidbody2D;
        private Pool _pool;
        private EnemySpawner _parentSpawner;
        private Transform _visualTransform;
        private Tween _scaleTween;
        private Tween _rotationTween;
        private bool _deathNotified;
        private bool _returnedToPool;
        private EnemyRender _enemyRender;

        public PoolingItemSO PoolingType => poolingType;
        public GameObject GameObject => gameObject;

        public void Initialize(List<Vector2Int> path, EnemySpawner parent, EnemyDataSO data, int level = 0)
        {
            _parentSpawner = parent;
            _movement = GetModule<EnemyMovement>();
            _enemyHealth = GetModule<EnemyHealth>();
            _enemyRender = GetModule<EnemyRender>();

            ResetRuntimeState();
            _enemyHealth.Initialize(data, level);
            _movement.SetSpeed(data.Speed);
            _movement.SetPath(path);
            _movement.MoveNext();
            PlayScaleEffect();
        }

        public void SetUpPool(Pool pool)
        {
            _pool = pool;
            _rigidbody2D ??= GetComponent<Rigidbody2D>();
            CacheVisualTransform();
        }

        public void ResetItem()
        {
            ResetRuntimeState();
            _movement ??= GetModule<EnemyMovement>();
            _enemyHealth ??= GetModule<EnemyHealth>();
            _movement?.ResetState();
            _enemyHealth?.ResetHealthToFull();
            StopScaleEffect();
        }

        public void ReturnToPool()
        {
            if (_returnedToPool)
            {
                return;
            }

            _returnedToPool = true;
            if (_pool != null)
            {
                StopScaleEffect();
                _pool.Push(this);
                return;
            }

            StopScaleEffect();
            Destroy(gameObject);
        }


        public void SetSpawnPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            _rigidbody2D ??= GetComponent<Rigidbody2D>();
            if (_rigidbody2D != null)
            {
                _rigidbody2D.position = new Vector2(worldPosition.x, worldPosition.y);
                _rigidbody2D.linearVelocity = Vector2.zero;
            }
            
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!other.gameObject.CompareTag("CC"))
            {
                return;
            }

            other.gameObject.TryGetComponent<EntityHealth>(out var health);
            health?.ApplyDamage(4, this);
            NotifyDeath();
            ReturnToPool();
        }

        private void NotifyDeath()
        {
            if (_deathNotified)
            {
                return;
            }

            _deathNotified = true;
            IsDead = true;
            OnDeath?.Invoke();
        }

        private void HandleDeath()
        {
            _parentSpawner?.EnemyDied(this);
        }

        private void ResetRuntimeState()
        {
            _returnedToPool = false;
            _deathNotified = false;
            IsDead = false;

            OnHit ??= new UnityEvent();
            OnDeath ??= new UnityEvent();
            OnDeath.RemoveAllListeners();
            OnDeath.AddListener(HandleDeath);
        }

        private void CacheVisualTransform()
        {
            _enemyRender ??= GetModule<EnemyRender>();
            _visualTransform = _enemyRender != null ? _enemyRender.transform : transform;
        }

        private void PlayScaleEffect()
        {
            CacheVisualTransform();
            StopScaleEffect();
            float duration = Random.Range(0.5f, 1f);
            float upScale = Random.Range(1.2f, 1.25f);
            float rotationAngle = Random.Range(2f, 6f);

            _scaleTween = _visualTransform
                .DOScale(Vector2.one * upScale, duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);

            _visualTransform.localRotation = Quaternion.Euler(0f, 0f, -rotationAngle);

            _rotationTween = _visualTransform
                .DOLocalRotate(new Vector3(0f, 0f, rotationAngle), duration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        private void StopScaleEffect()
        {
            _scaleTween?.Kill();
            _scaleTween = null;
            _rotationTween?.Kill();
            _rotationTween = null;

            if (_visualTransform != null)
            {
                _visualTransform.localScale = Vector3.one;
                _visualTransform.localRotation = Quaternion.identity;
            }
        }
    }
}
