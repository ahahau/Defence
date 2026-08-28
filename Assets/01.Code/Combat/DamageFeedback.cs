using DG.Tweening;
using System.Collections;
using UnityEngine;

namespace _01.Code.Combat
{
    [RequireComponent(typeof(Health))]
    public class DamageFeedback : MonoBehaviour
    {
        [SerializeField] private float textFloatDistance = 0.45f;
        [SerializeField] private float textDuration = 0.55f;
        [SerializeField] private int textSortingOrder = 60;
        [Header("Critical")]
        [SerializeField] private Color criticalTextColor = new(1f, 0.72f, 0.1f, 1f);
        [SerializeField, Min(1f), Tooltip("크리티컬 데미지 텍스트 크기 배율.")]
        private float criticalTextScale = 1.5f;
        [Header("Miss")]
        [SerializeField] private Color missTextColor = new(0.85f, 0.85f, 0.85f, 1f);
        [Header("Heal")]
        [SerializeField] private Color healTextColor = new(0.42f, 1f, 0.45f, 1f);
        [SerializeField] private Color healParticleColor = new(0.4f, 1f, 0.5f, 1f);
        [SerializeField, Min(1)] private int healParticleBurstCount = 10;
        [SerializeField, Min(0f), Tooltip("잦은 소량 힐(자연회복 등)을 하나의 텍스트로 합산하는 시간 창.")]
        private float healTextAggregateWindow = 0.4f;
        [SerializeField] private TextMesh damageTextPrefab;
        [SerializeField] private ParticleSystem hitParticles;
        [SerializeField] private Health health;
        [SerializeField] private Color hitParticleColor = new(1f, 0.18f, 0.06f, 1f);
        [SerializeField, Min(1)] private int hitParticleBurstCount = 18;
        [SerializeField, Min(0f)] private float hitParticleYOffset = 0.35f;
        [SerializeField] private int hitParticleSortingOrder = 80;

        private ParticleSystem _healParticles;
        private int _pendingHealAmount;
        private Coroutine _healTextRoutine;
        private void Awake()
        {
            if (health == null)
                health = GetComponent<Health>();

            EnsureDefaultHitParticles();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.DamagedDetailed += Play;
                health.Healed += PlayHeal;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.DamagedDetailed -= Play;
                health.Healed -= PlayHeal;
            }

            if (_healTextRoutine != null)
            {
                StopCoroutine(_healTextRoutine);
                _healTextRoutine = null;
                _pendingHealAmount = 0;
            }

        }

        private void Play(int damage, bool isCritical)
        {
            if (damage <= 0)
                return;

            PlayHitParticles();
            CreateDamageText(damage, isCritical);
        }

        /// <summary>회복 연출 — 초록 블룸 + 상승 스파클. 텍스트는 잦은 소량 힐이 겹치지 않게 짧게 합산해 표시.</summary>
        private void PlayHeal(int amount)
        {
            if (amount <= 0)
                return;

            PlayHealParticles();

            _pendingHealAmount += amount;
            if (_healTextRoutine == null && isActiveAndEnabled)
                _healTextRoutine = StartCoroutine(FlushHealText());
        }

        private IEnumerator FlushHealText()
        {
            yield return new WaitForSeconds(healTextAggregateWindow);

            var total = _pendingHealAmount;
            _pendingHealAmount = 0;
            _healTextRoutine = null;

            if (total > 0)
                SpawnFloatingText($"+{total}", healTextColor, 0.9f);
        }

        private void PlayHitParticles()
        {
            if (hitParticles == null)
                return;

            hitParticles.transform.position = transform.position + Vector3.up * hitParticleYOffset;
            hitParticles.Play(true);
        }

        private void PlayHealParticles()
        {
            if (_healParticles == null)
                _healParticles = CreateHealParticles();

            _healParticles.transform.position = transform.position + Vector3.up * hitParticleYOffset;
            _healParticles.Play(true);
        }

        private void CreateDamageText(int damage, bool isCritical)
        {
            var scale = isCritical ? criticalTextScale : 1f;
            var color = isCritical ? criticalTextColor : Color.red;
            var textMesh = SpawnFloatingText(damage.ToString(), color, scale);
            if (textMesh == null)
                return;

            // 크리티컬은 커졌다 줄어드는 팝 연출로 강조.
            if (isCritical)
            {
                var baseScale = textMesh.transform.localScale;
                textMesh.transform.localScale = baseScale * 1.5f;
                textMesh.transform.DOScale(baseScale, 0.16f).SetEase(Ease.OutBack).SetLink(textMesh.gameObject);
            }
        }

        /// <summary>회피 성공 시 MISS 텍스트를 띄운다(Combatant가 호출).</summary>
        public void ShowMissText()
        {
            SpawnFloatingText("MISS", missTextColor, 0.85f);
        }

        private TextMesh SpawnFloatingText(string text, Color color, float sizeScale)
        {
            var textMesh = damageTextPrefab != null
                ? Instantiate(damageTextPrefab, transform.position + Vector3.up * 0.85f, Quaternion.identity)
                : CreateRuntimeFloatingText(transform.position + Vector3.up * 0.85f);

            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.18f * sizeScale;
            textMesh.fontSize = 32;
            textMesh.color = color;

            var meshRenderer = textMesh.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
                meshRenderer.sortingOrder = textSortingOrder;

            var endPosition = textMesh.transform.position + Vector3.up * textFloatDistance;
            DOTween.Sequence()
                .Append(textMesh.transform.DOMove(endPosition, textDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(
                    () => textMesh.color.a,
                    alpha =>
                    {
                        var textColor = textMesh.color;
                        textColor.a = alpha;
                        textMesh.color = textColor;
                    },
                    0f,
                    textDuration))
                .OnComplete(() => Destroy(textMesh.gameObject));

            return textMesh;
        }

        private TextMesh CreateRuntimeFloatingText(Vector3 position)
        {
            var textObject = new GameObject("DamageText");
            textObject.transform.position = position;
            return textObject.AddComponent<TextMesh>();
        }

        private void EnsureDefaultHitParticles()
        {
            if (hitParticles != null)
                return;

            var particleObject = new GameObject("DamageHitParticles");
            particleObject.transform.SetParent(transform);
            particleObject.transform.localPosition = Vector3.up * hitParticleYOffset;
            particleObject.transform.localRotation = Quaternion.identity;
            particleObject.transform.localScale = Vector3.one;

            hitParticles = particleObject.AddComponent<ParticleSystem>();
            ConfigureHitParticles(hitParticles);
        }

        // 회복용 상승 스파클 — 피격 파티클과 달리 천천히 떠오르며 사라진다.
        private ParticleSystem CreateHealParticles()
        {
            var particleObject = new GameObject("HealParticles");
            particleObject.transform.SetParent(transform);
            particleObject.transform.localPosition = Vector3.up * hitParticleYOffset;
            particleObject.transform.localRotation = Quaternion.identity;
            particleObject.transform.localScale = Vector3.one;

            var particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = healParticleColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)healParticleBurstCount)
            });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.22f;
            shape.arc = 360f;

            var velocityOverLifetime = particles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.8f, 1f, 0.75f, 1f), 0f),
                    new GradientColorKey(healParticleColor, 0.5f),
                    new GradientColorKey(new Color(0.2f, 0.85f, 0.4f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.7f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = hitParticleSortingOrder;

            return particles;
        }

        private void ConfigureHitParticles(ParticleSystem particles)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.18f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = hitParticleColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)hitParticleBurstCount)
            });

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.06f;
            shape.arc = 360f;

            var velocityOverLifetime = particles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.45f, 1.25f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.86f, 0.26f, 1f), 0f),
                    new GradientColorKey(hitParticleColor, 0.45f),
                    new GradientColorKey(new Color(0.75f, 0.02f, 0.01f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.75f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = hitParticleSortingOrder;
        }
    }
}
