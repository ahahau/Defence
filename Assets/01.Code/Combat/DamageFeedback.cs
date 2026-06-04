using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

namespace _01.Code.Combat
{
    [RequireComponent(typeof(Health))]
    public class DamageFeedback : MonoBehaviour
    {
        [SerializeField] private Color bloomColor = new(1f, 0.12f, 0.08f, 1f);
        [SerializeField] private float bloomDuration = 0.12f;
        [SerializeField, Min(0f)] private float impactDuration = 0.18f;
        [SerializeField, Min(0f)] private float impactShakeDistance = 0.08f;
        [SerializeField, Min(0f)] private float impactScaleAmount = 0.12f;
        [SerializeField, Min(0f)] private float impactRotationAngle = 5f;
        [SerializeField] private float textFloatDistance = 0.45f;
        [SerializeField] private float textDuration = 0.55f;
        [SerializeField] private int textSortingOrder = 60;
        [SerializeField] private TextMesh damageTextPrefab;
        [SerializeField] private ParticleSystem hitParticles;
        [SerializeField] private Health health;
        [SerializeField] private SpriteRenderer[] spriteRenderers = new SpriteRenderer[0];
        [SerializeField] private Color hitParticleColor = new(1f, 0.18f, 0.06f, 1f);
        [SerializeField, Min(1)] private int hitParticleBurstCount = 18;
        [SerializeField, Min(0f)] private float hitParticleYOffset = 0.35f;
        [SerializeField] private int hitParticleSortingOrder = 80;

        private Color[] _originalColors;
        private Sequence _bloomSequence;
        private Sequence _impactSequence;
        private void Awake()
        {
            _originalColors = new Color[spriteRenderers.Length];

            for (var i = 0; i < spriteRenderers.Length; i++)
                _originalColors[i] = spriteRenderers[i].color;
        }

        private void OnEnable()
        {
            if (health != null)
                health.Damaged += Play;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Damaged -= Play;

            _bloomSequence?.Kill();
            _impactSequence?.Complete();
            _impactSequence?.Kill();
        }

        private void Play(int damage)
        {
            if (damage <= 0)
                return;

            PlayBloom();
            PlayImpactMotion();
            PlayHitParticles();
            CreateDamageText(damage);
        }

        private void PlayBloom()
        {
            _bloomSequence?.Kill();
            _bloomSequence = DOTween.Sequence();

            for (var i = 0; i < spriteRenderers.Length; i++)
            {
                var spriteRenderer = spriteRenderers[i];
                if (spriteRenderer == null || spriteRenderer.sortingOrder >= 40)
                    continue;

                var originalColor = _originalColors[i];
                _bloomSequence.Join(spriteRenderer.DOColor(bloomColor, bloomDuration));
                _bloomSequence.Insert(bloomDuration, spriteRenderer.DOColor(originalColor, bloomDuration));
            }
        }

        private void PlayImpactMotion()
        {
            if (impactDuration <= 0f)
                return;

            _impactSequence?.Complete();
            _impactSequence?.Kill();
            _impactSequence = DOTween.Sequence();

            var animatedTargets = new HashSet<Transform>();
            foreach (var spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer == null || spriteRenderer.sortingOrder >= 40)
                    continue;

                var target = spriteRenderer.transform;
                if (!animatedTargets.Add(target))
                    continue;

                var baseScale = target.localScale;
                var basePosition = target.localPosition;
                var baseRotation = target.localEulerAngles;
                var direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;

                var rendererSequence = DOTween.Sequence();
                if (impactShakeDistance > 0f)
                    rendererSequence.Join(target.DOShakePosition(
                        impactDuration,
                        impactShakeDistance,
                        12,
                        70f,
                        false,
                        true));

                if (impactScaleAmount > 0f)
                    rendererSequence.Join(target.DOPunchScale(Vector3.one * impactScaleAmount, impactDuration, 1, 0.4f));

                if (impactRotationAngle > 0f)
                    rendererSequence.Join(target.DOPunchRotation(
                        new Vector3(0f, 0f, impactRotationAngle * direction),
                        impactDuration,
                        1,
                        0.35f));

                rendererSequence.OnComplete(() =>
                {
                    target.localPosition = basePosition;
                    target.localScale = baseScale;
                    target.localEulerAngles = baseRotation;
                });
                _impactSequence.Join(rendererSequence);
            }
        }

        private void PlayHitParticles()
        {
            if (hitParticles == null)
                return;

            hitParticles.transform.position = transform.position + Vector3.up * hitParticleYOffset;
            hitParticles.Play(true);
        }

        private void CreateDamageText(int damage)
        {
            var textMesh = Instantiate(damageTextPrefab, transform.position + Vector3.up * 0.85f, Quaternion.identity);
            textMesh.text = damage.ToString();
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.18f;
            textMesh.fontSize = 32;
            textMesh.color = Color.red;

            var meshRenderer = textMesh.GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = textSortingOrder;

            var endPosition = textMesh.transform.position + Vector3.up * textFloatDistance;
            DOTween.Sequence()
                .Append(textMesh.transform.DOMove(endPosition, textDuration).SetEase(Ease.OutQuad))
                .Join(DOTween.To(
                    () => textMesh.color.a,
                    alpha =>
                    {
                        var color = textMesh.color;
                        color.a = alpha;
                        textMesh.color = color;
                    },
                    0f,
                    textDuration))
                .OnComplete(() => Destroy(textMesh.gameObject));
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
