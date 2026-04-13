using System.Collections.Generic;
using _01.Code.Buildings;
using _01.Code.Entities;
using _01.Code.Manager;
using GondrLib.ObjectPool.Runtime;
using UnityEngine;
using UnityEngine.Events;

namespace _01.Code.Enemies
{
    public class Enemy : Entity, IPoolable
    {
        [field: SerializeField] public PoolingItemSO PoolingType { get; private set; }

        private EnemyMovement _movement;
        private EnemyHealth _enemyHealth;
        private Rigidbody2D _rigidbody2D;
        private Pool _pool;
        private EnemySpawner _parentSpawner;
        private bool _deathNotified;
        private bool _returnedToPool;
        private EnemyRender _enemyRender;
        private EnemyDataSO _runtimeData;
        private CommandCenter _commandCenter;

        public GameObject GameObject
        {
            get { return gameObject; }
        }

        public void Initialize(List<Vector2Int> path, EnemySpawner parent, EnemyDataSO data, int level = 0)
        {
            _parentSpawner = parent;
            _movement = GetModule<EnemyMovement>();
            _enemyHealth = GetModule<EnemyHealth>();
            _enemyRender = GetModule<EnemyRender>();
            _runtimeData = data;

            ResetRuntimeState();
            _enemyHealth.Initialize(data, level);
            _movement.SetSpeed(data.Speed);
            _movement.SetPath(path);
            _movement.MoveNext();
        }

        public void Initialize(CommandCenter commandCenter)
        {
            _commandCenter = commandCenter;
        }

        public void SetUpPool(Pool pool)
        {
            _pool = pool;
            _rigidbody2D = GetComponent<Rigidbody2D>();
            CacheVisualTransform();
        }

        public void ResetItem()
        {
            ResetRuntimeState();
            _movement = GetModule<EnemyMovement>();
            _enemyHealth = GetModule<EnemyHealth>();
            _movement?.ResetState();
            _enemyHealth?.ResetHealthToFull();
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
                _pool.Push(this);
                return;
            }

            Destroy(gameObject);
        }


        public void SetSpawnPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
            _rigidbody2D = GetComponent<Rigidbody2D>();
            if (_rigidbody2D != null)
            {
                _rigidbody2D.position = new Vector2(worldPosition.x, worldPosition.y);
                _rigidbody2D.linearVelocity = Vector2.zero;
            }
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
            _runtimeData = null;

            OnHit = OnHit ?? new UnityEvent();
            OnDeath = OnDeath ?? new UnityEvent();
            OnDeath.RemoveAllListeners();
            OnDeath.AddListener(HandleDeath);
        }

        public void ReachDestination()
        {
            if (_returnedToPool)
            {
                return;
            }

            ApplyCommandCenterDamage();
            _parentSpawner?.EnemyDied(this);
            ReturnToPool();
        }

        public void RecalculatePath(GridManager gridManager)
        {
            if (_returnedToPool || gridManager == null || _commandCenter == null || _movement == null)
            {
                return;
            }

            Vector2Int start = gridManager.WorldToPlacementCell(transform.position);
            Vector2Int end = _commandCenter.GridPosition;
            List<Vector2Int> recalculatedPath = gridManager.PathFinder.FindPath(start, end);
            if (recalculatedPath == null || recalculatedPath.Count == 0)
            {
                return;
            }

            _movement.SetPath(recalculatedPath);
            _movement.MoveNext();
        }

        private void ApplyCommandCenterDamage()
        {
            if (_runtimeData == null)
            {
                return;
            }

            if (_commandCenter == null)
            {
                return;
            }

            if (_commandCenter.TryGetComponent(out _01.Code.Combat.IDamageable damageable))
            {
                damageable.ApplyDamage(_runtimeData.Damage, this);
            }
        }

        private void CacheVisualTransform()
        {
            _enemyRender = GetModule<EnemyRender>();
        }
    }
}
