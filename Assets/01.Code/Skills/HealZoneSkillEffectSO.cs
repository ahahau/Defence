using _01.Code.BT;
using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>힐 장판: 시전자 위치에 깔리고, 위에 있는 아군을 틱마다 회복한다.
    /// 선택적으로 장판 위 아군의 받는 피해를 줄이는 가호 효과도 준다.</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Heal Zone", fileName = "HealZoneSkillEffect", order = 0)]
    public class HealZoneSkillEffectSO : SkillEffectSO
    {
        [SerializeField, Min(0.1f)] private float radius = 2.2f;
        [SerializeField, Min(0.1f)] private float duration = 4f;
        [SerializeField, Min(0.1f)] private float tickInterval = 0.8f;
        [SerializeField, Min(0)] private int tickHeal = 1;
        [SerializeField, Range(0.05f, 1f), Tooltip("장판 위 아군이 받는 피해 배율. 1이면 가호 없음.")]
        private float damageTakenMultiplier = 1f;
        [SerializeField] private Color zoneColor = new(0.35f, 0.95f, 0.55f, 0.26f);

        public override void Execute(SkillContext context)
        {
            var caster = context.Caster;
            if (caster == null) return;

            var renderer = SkillZoneVisual.CreateZone("Heal Zone", caster.transform.position, radius, zoneColor);
            SkillZoneVisual.AddPulse(renderer);

            var runtime = renderer.gameObject.AddComponent<HealZoneRuntime>();
            runtime.Initialize(caster.Battlefield, caster.Team, radius, duration, tickInterval, tickHeal, damageTakenMultiplier);
        }

        private class HealZoneRuntime : MonoBehaviour
        {
            private NodeBattlefield _battlefield;
            private BattleTeam _team;
            private float _radius;
            private float _remaining;
            private float _tickInterval;
            private float _tickTimer;
            private int _tickHeal;
            private float _damageTakenMultiplier;

            public void Initialize(
                NodeBattlefield battlefield,
                BattleTeam team,
                float radius,
                float duration,
                float tickInterval,
                int tickHeal,
                float damageTakenMultiplier)
            {
                _battlefield = battlefield;
                _team = team;
                _radius = radius;
                _remaining = duration;
                _tickInterval = tickInterval;
                _tickTimer = 0f;
                _tickHeal = tickHeal;
                _damageTakenMultiplier = damageTakenMultiplier;
            }

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
                if (_battlefield == null) return;

                foreach (var ally in _battlefield.Allies(_team))
                {
                    if (ally == null || !ally.IsAlive)
                        continue;
                    if (Vector2.Distance(transform.position, ally.transform.position) > _radius)
                        continue;

                    if (_tickHeal > 0)
                        ally.Combatant?.Health?.Heal(_tickHeal);
                    if (_damageTakenMultiplier < 1f)
                        ally.ApplyCombatStatus("HealZoneGuard", _tickInterval + 0.15f, 1f, _damageTakenMultiplier);
                }
            }
        }
    }
}
