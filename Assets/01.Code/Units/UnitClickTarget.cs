using UnityEngine;

namespace _01.Code.Units
{
    public class UnitClickTarget : MonoBehaviour
    {
        [SerializeField] private Unit unit;

        public Unit Target => unit;

        public void Initialize(Unit targetUnit)
        {
            unit = targetUnit;
            EnsureCollider();
        }

        private void Awake()
        {
            if (unit == null)
                unit = GetComponent<Unit>();

            EnsureCollider();
        }

        private void EnsureCollider()
        {
            if (TryGetComponent<Collider2D>(out _))
                return;

            var boxCollider = gameObject.AddComponent<BoxCollider2D>();
            boxCollider.size = new Vector2(0.8f, 0.8f);
            boxCollider.offset = new Vector2(0f, 0.25f);
            boxCollider.isTrigger = true;
        }
    }
}
