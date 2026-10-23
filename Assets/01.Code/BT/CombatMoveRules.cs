namespace Code.BT
{
    /// <summary>
    /// 전투 중 이동과 대상 교체 규칙. 상태가 없어 테스트에서 바로 부른다.
    ///
    /// 근접이 휘두를 때마다 최대 속도로 물러나던 동작을 짧고 느린 회복 걸음으로 제한한다.
    /// </summary>
    public static class CombatMoveRules
    {
        /// <summary>근접이 휘두른 뒤 물러나는 시간(초). 예전에는 회복 구간(0.3초) 내내 최대 속도로 물러났다.</summary>
        public const float MeleeRecoveryStepSeconds = 0.15f;

        /// <summary>근접 회복 걸음의 속도 배율. 반 걸음 정도만 떨어진다.</summary>
        public const float MeleeRecoveryStepSpeedShare = 0.35f;

        /// <summary>이 시간(초) 넘게 쫓아도 사거리에 닿지 못하면 다른 상대로 바꾼다. 예전에는 3~6초마다 무작위로 바꿨다.</summary>
        public const float ChaseGiveUpSeconds = 3f;

        /// <summary>대상을 바꾼 뒤 점수 비교로 다시 돌아가지 않게 붙잡아 두는 시간(초).</summary>
        public const float SwitchHoldSeconds = 2f;

        /// <summary>
        /// 그래프로 움직이는 캐릭터의 밀어내기 세기 배율. 다가가기와 같은 세기로 밀면 둘이 프레임마다 엇갈려 제자리에서 떨었다.
        /// </summary>
        public const float GraphSeparationShare = 0.5f;

        /// <summary>상대 편과는 이 비율의 거리 안으로 겹칠 때만 민다. 근접 정지 거리보다 넓게 밀면 다가가기와 싸웠다.</summary>
        public const float OpponentSeparationShare = 0.5f;

        /// <summary>근접이 휘두른 뒤 아직 물러날 시간인가.</summary>
        public static bool InMeleeRecoveryStep(float secondsSinceAttack) =>
            secondsSinceAttack >= 0f && secondsSinceAttack < MeleeRecoveryStepSeconds;

        /// <summary>물러나는 길이 막혀 벽을 따라 미끄러지기 시작하면 그 방향을 이 시간(초) 유지한다.</summary>
        public const float KiteSlideHoldSeconds = 0.6f;

        /// <summary>한 걸음에서 이 비율 넘게 나아갔으면 막히지 않은 것으로 본다.</summary>
        public const float KiteProgressShare = 0.5f;

        public static bool MadeProgress(float moved, float step) => step <= 0f || moved >= step * KiteProgressShare;

        /// <summary>근접 회복 걸음이 멈추는 거리(사거리 배율). 이 밖으로 물러나면 다시 다가가느라 앞뒤로 떨었다.</summary>
        public const float MeleeRecoveryStepMaxReach = 0.95f;

        /// <summary>더 물러나도 사거리 안에 남는가.</summary>
        public static bool KeepsMeleeReach(float distance, float attackRange) => distance < attackRange * MeleeRecoveryStepMaxReach;

        /// <summary>같은 대상을 이만큼 쫓았는데도 닿지 못했으면 바꾼다.</summary>
        public static bool ShouldGiveUpChase(float chasingSeconds) => chasingSeconds >= ChaseGiveUpSeconds;
    }
}
