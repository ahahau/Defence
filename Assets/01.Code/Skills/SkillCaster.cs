using _01.Code.BT;
using _01.Code.Combat;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>에이전트의 스킬/궁극기를 보유하고 쿨다운·궁극기 사용 여부를 관리한다.
    /// BT의 Cast Skill 노드가 TryCast를 호출. 새 전투(전투필드 변경) 시 쿨다운/궁극기 초기화.</summary>
    [RequireComponent(typeof(BattleAgent))]
    public class SkillCaster : MonoBehaviour
    {
        [SerializeField] private BattleAgent agent;
        [SerializeField] private Combatant combatant;
        [SerializeField, Tooltip("전투 바 뷰(공속 바 아래 스킬 바 표시). 비우면 자식에서 자동 탐색.")]
        private CombatBarsView barsView;
        [SerializeField, Tooltip("쿨다운 기반 기본 스킬.")] private SkillDataSO skill;
        [SerializeField, Tooltip("전투당 1회 궁극기(있으면 우선 시전).")] private SkillDataSO ultimate;
        [Header("Cast Visual")]
        [SerializeField] private Color castFlashColor = new(0.55f, 0.85f, 1f, 1f);
        [SerializeField] private Color ultimateFlashColor = new(1f, 0.85f, 0.35f, 1f);
        [SerializeField, Min(0f)] private float castFlashDuration = 0.14f;
        [SerializeField, Min(0f)] private float castPunchScale = 0.14f;
        [SerializeField, Min(0f)] private float ultimateShakeDuration = 0.18f;
        [SerializeField, Min(0f)] private float ultimateShakeAmplitude = 0.1f;
        [SerializeField, Min(0f)] private float ultimateHitStopDuration = 0.035f;

        private float _cooldownTimer;
        private bool _ultimateUsed;
        private NodeBattlefield _lastBattlefield;
        private SpriteRenderer[] _spriteRenderers;
        private MMF_Player _skillCastPlayer;
        private MMF_Player _ultimateCastPlayer;

        public bool HasReadySkill =>
            _cooldownTimer <= 0f
            && ((ultimate != null && !_ultimateUsed) || skill != null);

        /// <summary>스킬 충전 비율(0=방금 사용, 1=사용 가능). 스킬 바 표시용.</summary>
        public float SkillChargeRatio
        {
            get
            {
                if (HasReadySkill) return 1f;
                if (skill != null && skill.Cooldown > 0f)
                    return Mathf.Clamp01(1f - _cooldownTimer / skill.Cooldown);
                return 0f;
            }
        }

        private void Awake()
        {
            if (agent == null) agent = GetComponent<BattleAgent>();
            if (combatant == null) combatant = GetComponent<Combatant>();
            if (barsView == null) barsView = GetComponentInChildren<CombatBarsView>();

            _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            BuildFeelPlayers();
        }

        private void OnDisable()
        {
            _skillCastPlayer?.StopFeedbacks();
            _ultimateCastPlayer?.StopFeedbacks();
        }

        private void Update()
        {
            // 새 전투(전투필드 변경) 진입 시 스킬 상태 초기화 — "전투당 1회 궁극기"가 다음 교전에 다시 차게.
            var battlefield = agent != null ? agent.Battlefield : null;
            if (battlefield != _lastBattlefield)
            {
                _lastBattlefield = battlefield;
                _cooldownTimer = 0f;
                _ultimateUsed = false;
            }

            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;

            // 공속 바 아래 스킬(쿨다운) 바 갱신.
            barsView?.SetSkillRatio(SkillChargeRatio);
        }

        /// <summary>준비된 스킬을 시전한다(궁극기 우선). 시전했으면 true.</summary>
        public bool TryCast()
        {
            if (agent == null || !agent.IsAlive || _cooldownTimer > 0f)
                return false;

            if (ultimate != null && !_ultimateUsed)
            {
                Execute(ultimate);
                _ultimateUsed = true;
                // 궁극기 직후 일반 스킬이 다음 자동 검사에서 연달아 발동하지 않게
                // 동일한 일반 스킬 주기를 공유한다.
                _cooldownTimer = skill != null ? skill.Cooldown : 0f;
                return true;
            }

            if (skill != null)
            {
                Execute(skill);
                _cooldownTimer = skill.Cooldown;
                return true;
            }

            return false;
        }

        private void Execute(SkillDataSO data)
        {
            PlayCastVisual(data != null && data.IsUltimate);
            var context = new SkillContext(agent, combatant, agent.CurrentTarget);
            data.Execute(context);
        }

        /// <summary>시전 순간 캐스터 강조 — 색 플래시 + 스케일 팝. 궁극기는 금색으로 더 크게.</summary>
        private void PlayCastVisual(bool isUltimate)
        {
            var player = isUltimate ? _ultimateCastPlayer : _skillCastPlayer;
            player?.PlayFeedbacks(transform.position);
        }

        private void BuildFeelPlayers()
        {
            if (_spriteRenderers == null || _spriteRenderers.Length == 0)
                return;

            Transform feedbackTarget = transform;
            for (var i = 0; i < _spriteRenderers.Length; i++)
            {
                var candidate = _spriteRenderers[i];
                if (candidate == null || candidate.sortingOrder >= 40)
                    continue;

                feedbackTarget = candidate.transform;
                break;
            }

            _skillCastPlayer = BuildFeelPlayer(
                "Feel Skill Cast", feedbackTarget, castFlashColor, castFlashDuration,
                castPunchScale, false);
            _ultimateCastPlayer = BuildFeelPlayer(
                "Feel Ultimate Cast", feedbackTarget, ultimateFlashColor, castFlashDuration * 1.6f,
                castPunchScale * 1.75f, true);
        }

        private MMF_Player BuildFeelPlayer(
            string playerName,
            Transform feedbackTarget,
            Color flashColor,
            float duration,
            float punchScale,
            bool isUltimate)
        {
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
                pulse.SpriteRenderers = _spriteRenderers;
                pulse.FlashColor = flashColor;
                pulse.Duration = Mathf.Max(0.08f, duration);
                pulse.ShakeDistance = isUltimate ? 0.055f : 0.018f;
                pulse.PunchScale = punchScale;
                pulse.RotationAngle = isUltimate ? 7f : 2.5f;
                pulse.Timing.CooldownDuration = 0.05f;
                pulse.Timing.TimescaleMode = TimescaleModes.Unscaled;
            }

            if (isUltimate)
            {
                FeelCombatSceneSetup.EnsureCameraShaker();

                var shake = player.AddFeedback(typeof(MMF_CameraShake)) as MMF_CameraShake;
                if (shake != null)
                {
                    shake.CameraShakeProperties = new MMCameraShakeProperties(
                        ultimateShakeDuration, 0f, 24f,
                        ultimateShakeAmplitude, ultimateShakeAmplitude, 0f);
                    shake.Timing.TimescaleMode = TimescaleModes.Unscaled;
                }

                if (ultimateHitStopDuration > 0f)
                {
                    var hitStop = player.AddFeedback(typeof(MMF_HitStop)) as MMF_HitStop;
                    if (hitStop != null)
                    {
                        hitStop.Duration = ultimateHitStopDuration;
                        hitStop.IgnoreGlobalCooldown = true;
                        hitStop.Timing.TimescaleMode = TimescaleModes.Unscaled;
                    }
                }
            }

            player.Initialization();
            return player;
        }
    }
}
