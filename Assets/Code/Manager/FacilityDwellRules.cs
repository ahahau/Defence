using Code.Buildings;
using Code.Enemies;
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

        /// <summary>시설 줄에서 기다린 1초마다 줄어드는 이용 수입 비율. 상점과 그 밖의 시설 기준값.</summary>
        public const float QueueIncomePenaltyPerSecond = 0.05f;

        /// <summary>쉬러 온 주점 손님은 느긋하다.</summary>
        public const float InnQueuePenaltyPerSecond = 0.03f;

        /// <summary>장비를 맡기러 온 대장간 손님은 그 중간이다.</summary>
        public const float BlacksmithQueuePenaltyPerSecond = 0.04f;

        public const float CowardQueuePenaltyMultiplier = 1.5f;
        public const float PriestQueuePenaltyMultiplier = 0.8f;
        public const float ShopaholicQueuePenaltyMultiplier = 0.6f;

        /// <summary>대기 때문에 줄어들 수 있는 이용 수입의 최대 비율.</summary>
        public const float MaxQueueIncomePenalty = 0.5f;

        /// <summary>대체 시설을 찾기 시작한 뒤 불만 탈출까지 적어도 남기는 시간(초).</summary>
        public const float EscapeMarginSeconds = 2f;

        /// <summary>
        /// 이 손님이 이 시설 줄에서 1초마다 잃는 만족도. 시설 기준값에 성격 배율을 곱한다.
        /// 겁쟁이는 조급하고, 쇼핑광은 물건 앞에서 오래 참는다.
        /// </summary>
        public static float QueuePenaltyPerSecond(Building facility, AdventurerTrait trait)
        {
            var baseRate = facility switch
            {
                Inn => InnQueuePenaltyPerSecond,
                Blacksmith => BlacksmithQueuePenaltyPerSecond,
                _ => QueueIncomePenaltyPerSecond
            };

            return baseRate * trait switch
            {
                AdventurerTrait.Coward => CowardQueuePenaltyMultiplier,
                AdventurerTrait.Priest => PriestQueuePenaltyMultiplier,
                AdventurerTrait.Shopaholic => ShopaholicQueuePenaltyMultiplier,
                _ => 1f
            };
        }

        /// <summary>그만큼 기다린 뒤의 만족도(0.5~1). 대기 감액이 최대 절반이라 0.5 아래로 떨어지지 않는다.</summary>
        public static float QueueSatisfaction(float waitSeconds, float penaltyPerSecond) =>
            1f - Mathf.Min(MaxQueueIncomePenalty, Mathf.Max(0f, waitSeconds) * Mathf.Max(0f, penaltyPerSecond));

        /// <summary>만족도가 바닥에 닿아 다른 시설을 찾기 시작하는 대기 시간.</summary>
        public static float AlternativeSearchSeconds(float penaltyPerSecond) =>
            penaltyPerSecond > 0f ? MaxQueueIncomePenalty / penaltyPerSecond : float.MaxValue;

        /// <summary>
        /// 갈 곳 없이 이만큼 기다리면 불만 탈출한다. 성격별 참을성을 따르되, 대체 시설을 찾아볼
        /// 틈도 없이 떠나지 않게 탐색 시작보다 늘 뒤에 둔다.
        /// </summary>
        public static float EscapeWaitSeconds(AdventurerTrait trait, float penaltyPerSecond) =>
            Mathf.Max(AdventurerTraitRules.GetEscapeWaitSeconds(trait),
                AlternativeSearchSeconds(penaltyPerSecond) + EscapeMarginSeconds);

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

        /// <summary>
        /// 대기 만족도를 이용 총액에 반영한다. 대기가 길어도 최소 절반은 지불하므로
        /// 대기열이 시설 방문 자체를 완전히 무의미하게 만들지는 않는다.
        /// </summary>
        public static int ApplyQueueSatisfaction(int totalGold, float waitSeconds) =>
            ApplyQueueSatisfaction(totalGold, waitSeconds, QueueIncomePenaltyPerSecond);

        /// <summary>시설·성격별 감소 속도로 대기 만족도를 이용 총액에 반영한다.</summary>
        public static int ApplyQueueSatisfaction(int totalGold, float waitSeconds, float penaltyPerSecond)
        {
            if (totalGold <= 0)
                return 0;

            return Mathf.Max(1, Mathf.CeilToInt(totalGold * QueueSatisfaction(waitSeconds, penaltyPerSecond)));
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
