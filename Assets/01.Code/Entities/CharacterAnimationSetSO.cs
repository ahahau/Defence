using System;
using UnityEngine;

namespace Code.Entities
{
    /// <summary>
    /// 캐릭터 하나의 프레임 애니메이션 묶음. <see cref="EntityRender"/>가 재생한다.
    /// 모험가는 <c>EnemyDataSO.AnimationSet</c>, 부하는 프리팹의 EntityRender에 연결한다.
    /// 프레임 그림의 기준점(발밑)과 크기는 텍스처 임포트 설정이 맞춘다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterAnimationSet", menuName = "Defence/Character Animation Set")]
    public class CharacterAnimationSetSO : ScriptableObject
    {
        [SerializeField] private Sprite[] idleFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] walkFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] attackFrames = Array.Empty<Sprite>();
        [SerializeField] private Sprite[] defeatedFrames = Array.Empty<Sprite>();

        [SerializeField, Min(1f)] private float idleFramesPerSecond = 8f;
        [SerializeField, Min(1f)] private float walkFramesPerSecond = 10f;
        [SerializeField, Min(1f), Tooltip("공격 동작은 끝까지 한 번 튼다. 공격 간격보다 짧게 끝나야 다음 공격과 겹치지 않는다.")]
        private float attackFramesPerSecond = 12f;
        [SerializeField, Min(1f)] private float defeatedFramesPerSecond = 10f;

        /// <summary>대기 첫 프레임. 고용 목록·보드처럼 정지 그림이 필요한 곳에 쓴다.</summary>
        public Sprite FirstIdleFrame => idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : null;

        public Sprite[] GetFrames(CharacterClip clip)
        {
            var frames = clip switch
            {
                CharacterClip.Walk => walkFrames,
                CharacterClip.Attack => attackFrames,
                CharacterClip.Defeated => defeatedFrames,
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
            CharacterClip.Attack => attackFramesPerSecond,
            CharacterClip.Defeated => defeatedFramesPerSecond,
            _ => idleFramesPerSecond
        };
    }
}
