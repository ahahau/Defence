using _01.Code.BT;
using _01.Code.Combat;
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

        private float _cooldownTimer;
        private bool _ultimateUsed;
        private NodeBattlefield _lastBattlefield;

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
            var context = new SkillContext(agent, combatant, agent.CurrentTarget);
            data.Execute(context);
        }
    }
}
