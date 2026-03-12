using System;
using System.Collections;
using System.Collections.Generic;
using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class Enemy : Entity
    {
        private EnemyRender _render;
        private EnemyMovement _movement;
        private EnemyHealth _enemyHealth;
        private List<Vector2Int> _path;
        private bool _deathNotified;

        public void Initialize(List<Vector2Int> path, EnemySpawner parent/*, EnemyDataSO data, int level*/)
        {
            _path = path;
            _render = GetModule<EnemyRender>();
            _movement = GetModule<EnemyMovement>();
            _enemyHealth = GetModule<EnemyHealth>();
            // TODO : 이거 고쳐야함
            //_enemyHealth.Initialize(data, level);
            OnDeath?.AddListener(() =>
            {
                parent.EnemyDied(this);
            });
            _movement.SetPath(path);
            _movement.MoveNext();
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

        public void OnMoveDirection(Vector2 dir)
        {
            //_render.ChangeAnimation(dir);
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("CC"))
            {
                other.gameObject.TryGetComponent<EntityHealth>(out var health);
                health?.ApplyDamage(4, this);
                NotifyDeath();
                Destroy(gameObject);
            }
        }
    }
}
