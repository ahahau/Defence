using System;
using System.Collections.Generic;
using _01.Code.Entities;
using _01.Code.Manager;
using Unity.Mathematics;
using UnityEngine;

namespace _01.Code.PlaceableObjects.Buildings
{
    [Serializable]
    public class AttackField
    {
        public Vector2 position;
        public float size;
    }
    public class BuildingAttackCompo : MonoBehaviour, IEntityComponent
    {
        [SerializeField] private List<AttackField> attackFields;
        [SerializeField] private float attackDelay;
        [SerializeField] private LayerMask enemyLayer;
        public Action AttackEvent;
        
        public void Initialize(Entity entity)
        {
            
        }

        public void AttackChecker()
        {
            if (AttackEvent == null)
                return;

            for (int i = 0; i < attackFields.Count; i++)
            {
                var field = attackFields[i];

                Vector2 center = field.position;
                Vector2 size = Vector2.one * field.size;

                Collider2D hit = Physics2D.OverlapBox(
                    center,
                    size,
                    0f,
                    enemyLayer
                );

                if (hit != null)
                {
                    AttackEvent.Invoke();
                    return;
                }
            }
        }

        private void OnDrawGizmos()
        {
            foreach (var field in attackFields)
            {
                float half = field.size * 0.5f;

                float cx = field.position.x;
                float cy = field.position.y;
                Vector2 topLeft = new Vector2(cx - half, cy + half);
                Vector2 topRight = new Vector2(cx + half, cy + half);
                Vector2 bottomLeft = new Vector2(cx - half, cy - half);
                Vector2 bottomRight = new Vector2(cx + half, cy - half);
                Gizmos.DrawLine(topLeft, topRight);
                Gizmos.DrawLine(topRight, bottomRight);
                Gizmos.DrawLine(bottomRight, bottomLeft);
                Gizmos.DrawLine(bottomLeft, topLeft);
            }
        }
    }
}