using _01.Code.Entities;
using UnityEngine;

namespace _01.Code.Combat
{
    /// <summary>
    /// 때리는 순간 공격 포즈로 바꿨다가 되돌리고, 쓰러지면 사망 포즈로 굳힌다.
    ///
    /// 포즈 그림 자체는 <see cref="EntityRender"/>가 들고 있다 — 침입자 쪽은 이미 그 방식으로 돌고 있어서
    /// 같은 부품을 쓴다. 여기서 하는 일은 "언제 바꿀지"뿐이다.
    ///
    /// 침입자는 <c>Enemy</c>가 자기 상태 기계 안에서 직접 전환하는데, 부하에는 그런 상태 기계가 없고
    /// <c>MainUnit</c>은 Idle과 Defeated만 오갈 뿐 공격 포즈를 한 번도 쓰지 않았다.
    /// 그래서 부하는 때려도 서 있는 그림 그대로였다.
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

        /// <summary>공격이 적중한 순간 <see cref="Combatant"/>가 불러준다.</summary>
        public void PlayAttackPose()
        {
            if (isDead || entityRender == null)
                return;

            restoreIdleAt = Time.time + attackPoseDuration;
            entityRender.SetUnitSprite(EntityState.Attack);
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
