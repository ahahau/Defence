using Code.Entities;
using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 때리는 순간 공격 포즈로 바꿨다가 되돌리고, 쓰러지면 사망 포즈로 굳힌다.
    ///
    /// 포즈 그림 자체는 <see cref="EntityRender"/>가 들고 있다 — 침입자 쪽은 이미 그 방식으로 돌고 있어서
    /// 같은 부품을 쓴다. 여기서 하는 일은 "언제 바꿀지"뿐이다.
    ///
    /// 부하에는 상태 기계가 없고 <c>MainUnit</c>은 Idle과 Defeated만 오갈 뿐 공격 포즈를 쓰지 않았다.
    /// 침입자는 <c>Enemy</c>가 상태 기계로 전환하지만 EnterState(Attack)를 부르는 곳이 없어
    /// 결국 Idle과 Defeated만 오갔다. 셋 다 때려도 서 있는 그림 그대로였다.
    ///
    /// 그래서 <see cref="Combatant"/>가 없으면 직접 붙인다 — 프리팹에 일일이 얹지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPoseView : MonoBehaviour
    {
        [SerializeField] private EntityRender entityRender;

        [SerializeField, Min(0.02f), Tooltip("공격 포즈를 유지하는 시간(초). 공격 간격보다 짧아야 제자리로 돌아온다.")]
        private float attackPoseDuration = 0.18f;

        private Health health;
        private float restoreIdleAt;
        private bool isDead;

        private void Awake()
        {
            if (entityRender == null)
                entityRender = GetComponentInChildren<EntityRender>(true);

            var combatant = GetComponent<Combatant>();
            health = combatant != null && combatant.Health != null
                ? combatant.Health
                : GetComponentInChildren<Health>(true);
        }

        private void OnEnable()
        {
            isDead = false;
            restoreIdleAt = 0f;

            if (health != null)
                health.DamagedDetailed += HandleDamaged;
        }

        private void OnDisable()
        {
            if (health != null)
                health.DamagedDetailed -= HandleDamaged;
        }

        /// <summary>공격 동작을 시작해 무기가 닿기까지의 시간(초). 프레임 애니메이션이 없으면 0 — 그때는 맞는 순간 포즈만 바꾼다.</summary>
        public float AttackImpactSeconds => entityRender != null ? entityRender.AttackImpactSeconds : 0f;

        /// <summary>
        /// 공격 동작을 시작한다. 애니메이션이 있으면 휘두르기 시작할 때(<see cref="Combatant"/>의 예비 동작), 없으면 맞는 순간 불린다.
        /// 대상 쪽(x 부호)으로 돌아선다.
        /// </summary>
        public void PlayAttackPose(float facingX = 0f)
        {
            if (isDead || entityRender == null)
                return;

            entityRender.FaceTowards(facingX);
            restoreIdleAt = Time.time + attackPoseDuration;
            entityRender.SetUnitSprite(EntityState.Attack);
        }

        /// <summary>
        /// 살아났다. 쓰러짐 잠금을 풀고 대기로 돌린다. 잠금은 오브젝트가 다시 켜질 때만 풀렸는데
        /// 부하는 쓰러져도 꺼지지 않아, 부활한 뒤에도 쓰러진 그림에 굳고 공격 동작이 막혔다.
        /// </summary>
        public void ResetAfterRevive()
        {
            isDead = false;
            restoreIdleAt = 0f;
            entityRender?.SetUnitSprite(EntityState.Idle);
        }

        private void Update()
        {
            if (isDead || restoreIdleAt <= 0f || Time.time < restoreIdleAt)
                return;

            restoreIdleAt = 0f;
            entityRender.SetUnitSprite(EntityState.Idle);
        }

        private void HandleDamaged(int damage, bool isCritical)
        {
            // 쓰러진 뒤에는 공격 포즈 복구가 사망 포즈를 덮지 않도록 잠근다.
            if (isDead || health == null || health.IsAlive || entityRender == null)
                return;

            isDead = true;
            restoreIdleAt = 0f;
            entityRender.SetUnitSprite(EntityState.Defeated);
        }
    }
}
