using Code.Entities;
using GameLib.Entity;
using UnityEngine;

namespace Code.Run
{
    /// <summary>
    /// 던전 FTL 전투 인물의 화면 쪽 몸. 값은 `RunBattle`의 `RunCombatant`가 진짜이고,
    /// 여기서는 체력 모듈을 그 값에 맞춰 피격·사망 연출(Hit/Died)을 그대로 쓴다. 행동 트리는 없다.
    /// </summary>
    public class RunActor : Entity
    {
        [SerializeField] private EntityRender render;

        private HealthModule _health;

        public RunCombatant Combatant { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _health = GetModule<HealthModule>();
        }

        /// <summary>전투 인물에 묶고 그림·최대 체력을 맞춘다.</summary>
        public void Bind(RunCombatant combatant)
        {
            Combatant = combatant;
            if (render != null)
            {
                render.ConfigureAnimation(combatant.Definition.ResolveAnimationSet());
                render.SetUnitSprite(EntityState.Idle);
                AlignFeet();
            }
            // 묶을 때는 체력을 피해·회복으로 넣지 않고 바로 맞춘다. 피해로 넣으면 이미 쓰러진 부하의
            // 사망 연출·소리가 다시 나고, 멈춘 준비 화면에 그대로 남았다(2026-11-12 한 판 완주 확인).
            if (_health != null)
            {
                _health.SetMaxHealth(combatant.MaxHealth, true);
                _health.SetCurrentRatio(combatant.Health / (float)Mathf.Max(1, combatant.MaxHealth));
            }
            IsDead = combatant.Health <= 0;
        }

        /// <summary>
        /// 캐릭터 그림은 기준점이 가운데라 바닥선에 세우면 몸이 바닥 아래로 내려간다.
        /// 그림 아래 끝이 이 오브젝트 위치(발밑)에 오도록 그림 자식을 올린다.
        /// </summary>
        private void AlignFeet()
        {
            if (!TryGetOpaqueSpan(out float bottom, out _)) return;
            float scale = Mathf.Max(0.0001f, transform.lossyScale.y);
            render.transform.localPosition += new Vector3(0f, (transform.position.y - bottom) / scale, 0f);
        }

        /// <summary>
        /// 발밑에서 머리까지의 높이(이 오브젝트 로컬 단위). 그림 사각형은 위아래 여백이 커서
        /// 실제로 칠해진 모양(타이트 메시 꼭짓점)으로 잰다. 잴 수 없으면 fallback.
        /// </summary>
        public float HeadHeight(float fallback)
        {
            if (!TryGetOpaqueSpan(out _, out float top)) return fallback;
            return (top - transform.position.y) / Mathf.Max(0.0001f, transform.lossyScale.y);
        }

        private bool TryGetOpaqueSpan(out float bottom, out float top)
        {
            bottom = top = 0f;
            SpriteRenderer renderer = render != null ? render.SpriteRenderer : null;
            if (renderer == null || renderer.sprite == null) return false;
            Vector2[] vertices = renderer.sprite.vertices;
            if (vertices == null || vertices.Length == 0) return false;
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector2 vertex in vertices)
            {
                min = Mathf.Min(min, vertex.y);
                max = Mathf.Max(max, vertex.y);
            }
            bottom = renderer.transform.TransformPoint(new Vector3(0f, min, 0f)).y;
            top = renderer.transform.TransformPoint(new Vector3(0f, max, 0f)).y;
            return true;
        }

        /// <summary>공격 동작을 한 번 튼다(규칙 쪽 공격 횟수가 늘었을 때).</summary>
        public void PlayAttack()
        {
            if (render != null && !IsDead)
                render.SetUnitSprite(EntityState.Attack);
        }

        public void FaceTowards(float directionX)
        {
            if (render != null)
                render.FaceTowards(directionX);
        }

        /// <summary>
        /// 규칙 쪽 체력(재생 중이면 그 시점 값)을 체력 모듈에 옮긴다. 줄면 피해, 늘면 회복으로 넣어 연출이 따라온다.
        /// </summary>
        public void ShowHealth(int health)
        {
            if (_health == null) return;
            int difference = health - _health.CurrentHealth;
            if (difference < 0)
                _health.TakeDamage(-difference);
            else if (difference > 0)
                _health.Heal(difference);
            IsDead = health <= 0;
        }
    }
}
