using Code.Enemies;

namespace Code.Manager
{
    /// <summary>
    /// 엘리트·보스가 던전에 있는 동안 플레이어 일시정지를 막는 규칙.
    ///
    /// 멈춰 놓고 배치를 다시 짜는 것이 일반 파티 상대로는 운영이지만, 강적 앞에서도 허용하면
    /// 고비가 고비가 아니게 된다. 그래서 강적이 들어온 순간 시계를 다시 돌리고, 있는 동안은 잠근다.
    /// </summary>
    public static class PauseLockRules
    {
        /// <summary>이 강함 단계(4=위험, 보라) 이상이면 엘리트로 본다.</summary>
        public const int EliteStrengthTier = 4;

        /// <summary>낮에서 밤으로 넘어갈 때 멈추는 실제 시간(초).</summary>
        public const float DayNightTransitionPauseSeconds = 1f;

        public const string BossLockReason = "보스가 던전에 있어 멈출 수 없습니다";
        public const string EliteLockReason = "엘리트가 던전에 있어 멈출 수 없습니다";

        /// <summary>일시정지를 잠그는 강적인가. 쓰러진 적은 잠그지 않는다.</summary>
        public static bool LocksPause(bool isAlive, bool isBoss, int strengthTier) =>
            isAlive && (isBoss || strengthTier >= EliteStrengthTier);

        public static bool LocksPause(Enemy enemy) =>
            enemy != null && LocksPause(enemy.IsAlive, enemy.IsBoss, enemy.StrengthTier);
    }
}
