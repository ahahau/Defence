using System;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Entities
{
    [Serializable]
    public class AttackField
    {
        public Vector2 position;
        public float size;
        public string animationParam;
    }
    public class EntitySensor : MonoBehaviour
    {
        [SerializeField] private LayerMask targetLayer;
        
        [SerializeField] private List<AttackField> attackFields;

        
        public Vector2 BoxCastTarget(out RaycastHit2D hit)
        {
            foreach (var field in attackFields)
            {
                Vector2 center = (Vector2)transform.position + field.position;
                Vector2 size = Vector2.one * field.size;

                hit = Physics2D.BoxCast(center, size, 0, Vector2.zero, 0, targetLayer);
                if (hit.collider != null)
                    return field.position;
            }
            hit = new RaycastHit2D();
            return Vector2.zero;
        }

        public bool IsTargetInRange()
        {
            foreach(var field in attackFields)
            {
                Vector2 center = (Vector2)transform.position + field.position;
                Vector2 size = Vector2.one * field.size;

                Collider2D hitCollider = Physics2D.OverlapBox(center, size, 0, targetLayer);
                if (hitCollider != null)
                    return true;
            }
            return false;
            //hitCollider = Physics2D.OverlapCircle(transform.position, range, targetLayer);
            //return hitCollider != null;
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            
            foreach (var field in attackFields)
            {
                Vector2 center = (Vector2)transform.position + field.position;
                Vector2 size = Vector2.one * field.size;

                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}