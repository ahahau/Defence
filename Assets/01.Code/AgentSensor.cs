using System;
using UnityEngine;

namespace Code.Agents
{
    
    public class AgentSensor : MonoBehaviour
    {
        [SerializeField] private LayerMask obstacleLayer;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private LayerMask targetLayer;
        
        [SerializeField] private Vector2 boxSize;
        [SerializeField] private Vector2 offset;

        public bool IsObstaclePresent(Vector2 direction, out Collider2D hitCollider)
        {
            Vector2 position = (Vector2)transform.position + direction;
            hitCollider = Physics2D.OverlapBox(position + offset, boxSize, 0, obstacleLayer);
            
            return hitCollider != null;
        }

        public float BoxCastObstacle(Vector2 direction, float distance, out RaycastHit2D hit)
        {
            hit = Physics2D.BoxCast((Vector2)transform.position + offset, boxSize, 0, 
                direction, distance, obstacleLayer);
            
            distance = hit ? hit.distance : distance;
            return distance;
        }

        public float BoxCastGround(float distance, out RaycastHit2D hit)
        {
            hit = Physics2D.BoxCast((Vector2)transform.position + offset, boxSize, 0, 
                Vector2.down, distance, obstacleLayer);

            distance = hit ? hit.distance : distance;
            return distance;
        }

        public bool IsTargetInRange(float range, out Collider2D hitCollider)
        {
            hitCollider = Physics2D.OverlapCircle(transform.position, range, targetLayer);
            return hitCollider != null;
        }

        public bool IsTargetInSight(Vector3 startPosition, float range, Collider2D target)
        {
            Vector2 direction = target.transform.position - startPosition;
            RaycastHit2D hit = Physics2D.Raycast(startPosition, direction.normalized, direction.magnitude, obstacleLayer);
            return hit.collider == null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube( transform.position + (Vector3)offset, boxSize);
        }
    }
}