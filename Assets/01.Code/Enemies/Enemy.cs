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
        private EnemyRender _render;
        private EnemyMovement _movement;
        public List<Vector2Int> Path { get; private set; }

        public void Initialize(List<Vector2Int> path)
        {
            Path = path;
            _render = GetModule<EnemyRender>();
            _movement = GetModule<EnemyMovement>();
            
            _movement.SetPath(path);
            _movement.MoveNext();
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
                Destroy(gameObject);
            }
        }
    }
}