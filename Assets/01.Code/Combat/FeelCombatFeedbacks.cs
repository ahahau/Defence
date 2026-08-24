using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Combat
{
    public class FeelCombatFeedbacks : MonoBehaviour
    {
        [Header("Hit")]
        [SerializeField, Min(0f)] private float hitShakeDuration = 0.12f;
        [SerializeField, Min(0f)] private float hitShakeAmplitude = 0.05f;
        [SerializeField, Min(0f)] private float hitShakeFrequency = 25f;
        [SerializeField, Min(0f)] private float hitCooldown = 0.08f;
        [SerializeField, Min(0f), Tooltip("일반 타격이 적중할 때 화면이 멈추는 시간(초). 0이면 멈추지 않는다.")]
        private float hitStopDuration = 0.025f;

        [Header("Big Hit")]
        [Tooltip("Damage ratio of max health that should trigger a stronger feedback.")]
        [SerializeField, Range(0f, 1f)] private float bigHitDamageRatio = 0.25f;
        [SerializeField, Min(0f)] private float bigHitShakeDuration = 0.18f;
        [SerializeField, Min(0f)] private float bigHitShakeAmplitude = 0.13f;
        [SerializeField, Min(0f)] private float bigHitStopDuration = 0.04f;
        [SerializeField, Min(0f)] private float bigHitCooldown = 0.25f;

        [Header("Death")]
        [SerializeField, Min(0f)] private float deathShakeDuration = 0.28f;
        [SerializeField, Min(0f)] private float deathShakeAmplitude = 0.2f;
        [SerializeField, Min(0f)] private float deathHitStopDuration = 0.07f;

        [Header("Damage Pulse")]
        [SerializeField] private Transform feedbackTarget;
        [SerializeField] private SpriteRenderer[] spriteRenderers = new SpriteRenderer[0];
        [SerializeField] private Color hitFlashColor = new(1f, 0.2f, 0.08f, 1f);
        [SerializeField] private Color bigHitFlashColor = new(1f, 0.72f, 0.12f, 1f);
        [SerializeField] private Color deathFlashColor = new(1f, 0.05f, 0.02f, 1f);
        [SerializeField, Min(0f)] private float hitPulseScale = 0.08f;
        [SerializeField, Min(0f)] private float bigHitPulseScale = 0.18f;
        [SerializeField, Min(0f)] private float deathPulseScale = 0.28f;

        [SerializeField] private Health health;

        private MMF_Player _hitPlayer;
        private MMF_Player _bigHitPlayer;
        private MMF_Player _deathPlayer;

        private void Awake()
        {
            if (health == null)
            {
                var combatant = GetComponent<Combatant>();
                health = combatant != null && combatant.Health != null
                    ? combatant.Health
                    : GetComponentInChildren<Health>();
            }

            if (spriteRenderers == null || spriteRenderers.Length == 0)
                spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (feedbackTarget == null)
                feedbackTarget = spriteRenderers != null && spriteRenderers.Length > 0 && spriteRenderers[0] != null
                    ? spriteRenderers[0].transform
                    : transform;

            FeelCombatSceneSetup.EnsureCameraShaker();

            // 큰 타격과 사망은 드물게 일어나므로 쿨다운을 무시하고 항상 멈춘다.
            _hitPlayer = BuildPlayer("Feel Hit", hitShakeDuration, hitShakeAmplitude, hitCooldown, hitStopDuration, hitFlashColor, hitPulseScale, false);
            _bigHitPlayer = BuildPlayer("Feel Big Hit", bigHitShakeDuration, bigHitShakeAmplitude, bigHitCooldown, bigHitStopDuration, bigHitFlashColor, bigHitPulseScale, true);
            _deathPlayer = BuildPlayer("Feel Death", deathShakeDuration, deathShakeAmplitude, 0f, deathHitStopDuration, deathFlashColor, deathPulseScale, true);
        }

        private void OnEnable()
        {
            if (health != null)
                health.DamagedDetailed += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null)
                health.DamagedDetailed -= OnDamaged;
        }

        private void OnDamaged(int damage, bool isCritical)
        {
            if (damage <= 0)
                return;

            var maxHealth = health != null ? health.MaxHealth : 0;
            var damageRatio = maxHealth > 0 ? (float)damage / maxHealth : 0f;

            if (health != null && !health.IsAlive)
            {
                _deathPlayer?.PlayFeedbacks(transform.position);
                return;
            }

            if (isCritical || (bigHitDamageRatio > 0f && damageRatio >= bigHitDamageRatio))
            {
                var intensity = Mathf.Clamp(0.8f + damageRatio, 0.8f, 1.5f);
                _bigHitPlayer?.PlayFeedbacks(transform.position, intensity);
                return;
            }

            _hitPlayer?.PlayFeedbacks(transform.position, Mathf.Clamp(0.6f + damageRatio * 2f, 0.6f, 1.2f));
        }

        private MMF_Player BuildPlayer(string playerName, float shakeDuration, float shakeAmplitude, float cooldown, float hitStopDuration, Color flashColor, float pulseScale, bool hitStopIgnoresCooldown)
        {
            // MMF_Player는 [DisallowMultipleComponent]라 같은 GO에 여러 개 못 붙는다 → 전용 자식 GO에 하나씩.
            var host = new GameObject(playerName);
            host.transform.SetParent(transform, false);
            var player = host.AddComponent<MMF_Player>();
            player.InitializationMode = MMFeedbacks.InitializationModes.Script;
            player.AutoPlayOnStart = false;
            player.AutoPlayOnEnable = false;

            var pulse = player.AddFeedback(typeof(MMF_DamagePulse)) as MMF_DamagePulse;
            if (pulse != null)
            {
                pulse.Target = feedbackTarget;
                pulse.SpriteRenderers = spriteRenderers;
                pulse.FlashColor = flashColor;
                pulse.Duration = Mathf.Max(0.1f, shakeDuration);
                pulse.ShakeDistance = Mathf.Max(0.02f, shakeAmplitude * 0.85f);
                pulse.PunchScale = pulseScale;
                pulse.RotationAngle = Mathf.Lerp(3f, 10f, Mathf.Clamp01(pulseScale / 0.25f));
                pulse.Timing.CooldownDuration = cooldown;
                pulse.Timing.TimescaleMode = TimescaleModes.Unscaled;
            }
            else
            {
                Debug.LogWarning($"{nameof(FeelCombatFeedbacks)} could not create {nameof(MMF_DamagePulse)} on {name}. Sprite pulse will be skipped.", this);
            }

            var shake = player.AddFeedback(typeof(MMF_CameraShake)) as MMF_CameraShake;
            if (shake != null)
            {
                shake.CameraShakeProperties = new MMCameraShakeProperties(
                    shakeDuration, 0f, hitShakeFrequency, shakeAmplitude, shakeAmplitude, 0f);
                shake.Timing.CooldownDuration = cooldown;
                shake.Timing.TimescaleMode = TimescaleModes.Unscaled;
            }
            else
            {
                Debug.LogWarning($"{nameof(FeelCombatFeedbacks)} could not create {nameof(MMF_CameraShake)} on {name}. Camera shake will be skipped.", this);
            }

            if (hitStopDuration > 0f)
            {
                var hitStop = player.AddFeedback(typeof(MMF_HitStop)) as MMF_HitStop;
                if (hitStop != null)
                {
                    hitStop.Duration = hitStopDuration;
                    hitStop.IgnoreGlobalCooldown = hitStopIgnoresCooldown;
                    hitStop.Timing.CooldownDuration = cooldown;
                    hitStop.Timing.TimescaleMode = TimescaleModes.Unscaled;
                }
            }

            player.Initialization();
            return player;
        }
    }
}
