using Code.Combat;
using Code.UI;
using UnityEngine;

namespace Code.Stages
{
    public sealed class StageHealthBar : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Transform fill;
        private Vector3 _size;
        private SpriteRenderer _fillRenderer;
        private StageBarricade _barricade;

        private void Awake()
        {
            _size = fill.localScale;
            _fillRenderer = fill.GetComponent<SpriteRenderer>();
            _barricade = GetComponent<StageBarricade>();
        }

        private void LateUpdate()
        {
            fill.localScale = new Vector3(_size.x * health.CurrentRatio, _size.y, _size.z);
            if (_fillRenderer != null)
                _fillRenderer.color = _barricade != null && _barricade.IsRecovering ? HudPalette.Info
                    : health.CurrentRatio <= 0.3f ? HudPalette.Danger
                    : health.CurrentRatio < 1f ? HudPalette.Warn : HudPalette.Gain;
        }
    }
}
