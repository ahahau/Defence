using System.Collections.Generic;
using _01.Code.Combat;
using _01.Code.Manager;
using _01.Code.Modules;
using UnityEngine;

namespace _01.Code.Entities
{
    public class EntitySensor : MonoBehaviour, IModule
    {
        [SerializeField] private LayerMask targetLayer;
        [SerializeField] private AttackPatternDataSO attackPatternData;
        [SerializeField] private List<Vector2Int> attackOffsets = new();
        [SerializeField] private int cellSearchSize = 1;

        public AttackPatternDataSO AttackPatternData => attackPatternData;
        public List<AttackPatternData> AttackPatterns => HasPatternData()
            ? attackPatternData.AttackOffsets
            : _fallbackPatterns;

        private readonly List<AttackPatternData> _fallbackPatterns = new();
        private GridManager _gridManager;

        public bool TryGetTargetCollider(Vector2Int originCell, out Collider2D targetCollider)
        {
            List<AttackPatternData> patterns = AttackPatterns;
            for (int patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                AttackPatternData pattern = patterns[patternIndex];
                if (pattern == null || pattern.attackOffsets == null)
                {
                    continue;
                }

                for (int offsetIndex = 0; offsetIndex < pattern.attackOffsets.Count; offsetIndex++)
                {
                    Vector2Int targetCell = originCell + pattern.attackOffsets[offsetIndex];
                    if (TryGetColliderAtCell(targetCell, pattern.cellSearchSize, out targetCollider))
                    {
                        return true;
                    }
                }
            }

            targetCollider = null;
            return false;
        }

        public bool TryGetTargetEntity(Vector2Int originCell, out Entity targetEntity)
        {
            if (TryGetTargetCollider(originCell, out Collider2D targetCollider) &&
                TryResolveTargetEntity(targetCollider, out targetEntity))
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
                TryResolveTargetEntity(targetCollider, out targetEntity);
                if (TryResolveDamageable(targetCollider, out damageable))
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


        private void OnValidate()
        {
            RefreshFallbackPatterns();
        }

        private bool TryGetColliderAtCell(Vector2Int targetCell, int searchSizeValue, out Collider2D targetCollider)
        {
            Vector2 worldCenter = GetWorldCenter(targetCell);
            Vector2 searchSize = Vector2.one * Mathf.Max(1, searchSizeValue);
            targetCollider = Physics2D.OverlapBox(worldCenter, searchSize, 0f, targetLayer);
            return targetCollider != null;
        }

        private static bool TryResolveTargetEntity(Component source, out Entity targetEntity)
        {
            if (source.TryGetComponent(out targetEntity))
            {
                return true;
            }

            targetEntity = source.GetComponentInParent<Entity>();
            return targetEntity != null;
        }

        private static bool TryResolveDamageable(Component source, out IDamageable damageable)
        {
            if (source.TryGetComponent(out damageable))
            {
                return true;
            }

            damageable = source.GetComponentInParent<IDamageable>();
            return damageable != null;
        }

        private Vector2 GetWorldCenter(Vector2Int cellPosition)
        {
            if (_gridManager != null)
            {
                Vector3 cellWorld = _gridManager.CellToWorld(cellPosition);
                return new Vector2(cellWorld.x, cellWorld.y);
            }

            return cellPosition;
        }


        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            Vector2Int originCell = GetOriginCellForGizmos();
            List<AttackPatternData> patterns = AttackPatterns;
            foreach (var pattern in patterns)
            {
                if (pattern == null || pattern.attackOffsets == null)
                {
                    continue;
                }
                
                int gizmoSize = Mathf.Max(1, pattern.cellSearchSize);
                for (int offsetIndex = 0; offsetIndex < pattern.attackOffsets.Count; offsetIndex++)
                {
                    Vector2Int targetCell = originCell + pattern.attackOffsets[offsetIndex];
                    Vector2 worldCenter = GetWorldCenter(targetCell);
                    Gizmos.DrawWireCube(worldCenter, Vector3.one * gizmoSize);
                }
            }
        }

        private Vector2Int GetOriginCellForGizmos()
        {
            if (_gridManager != null)
            {
                return _gridManager.WorldToCell(transform.position);
            }

            return Vector2Int.RoundToInt(transform.position);
        }

        private bool HasPatternData()
        {
            return attackPatternData != null && attackPatternData.AttackOffsets != null && attackPatternData.AttackOffsets.Count > 0;
        }

        private void RefreshFallbackPatterns()
        {
            _fallbackPatterns.Clear();

            if (attackOffsets == null || attackOffsets.Count == 0)
            {
                return;
            }

            _fallbackPatterns.Add(new AttackPatternData
            {
                attackOffsets = new List<Vector2Int>(attackOffsets),
                cellSearchSize = Mathf.Max(1, cellSearchSize)
            });
        }

        public void Initialize(ModuleOwner owner)
        {
            if (owner is PlaceableEntity placeableEntity)
            {
                _gridManager = placeableEntity.GetComponentInParent<GridManager>();
            }

            if (_gridManager == null)
            {
                _gridManager = FindFirstObjectByType<GridManager>();
            }
            RefreshFallbackPatterns();
        }
    }
}
