using _01.Code.Combat;
using UnityEngine;

namespace _01.Code.Skills
{
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Ground Zone", fileName = "GroundZoneSkillEffect", order = 0)]
    public class GroundZoneSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0.1f)] private float radius = 2.2f;
        [SerializeField, Min(0.1f)] private float duration = 3f;
        [SerializeField, Min(0.1f)] private float tickInterval = 0.5f;
        [SerializeField, Min(0)] private int tickDamage = 1;
        [SerializeField, Min(0.05f)] private float moveSpeedMultiplier = 0.45f;
        [SerializeField] private Color zoneColor = new(0.45f, 0.9f, 0.25f, 0.28f);

        public override void Execute(SkillContext context)
        {
            if (context.Caster == null) return;

            var center = context.Target != null
                ? context.Target.transform.position
                : context.Caster.transform.position;

            var renderer = SkillZoneVisual.CreateZone("Skill Zone", center, radius, zoneColor);
            SkillZoneVisual.AddPulse(renderer);

            var runtime = renderer.gameObject.AddComponent<GroundZoneRuntime>();
            runtime.Initialize(context.Caster, radius, duration, tickInterval, tickDamage, moveSpeedMultiplier);
        }

        private class GroundZoneRuntime : MonoBehaviour
        {
            private const string OwnershipSlot = "GroundZone";
            private BT.BattleAgent _caster;
            private int _ownerId;
            private float _radius;
            private float _remaining;
            private float _tickInterval;
            private float _tickTimer;
            private int _tickDamage;
            private float _moveSpeedMultiplier;

            public void Initialize(BT.BattleAgent caster, float radius, float duration, float tickInterval, int tickDamage, float moveSpeedMultiplier)
            {
                _caster = caster;
                _radius = radius;
                _remaining = duration;
                _tickInterval = tickInterval;
                _tickTimer = 0f;
                _tickDamage = tickDamage;
                _moveSpeedMultiplier = moveSpeedMultiplier;
                _ownerId = SkillZoneOwnership.Replace(caster, OwnershipSlot, gameObject);
            }

            private void OnDestroy() =>
                SkillZoneOwnership.Release(_ownerId, OwnershipSlot, gameObject);

            private void Update()
            {
                _remaining -= Time.deltaTime;
                if (_remaining <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }

                _tickTimer -= Time.deltaTime;
                if (_tickTimer > 0f) return;

                _tickTimer = _tickInterval;
                var field = _caster != null ? _caster.Battlefield : null;
                if (field == null) return;

                foreach (var enemy in field.Opponents(_caster.Team))
                {
                    if (enemy == null || !enemy.IsAlive)
                        continue;
                    if (Vector2.Distance(transform.position, enemy.transform.position) > _radius)
                        continue;

                    enemy.ApplyCombatStatus("GroundZoneSlow", _tickInterval + 0.15f, _moveSpeedMultiplier, 1f);
                    enemy.TakeSkillDamage(_tickDamage);
                }
            }
        }
    }
}
