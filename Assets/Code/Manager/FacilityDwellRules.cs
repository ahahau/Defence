using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 모험가가 시설에 머무는 동안 벌어지는 일.
    ///
    /// 여태 시설은 지나가는 순간 한 번 돈을 받고 끝이었다. 그래서 화면에서 보이는 것(적이
    /// 걸어간다)과 장부에 찍히는 것(금화가 늘었다)이 이어지지 않았다. 머무르게 하면 상점에
    /// 들어가 있는 장면이 곧 버는 장면이 되고, 시설을 깊이 두는 것이 배치의 이유가 된다.
    /// </summary>
    public static class FacilityDwellRules
    {
        /// <summary>머무는 1초마다 쌓이는 피로.</summary>
        public const float FatiguePerSecond = 9f;

        /// <summary>피로가 이 값에 이르면 지친 것으로 본다.</summary>
        public const float MaxFatigue = 100f;

        /// <summary>완전히 지쳤을 때 남는 공격력 비율. 0.55면 45%까지 깎인다.</summary>
        public const float MinAttackMultiplier = 0.55f;

        /// <summary>완전히 지쳤을 때 남는 이동 속도 비율.</summary>
        public const float MinMoveMultiplier = 0.6f;

        /// <summary>
        /// 이 시설에서 1초 머물 때 쓰는 금화.
        ///
        /// 통과 보상 전체를 체류 시간에 나눠 담는다. 오래 잡아 둘수록 더 버는 것이 아니라,
        /// 같은 값을 시간에 걸쳐 받는다 — 체류가 수입을 부풀리려고 넣은 장치가 아니라
        /// 언제 버는지를 화면에 보이게 하려고 넣은 장치이기 때문이다.
        /// </summary>
        public static float GoldPerSecond(int totalGold, float dwellSeconds)
        {
            if (totalGold <= 0 || dwellSeconds <= 0f)
                return 0f;

            return totalGold / dwellSeconds;
        }

        /// <summary>피로 0~100을 공격력 배율로. 지칠수록 약해진다.</summary>
        public static float AttackMultiplierFor(float fatigue) =>
            Mathf.Lerp(1f, MinAttackMultiplier, Mathf.Clamp01(fatigue / MaxFatigue));

        /// <summary>피로 0~100을 이동 속도 배율로.</summary>
        public static float MoveMultiplierFor(float fatigue) =>
            Mathf.Lerp(1f, MinMoveMultiplier, Mathf.Clamp01(fatigue / MaxFatigue));

        /// <summary>그만큼 머문 뒤 쌓이는 피로. 100을 넘지 않는다.</summary>
        public static float AccumulateFatigue(float current, float dwellSeconds) =>
            Mathf.Clamp(current + Mathf.Max(0f, dwellSeconds) * FatiguePerSecond, 0f, MaxFatigue);
    }
}
