using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Modules;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class EnemyMovement : MonoBehaviour, IModule
    {
        private Enemy _enemy;
        private Rigidbody2D _rb;
        private EnemyRender _enemyRender;
        private _01.Code.Manager.GridManager _gridManager;

        [SerializeField] private List<Vector2Int> _path;
        private int _pathIndex;

        [SerializeField] private float moveSpeed = 5f;

        private Tween _moveTween;

        public void Initialize(ModuleOwner owner)
        {
            _enemy = owner as Enemy;
            _rb = owner.GetComponent<Rigidbody2D>();
            _enemyRender = _enemy.GetModule<EnemyRender>();
            _gridManager = FindFirstObjectByType<_01.Code.Manager.GridManager>();
            _pathIndex = 0;
        }

        public void MoveNext()
        {
            if (_path == null || _pathIndex >= _path.Count)
            {
                _enemy?.ReachDestination();
                return;
            }

            Vector3 targetWorld = _gridManager.CellToObjectWorld(_path[_pathIndex]);
            Vector2 target = new Vector2(targetWorld.x, targetWorld.y);

            Vector2 currentPos = _rb.position;
            Vector2 dir = target - currentPos;


            float distance = dir.magnitude;
                float duration = distance / moveSpeed;
            
            _moveTween?.Kill();
            
            if(_pathIndex > 0)
                _enemyRender.ChangeAnimation(_path[_pathIndex] - _path[_pathIndex - 1]);
            
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
            _pathIndex = 0;
        }

        public void SetSpeed(float speed)
        {
            moveSpeed = speed;
        }

        public void ResetState()
        {
            _pathIndex = 0;
            _moveTween?.Kill();

            if (_rb != null)
            {
                _rb.linearVelocity = Vector2.zero;
            }
        }
    }
}
