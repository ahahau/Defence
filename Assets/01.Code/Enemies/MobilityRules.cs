using UnityEngine;

namespace Code.Enemies
{
    /// <summary>
    /// 모험가 기동성(0~10)이 주는 효과. 수치는 여기에만 둔다(2026-10-21 사용자 결정).
    /// 1점마다 공격 회피 +2%p, 함정 피하기 +4%p, 이동 속도 +5%. 가볍고 빠른 모험가가 함정 길을 잘 빠져나가게 해
    /// 함정만 깔아 두면 끝나는 판을 막는다.
    /// </summary>
    public static class MobilityRules
    {
        public const int MaxMobility = 10;
        public const float EvasionPerPoint = 0.02f;
        public const float TrapAvoidPerPoint = 0.04f;
        public const float SpeedPerPoint = 0.05f;

        public static int Clamp(int mobility) => Mathf.Clamp(mobility, 0, MaxMobility);

        public static float EvasionBonus(int mobility) => Clamp(mobility) * EvasionPerPoint;

        public static float SpeedMultiplier(int mobility) => 1f + Clamp(mobility) * SpeedPerPoint;

        /// <summary>
        /// 함정 칸에 들어선 순간 실제로 밟을 확률 = 함정 발동 확률 × (1 − 기동성 × 4%).
        /// 칸에 들어설 때 한 번만 굴린다(매 프레임 굴리면 서 있는 동안 결국 밟는다).
        /// </summary>
        public static float StepChance(float trapTriggerChance, int mobility) =>
            Mathf.Clamp01(trapTriggerChance) * (1f - Clamp(mobility) * TrapAvoidPerPoint);
    }
}
