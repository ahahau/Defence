using System;
using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Modules;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyMovement : MonoBehaviour , IModule
    {
        private Enemy _enemy;
        private Rigidbody2D _rb;

        [SerializeField]private List<Vector2Int> _path;
        private int _pathIndex;

        [SerializeField] private float moveSpeed = 5f;

        private Tween _moveTween;

        public void Initialize(ModuleOwner owner)
        {
            _enemy = owner as Enemy;
            _rb = owner.GetComponent<Rigidbody2D>();
            _pathIndex = 0;
        }

        private void Start()
        {
            MoveNext();
        }

        public void MoveNext()
        {
            if (_pathIndex >= _path.Count)
                return;

            Vector2 target = new Vector2(
                _path[_pathIndex].x,
                _path[_pathIndex].y
            );

            Vector2 currentPos = _rb.position;
            Vector2 dir = target - currentPos;

            _enemy.OnMoveDirection(dir);

            float distance = dir.magnitude;
            float duration = distance / moveSpeed;

            _moveTween?.Kill();

            _moveTween = _rb
                .DOMove(target, duration)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    _pathIndex++;
                    MoveNext();
                });
        }

        private void OnDisable()
        {
            _moveTween?.Kill();
        }

        public void SetPath(List<Vector2Int> path)
        {
            _path = path;
        }
    }
}