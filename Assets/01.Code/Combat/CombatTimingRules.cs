using UnityEngine;

namespace Code.Combat
{
    /// <summary>
    /// 평타 한 번의 박자: 게이지 → 예비 동작(휘두르기 시작) → 타격. 상태가 없어 테스트에서 바로 부른다.
    /// 예전에는 피해가 들어간 뒤에야 공격 동작이 시작돼, 맞고 나서 휘두르는 것처럼 보였다.
    /// </summary>
    public static class CombatTimingRules
    {
        /// <summary>맞으면 이 시간(초) 동안 움찔해 움직이지도, 새로 휘두르기 시작하지도 못한다. 이미 휘두르는 중이면 그대로 친다.</summary>
        public const float HitStaggerSeconds = 0.2f;

        /// <summary>바짝 붙었을 때도 남기는 옆자리 비율. 0이면 한 대상에 붙은 근접이 같은 점으로 겹친다.</summary>
        public const float CloseFlankShare = 0.4f;

        /// <summary>휘두르기 시작할 수 있는 거리(사거리 배율). 밀려난 직후 조금 벗어난 것까지는 봐준다.</summary>
        public const float SwingStartRangeScale = 1.15f;

        /// <summary>휘두른 끝에 대상이 이보다 멀어졌으면 헛친다(사거리 배율).</summary>
        public const float StrikeReachScale = 1.3f;

        /// <summary>지금 휘두르기 시작해야 하는지. 게이지가 (간격 − 예비 동작)에 닿았고, 사거리 안이며, 움찔하는 중이 아니다.</summary>
        public static bool ShouldStartSwing(float attackTimer, float attackInterval, float windupSeconds, bool inRange, bool staggered) =>
            inRange && !staggered && attackTimer >= Mathf.Max(0f, attackInterval - windupSeconds);

        /// <summary>
        /// 휘두르지 못하는 동안(사거리 밖·움찔) 게이지는 예비 동작 직전에서 멈춰 기다린다.
        /// 계속 차오르게 두면 다시 붙는 순간 예비 동작 없이 바로 맞았다.
        /// </summary>
        public static float HoldTimer(float attackTimer, float attackInterval, float windupSeconds) =>
            Mathf.Min(attackTimer, Mathf.Max(0f, attackInterval - windupSeconds));

        public static bool InSwingRange(float distance, float strikeRange) => distance <= strikeRange * SwingStartRangeScale;

        public static bool InStrikeReach(float distance, float strikeRange) => distance <= strikeRange * StrikeReachScale;
    }
}
