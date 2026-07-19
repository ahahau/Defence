using _01.Code.BT;
using _01.Code.Combat;
using DG.Tweening;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>지연 폭발 장판: 타깃 위치에 경고 장판을 깔고, 안쪽 원이 차오른 뒤 폭발해
    /// 범위 피해 + 넉백을 준다. 폭발 시 Feel 카메라 셰이크와 히트스톱이 재생된다.</summary>
    [CreateAssetMenu(menuName = "SO/Skill/Effect/Delayed Blast Zone", fileName = "DelayedBlastZoneSkillEffect", order = 0)]
    public class DelayedBlastZoneSkillEffectSO : SkillEffectSO
    {
        [Header("Zone")]
        [SerializeField, Min(0.1f)] private float radius = 1.8f;
        [SerializeField, Min(0.1f), Tooltip("경고 표시 후 폭발까지 걸리는 시간(초)")]
        private float delay = 0.9f;

        [Header("Damage")]
        [SerializeField, Min(0)] private int flatDamage = 8;
        [SerializeField, Min(0f), Tooltip("시전자 공격력 × 이 값을 추가 피해로.")]
        private float attackDamageMultiplier = 0.5f;
        [SerializeField, Min(0f)] private float knockbackDistance = 0.6f;

        [Header("Visual")]
        [SerializeField] private Color warningColor = new(1f, 0.25f, 0.15f, 0.2f);
        [SerializeField] private Color fillColor = new(1f, 0.42f, 0.12f, 0.4f);
        [SerializeField] private Color explosionColor = new(1f, 0.72f, 0.25f, 0.85f);

        [Header("Feel")]
        [SerializeField, Min(0f)] private float shakeDuration = 0.2f;
        [SerializeField, Min(0f)] private float shakeAmplitude = 0.16f;
        [SerializeField, Min(0f)] private float shakeFrequency = 25f;
        [SerializeField, Min(0f)] private float hitStopDuration = 0.045f;

        public override void Execute(SkillContext context)
        {
            var caster = context.Caster;
            if (caster == null) return;

            var center = context.Target != null
                ? context.Target.transform.position
                : caster.transform.position;

            var damage = flatDamage + (context.CasterCombatant != null
                ? Mathf.RoundToInt(context.CasterCombatant.AttackDamage * attackDamageMultiplier)
                : 0);

            var warning = SkillZoneVisual.CreateZone("Blast Warning", center, radius, warningColor);
            SkillZoneVisual.AddPulse(warning, 0.45f, 0.22f);

            // 안쪽에서 차오르는 원(텔레그래프). 경고 장판의 자식으로 두면 스케일이 곱해지므로 별도 GO로 만든다.
            var fill = SkillZoneVisual.CreateZone("Blast Warning Fill", center, radius, fillColor, 6);

            var runtime = warning.gameObject.AddComponent<DelayedBlastRuntime>();
            runtime.Initialize(
                caster.Battlefield,
                caster.Team,
                fill,
                radius,
                delay,
                damage,
                knockbackDistance,
                explosionColor,
                shakeDuration,
                shakeAmplitude,
                shakeFrequency,
                hitStopDuration);
        }

        private class DelayedBlastRuntime : MonoBehaviour
        {
            private NodeBattlefield _battlefield;
            private BattleTeam _team;
            private SpriteRenderer _fill;
            private float _radius;
            private float _timer;
            private int _damage;
            private float _knockbackDistance;
            private Color _explosionColor;
            private float _shakeDuration;
            private float _shakeAmplitude;
            private float _shakeFrequency;
            private float _hitStopDuration;

            public void Initialize(
                NodeBattlefield battlefield,
                BattleTeam team,
                SpriteRenderer fill,
                float radius,
                float delay,
                int damage,
                float knockbackDistance,
                Color explosionColor,
                float shakeDuration,
                float shakeAmplitude,
                float shakeFrequency,
                float hitStopDuration)
            {
                _battlefield = battlefield;
                _team = team;
                _fill = fill;
                _radius = radius;
                _timer = delay;
                _damage = damage;
                _knockbackDistance = knockbackDistance;
                _explosionColor = explosionColor;
                _shakeDuration = shakeDuration;
                _shakeAmplitude = shakeAmplitude;
                _shakeFrequency = shakeFrequency;
                _hitStopDuration = hitStopDuration;

                if (_fill != null)
                {
                    _fill.transform.localScale = Vector3.one * 0.01f;
                    _fill.transform.DOScale(Vector3.one * (radius * 2f), delay)
                        .SetEase(Ease.Linear)
                        .SetLink(_fill.gameObject);
                }
            }

            private void Update()
            {
                _timer -= Time.deltaTime;
                if (_timer > 0f) return;

                Explode();

                if (_fill != null)
                    Destroy(_fill.gameObject);
                Destroy(gameObject);
            }

            private void Explode()
            {
                if (_battlefield != null)
                {
                    foreach (var enemy in _battlefield.Opponents(_team))
                    {
                        if (enemy == null || !enemy.IsAlive)
                            continue;

                        var offset = (Vector2)enemy.transform.position - (Vector2)transform.position;
                        if (offset.magnitude > _radius)
                            continue;

                        enemy.TakeSkillDamage(_damage);

                        if (_knockbackDistance > 0f)
                        {
                            var dir = offset.sqrMagnitude > 0.0001f
                                ? offset.normalized
                                : Random.insideUnitCircle.normalized;
                            enemy.TeleportToCombatPosition((Vector2)enemy.transform.position + dir * _knockbackDistance);
                        }
                    }
                }

                PlayExplosionVisual();
                PlayFeelFeedback();
            }

            private void PlayExplosionVisual()
            {
                var flash = SkillZoneVisual.CreateZone("Blast Explosion", transform.position, _radius, _explosionColor, 7);
                flash.transform.localScale = Vector3.one * (_radius * 0.6f);

                DOTween.Sequence()
                    .Join(flash.transform.DOScale(Vector3.one * (_radius * 2.2f), 0.28f).SetEase(Ease.OutCubic))
                    .Join(flash.DOFade(0f, 0.28f).SetEase(Ease.OutQuad))
                    .OnComplete(() =>
                    {
                        if (flash != null)
                            Destroy(flash.gameObject);
                    })
                    .SetLink(flash.gameObject);
            }

            private void PlayFeelFeedback()
            {
                FeelCombatSceneSetup.EnsureCameraShaker();

                if (_shakeAmplitude > 0f && _shakeDuration > 0f)
                {
                    // X/Y만 흔들어 2D 직교 카메라의 Z축 흔들림을 막는다. 히트스톱과 겹치므로 unscaled로 재생.
                    MMCameraShakeEvent.Trigger(
                        _shakeDuration, 0f, _shakeFrequency,
                        _shakeAmplitude, _shakeAmplitude, 0f,
                        false, new MMChannelData(MMChannelModes.Int, 0, null), true);
                }

                if (_hitStopDuration > 0f)
                    HitStopRunner.Play(_hitStopDuration, 0.05f, 0.1f);
            }
        }
    }
}
