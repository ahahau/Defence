using _01.Code.Audio;
using _01.Code.BT;
using _01.Code.Combat;
using DG.Tweening;
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

        private float _cooldownTimer;
        private bool _ultimateUsed;
        private NodeBattlefield _lastBattlefield;
        private SpriteRenderer[] _spriteRenderers;
        private Color[] _originalColors;
        private Sequence _castFlashSequence;

        public bool HasReadySkill =>
            (ultimate != null && !_ultimateUsed) || (skill != null && _cooldownTimer <= 0f);

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
            _originalColors = new Color[_spriteRenderers.Length];
            for (var i = 0; i < _spriteRenderers.Length; i++)
                _originalColors[i] = _spriteRenderers[i] != null ? _spriteRenderers[i].color : Color.white;
        }

        private void OnDisable()
        {
            _castFlashSequence?.Kill();
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
            if (agent == null || !agent.IsAlive)
                return false;

            if (ultimate != null && !_ultimateUsed)
            {
                Execute(ultimate);
                _ultimateUsed = true;
                return true;
            }

            if (skill != null && _cooldownTimer <= 0f)
            {
                Execute(skill);
                _cooldownTimer = skill.Cooldown;
                return true;
            }

            return false;
        }

        private void Execute(SkillDataSO data)
        {
            GameSfxPlayer.Play(GameSfxCue.SkillCast);
            PlayCastVisual(data != null && data.IsUltimate);
            var context = new SkillContext(agent, combatant, agent.CurrentTarget);
            data.Execute(context);
        }

        /// <summary>시전 순간 캐스터 강조 — 색 플래시 + 스케일 팝. 궁극기는 금색으로 더 크게.</summary>
        private void PlayCastVisual(bool isUltimate)
        {
            if (castFlashDuration <= 0f || _spriteRenderers == null)
                return;

            var flashColor = isUltimate ? ultimateFlashColor : castFlashColor;
            var punch = castPunchScale * (isUltimate ? 1.6f : 1f);

            _castFlashSequence?.Kill();
            _castFlashSequence = DOTween.Sequence().SetLink(gameObject);

            var punchPlayed = false;
            for (var i = 0; i < _spriteRenderers.Length; i++)
            {
                var spriteRenderer = _spriteRenderers[i];
                // 전투 바 등 오버레이(sortingOrder 40+)는 제외 — DamageFeedback과 같은 규약.
                if (spriteRenderer == null || spriteRenderer.sortingOrder >= 40)
                    continue;

                var originalColor = _originalColors[i];
                _castFlashSequence.Join(spriteRenderer.DOColor(flashColor, castFlashDuration));
                _castFlashSequence.Insert(castFlashDuration, spriteRenderer.DOColor(originalColor, castFlashDuration));

                if (punch > 0f && !punchPlayed)
                {
                    punchPlayed = true;
                    var target = spriteRenderer.transform;
                    target.DOComplete();
                    _castFlashSequence.Join(target.DOPunchScale(Vector3.one * punch, castFlashDuration * 2f, 1, 0.5f));
                }
            }
        }
    }
}
