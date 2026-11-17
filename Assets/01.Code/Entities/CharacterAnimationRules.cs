using UnityEngine;

namespace Code.Entities
{
    /// <summary>프레임 애니메이션의 시간 → 프레임 계산. 상태가 없어 테스트에서 바로 부른다.</summary>
    public static class CharacterAnimationRules
    {
        /// <summary>이 속도보다 느리게 움직이면 걷는 것으로 보지 않는다(단위/초). 밀림·흔들림에 다리가 떨지 않게 한다.</summary>
        public const float WalkSpeedThreshold = 0.05f;

        /// <summary>멈춘 뒤에도 이 시간(초) 동안 걷기를 유지한다. 프레임마다 멈춤·이동이 번갈아 깜빡이지 않게 한다.</summary>
        public const float WalkHoldSeconds = 0.15f;

        /// <summary>
        /// 경과 시간에 맞는 프레임 번호. 반복 동작은 처음으로 돌아가고,
        /// 한 번만 트는 동작(공격·쓰러짐)은 마지막 프레임에 머문다.
        /// </summary>
        public static int GetFrameIndex(int frameCount, float elapsedSeconds, float framesPerSecond, bool loop)
        {
            if (frameCount <= 1 || framesPerSecond <= 0f)
                return 0;

            var frame = Mathf.FloorToInt(Mathf.Max(0f, elapsedSeconds) * framesPerSecond);
            return loop ? frame % frameCount : Mathf.Min(frame, frameCount - 1);
        }

        /// <summary>한 번만 트는 동작이 끝났는지. 프레임이 없으면 이미 끝난 것으로 본다.</summary>
        public static bool IsFinished(int frameCount, float elapsedSeconds, float framesPerSecond)
        {
            if (frameCount <= 0 || framesPerSecond <= 0f)
                return true;

            return elapsedSeconds >= frameCount / framesPerSecond;
        }

        public static bool IsLooping(CharacterClip clip) => clip == CharacterClip.Idle || clip == CharacterClip.Walk;

        public static bool IsAttack(CharacterClip clip) => clip == CharacterClip.Attack || clip == CharacterClip.AttackAlt;

        /// <summary>끝까지 튼 뒤 스스로 대기로 돌아가는 동작(공격 둘·움찔).</summary>
        public static bool ReturnsToIdle(CharacterClip clip) => IsAttack(clip) || clip == CharacterClip.Hurt;

        /// <summary>
        /// 맞았을 때 움찔 동작을 틀어도 되는지. 휘두르는 중에는 끊지 않는다 — 맞아도 휘두르기는 이어져
        /// 피해가 나가는데(<c>Combatant</c>) 그림만 움찔하면 무기 없이 때리는 것처럼 보인다. 쓰러진 뒤에도 틀지 않는다.
        /// </summary>
        public static bool AllowsHurt(CharacterClip current) => !IsAttack(current) && current != CharacterClip.Defeated;

        /// <summary>다음 공격 동작. 둘째 동작이 있으면 번갈아 쓴다.</summary>
        public static CharacterClip NextAttack(bool hasAlt, bool useAlt) =>
            hasAlt && useAlt ? CharacterClip.AttackAlt : CharacterClip.Attack;

        /// <summary>무기가 닿는 지점의 기본값(공격 동작 길이 비율). 6장짜리 휘두르기면 넷째 장 무렵이다.</summary>
        public const float DefaultImpactRatio = 0.55f;

        /// <summary>공격 동작을 시작하고 무기가 닿기까지의 시간(초). 닿는 프레임의 시작 시각이다.</summary>
        public static float ImpactSeconds(int frameCount, float framesPerSecond, float impactRatio)
        {
            if (frameCount <= 0 || framesPerSecond <= 0f)
                return 0f;

            var frame = Mathf.Clamp(Mathf.FloorToInt(frameCount * Mathf.Clamp01(impactRatio)), 0, frameCount - 1);
            return frame / framesPerSecond;
        }

        /// <summary>
        /// 공격 예비 동작 시간. 동작이 닿기까지 걸리는 시간이지만 공격 간격의 절반을 넘지 않는다 —
        /// 넘으면 게이지가 차기 한참 전부터 휘두르기 시작해 다음 공격과 겹친다.
        /// </summary>
        public static float WindupSeconds(float impactSeconds, float attackInterval) =>
            Mathf.Clamp(impactSeconds, 0f, Mathf.Max(0f, attackInterval) * 0.5f);
    }
}
