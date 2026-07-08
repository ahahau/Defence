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
        }

        private void Awake()
        {
            if (unit == null)
                unit = GetComponent<Unit>();

            if (!TryGetComponent<Collider2D>(out _))
                Debug.LogError($"{nameof(UnitClickTarget)} requires a Collider2D configured on the prefab.", this);
        }
    }
}
