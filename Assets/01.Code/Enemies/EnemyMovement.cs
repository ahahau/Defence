using System.Collections.Generic;
using _01.Code.Animation;
using _01.Code.Entities;
using UnityEngine;
using NotImplementedException = System.NotImplementedException;

namespace _01.Code.Enemies
{
    public class EnemyMovement : MonoBehaviour , IEntityComponent
    {
        private List<Vector2Int> _path;
        private Enemy _enemy;
        private EnemyRender _render;
        private Rigidbody2D rb;
        
        [SerializeField]private float moveSpeed = 5;
        
        public bool isMoving = false;
        public Vector2 targetPosition;
        public void Initialize(Entity entity)
        {
            _enemy = entity as Enemy;
            _render = entity.GetCompo<EnemyRender>();
            rb = entity.GetComponent<Rigidbody2D>();
            _path = _enemy.Path;
            isMoving = true;
        }

        private void Update()
        {
            Move();
        }


        private void Move()
        {
            if (!isMoving)
                return;
            
            Vector2 newPos = Vector2.MoveTowards(rb.position, targetPosition, moveSpeed * Time.deltaTime);
            rb.MovePosition(newPos);
        }

    }
}