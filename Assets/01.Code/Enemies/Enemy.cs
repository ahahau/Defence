using System;
using System.Collections;
using System.Collections.Generic;
using _01.Code.Entities;
using DG.Tweening;
using UnityEngine;

namespace _01.Code.Enemies
{
    public class Enemy : Entity
    {
        private List<Vector2Int> _path;
        public void Initalize(List<Vector2Int> path)
        {
            _path = path;
            OnDeath += OnDeadHandle;
            StartCoroutine(Move());
        }

        private void OnDestroy()
        {
            OnDeath -= OnDeadHandle;
        }

        IEnumerator Move()
        {
            foreach (var pos in _path)
            {
                Vector3 targetPos = new Vector3(pos.x, pos.y, 0);
                Tween t = transform.DOMove(targetPos, 0.2f).SetEase(Ease.Linear);
                yield return t.WaitForCompletion();
            }
        }
        
        private void OnDeadHandle()
        {
            Destroy(gameObject);
        }
    }
}