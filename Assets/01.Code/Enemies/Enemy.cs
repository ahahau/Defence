using System;
using System.Collections;
using System.Collections.Generic;
using _01.Code.Entities;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

namespace _01.Code.Enemies
{
    public class Enemy : Entity
    {
        private EnemyMovement _movement;
        private EnemyRender _render;
        
        
        private int _currentPathIndex;
        private Vector2 _targetPosition;
        
        public List<Vector2Int> Path {get; private set;}
        public void Initialize(List<Vector2Int> path)
        {
            base.Initialize();
            Path = path;
            _movement = GetCompo<EnemyMovement>();
            _render = GetCompo<EnemyRender>();
            _targetPosition = new Vector2(transform.position.x, transform.position.y);
            _currentPathIndex = 0;
        }

        private void Update()
        {
            if (Mathf.Abs(_targetPosition.y - transform.position.y) <= 0.05f && Mathf.Abs(_targetPosition.x - transform.position.x) <= 0.05f)
            {
                _currentPathIndex++;
                if (_currentPathIndex >= Path.Count)
                {
                    _movement.isMoving = false;
                    return;
                }
                _targetPosition = new Vector2(Path[_currentPathIndex].x, Path[_currentPathIndex].y);
                _render.ChangeAnimation(_targetPosition - new Vector2(transform.position.x, transform.position.y));
                _movement.targetPosition = _targetPosition;
            }
        }
    }
}