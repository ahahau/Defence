using Code.Combat;
using UnityEngine;

namespace Code.Stages
{
    public sealed class StageHealthBar : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Transform fill;
        private Vector3 _size;

        private void Awake() => _size = fill.localScale;

        private void LateUpdate()
        {
            fill.localScale = new Vector3(_size.x * health.CurrentRatio, _size.y, _size.z);
        }
    }
}
