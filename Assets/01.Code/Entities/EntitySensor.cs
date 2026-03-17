using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Entities
{
    public class EntitySensor : MonoBehaviour
    {
        [SerializeField] private LayerMask targetLayer;
        [SerializeField] private AttackPatternDataSO attackPatternData;
        [SerializeField] private List<Vector2Int> attackOffsets = new();

        public AttackPatternDataSO AttackPatternData => attackPatternData;
        public IReadOnlyList<Vector2Int> AttackOffsets => HasPatternOffsets() ? attackPatternData.AttackOffsets : attackOffsets;

        public bool TryGetTargetCollider(Vector2Int originCell, out Collider2D targetCollider)
        {
            IReadOnlyList<Vector2Int> offsets = AttackOffsets;
            for (int i = 0; i < offsets.Count; i++)
            {
                Vector2Int targetCell = originCell + offsets[i];
                if (TryGetColliderAtCell(targetCell, out targetCollider))
                {
                    return true;
                }
            }

            targetCollider = null;
            return false;
        }

        public bool TryGetTargetEntity(Vector2Int originCell, out Entity targetEntity)
        {
            if (TryGetTargetCollider(originCell, out Collider2D targetCollider) &&
                targetCollider.TryGetComponent(out targetEntity))
            {
                return true;
            }

            targetEntity = null;
            return false;
        }

        public bool TryGetDamageableTarget(Vector2Int originCell, out IDamageable damageable, out Entity targetEntity)
        {
            if (TryGetTargetCollider(originCell, out Collider2D targetCollider))
            {
                targetCollider.TryGetComponent(out targetEntity);
                if (targetCollider.TryGetComponent(out damageable))
                {
                    return true;
                }
            }
            
            damageable = null;
            targetEntity = null;
            return false;
        }

        public bool IsTargetInRange(Vector2Int originCell)
        {
            return TryGetTargetCollider(originCell, out _);
        }

        private bool TryGetColliderAtCell(Vector2Int targetCell, out Collider2D targetCollider)
        {
            Vector2 worldCenter = GetWorldCenter(targetCell);
            targetCollider = Physics2D.OverlapBox(worldCenter, Vector2.one, 0f, targetLayer);
            return targetCollider != null;
        }

        private Vector2 GetWorldCenter(Vector2Int cellPosition)
        {
            if (GameManager.Instance != null && GameManager.Instance.GridManager != null)
            {
                Vector3 cellWorld = GameManager.Instance.GridManager.Grid.CellToWorld(new Vector3Int(cellPosition.x, cellPosition.y, 0));
                return new Vector2(cellWorld.x, cellWorld.y);
            }

            return cellPosition;
        }

        private Vector2Int GetGizmoOriginCell()
        {
            if (GameManager.Instance != null && GameManager.Instance.GridManager != null)
            {
                return GameManager.Instance.GridManager.Tilemap.WorldToCell(transform.position);
            }

            return Vector2Int.RoundToInt(transform.position);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            Vector2Int originCell = GetGizmoOriginCell();
            IReadOnlyList<Vector2Int> offsets = AttackOffsets;
            for (int i = 0; i < offsets.Count; i++)
            {
                Vector2Int targetCell = originCell + offsets[i];
                Vector2 worldCenter = GetWorldCenter(targetCell);
                Gizmos.DrawWireCube(worldCenter, Vector3.one);
            }
        }

        private bool HasPatternOffsets()
        {
            return attackPatternData != null && attackPatternData.AttackOffsets != null && attackPatternData.AttackOffsets.Count > 0;
        }
    }
}
