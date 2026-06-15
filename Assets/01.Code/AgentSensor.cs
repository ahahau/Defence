using UnityEngine;

namespace _01.Code
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

        /// <summary>Returns the closest target on the target layer within range, or null.
        /// Used by team-fight combat to pick an opponent.</summary>
        public Collider2D FindNearestTarget(float range)
        {
            var hits = Physics2D.OverlapCircleAll(transform.position, range, targetLayer);
            Collider2D nearest = null;
            var bestSqr = float.MaxValue;
            Vector2 origin = transform.position;

            foreach (var hit in hits)
            {
                if (hit == null)
                    continue;

                var sqr = ((Vector2)hit.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    nearest = hit;
                }
            }

            return nearest;
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