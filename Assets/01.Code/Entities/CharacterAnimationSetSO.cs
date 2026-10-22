using System;
using UnityEngine;

namespace Code.Entities
{
    /// <summary>
    /// 캐릭터 하나의 프레임 애니메이션 묶음. <see cref="EntityRender"/>가 재생한다.
    /// 모험가는 <c>EnemyDataSO.AnimationSet</c>, 부하는 프리팹의 EntityRender에 연결한다.
    /// 프레임 그림의 기준점(발밑)과 크기는 텍스처 임포트 설정이 맞춘다.
    /// 둘째 공격·맞는 동작·발사체 그림은 팩에 있는 캐릭터만 채운다(생성기 <c>CombatPolishBuilder</c>).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterAnimationSet", menuName = "Defence/Character Animation Set")]
    public class CharacterAnimationSetSO : ScriptableObject
    {
        [SerializeField] private Sprite[] idleFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] walkFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attackFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] defeatedFrames = Array.Empty<Sprite>();
        [SerializeField, Tooltip("둘째 공격 동작. 있으면 평타마다 첫째와 번갈아 튼다. 비우면 첫째만 튼다.")]
        private Sprite[] attackAltFrames = Array.Empty<Sprite>();
        [SerializeField, Tooltip("맞고 움찔하는 동작. 비우면 맞아도 그림을 바꾸지 않는다.")]
        private Sprite[] hurtFrames = Array.Empty<Sprite>();

        [SerializeField, Min(1f)] private float idleFramesPerSecond = 8f;
        [SerializeField, Min(1f)] private float walkFramesPerSecond = 10f;
        [SerializeField, Min(1f), Tooltip("공격 동작은 끝까지 한 번 튼다. 공격 간격보다 짧게 끝나야 다음 공격과 겹치지 않는다.")]
        private float attackFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float defeatedFramesPerSecond = 10f;
        [SerializeField, Min(1f), Tooltip("움찔(0.2초) 안에 끝나도록 빠르게 튼다.")]
        private float hurtFramesPerSecond = 15f;
        [SerializeField, Range(0f, 1f), Tooltip("공격 동작 중 무기가 닿는 지점(동작 길이 비율). 피해·소리·돌진이 이때 나간다.")]
        private float attackImpactRatio = CharacterAnimationRules.DefaultImpactRatio;

        [Header("Projectile")]
        [SerializeField, Tooltip("원거리 평타가 날리는 그림(화살·도끼·마법 구슬). 여러 장이면 날아가는 동안 돈다. 비우면 사각형.")]
        private Sprite[] projectileFrames = Array.Empty<Sprite>();
        [SerializeField, Tooltip("발사체가 닿은 자리에서 한 번 트는 그림(폭발 등). 비우면 없음.")]
        private Sprite[] projectileImpactFrames = Array.Empty<Sprite>();
        [SerializeField, Min(1f)] private float projectileFramesPerSecond = 12f;

        /// <summary>대기 첫 프레임. 고용 목록·보드처럼 정지 그림이 필요한 곳에 쓴다.</summary>
        public Sprite FirstIdleFrame => idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : null;

        public bool HasAttackAlt => attackAltFrames != null && attackAltFrames.Length > 0;
        public bool HasHurt => hurtFrames != null && hurtFrames.Length > 0;

        public Sprite[] ProjectileFrames => projectileFrames ?? Array.Empty<Sprite>();
        public Sprite[] ProjectileImpactFrames => projectileImpactFrames ?? Array.Empty<Sprite>();
        public float ProjectileFramesPerSecond => projectileFramesPerSecond;

        /// <summary>첫째 공격 동작을 시작하고 무기가 닿기까지의 시간(초).</summary>
        public float AttackImpactSeconds => GetAttackImpactSeconds(CharacterClip.Attack);

        /// <summary>그 공격 동작(<see cref="CharacterClip.Attack"/>·<see cref="CharacterClip.AttackAlt"/>)의 타격 시점(초).</summary>
        public float GetAttackImpactSeconds(CharacterClip attackClip)
        {
            var frames = GetFrames(attackClip);
            return CharacterAnimationRules.ImpactSeconds(frames.Length, attackFramesPerSecond, attackImpactRatio);
        }

        public Sprite[] GetFrames(CharacterClip clip)
        {
            var frames = clip switch
            {
                CharacterClip.Walk => walkFrames,
                CharacterClip.Attack => attackFrames,
                CharacterClip.AttackAlt => HasAttackAlt ? attackAltFrames : attackFrames,
                CharacterClip.Defeated => defeatedFrames,
                CharacterClip.Hurt => hurtFrames,
                _ => idleFrames
            };

            // 걷기 그림이 없는 캐릭터(유령 등)는 대기 동작으로 움직인다.
            if ((frames == null || frames.Length == 0) && clip == CharacterClip.Walk)
                return idleFrames ?? Array.Empty<Sprite>();

            return frames ?? Array.Empty<Sprite>();
        }

        public float GetFramesPerSecond(CharacterClip clip) => clip switch
        {
            CharacterClip.Walk => walkFramesPerSecond,
            CharacterClip.Attack or CharacterClip.AttackAlt => attackFramesPerSecond,
            CharacterClip.Defeated => defeatedFramesPerSecond,
            CharacterClip.Hurt => hurtFramesPerSecond,
            _ => idleFramesPerSecond
        };
    }
}
