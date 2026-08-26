using System.Collections.Generic;
using _01.Code.Manager;
using UnityEngine;

namespace _01.Code.Progression
{
    /// <summary>
    /// 이번 판에서 각 마을을 얼마나 장악했는지.
    /// 카탈로그 에셋에 담으면 플레이를 끝내도 값이 남아 다음 판이 오염되므로 런타임에만 든다.
    /// 원정 결과가 쓰고 웨이브 편성이 읽는, 두 시스템 사이의 유일한 접점이다.
    /// </summary>
    public static class VillageConquestState
    {
        private static readonly Dictionary<AdventurerPartySO, int> ConquestByParty = new();

        /// <summary>마을이 하나도 없을 때 0으로 나누지 않기 위해 마을 수를 따로 센다.</summary>
        private static int villageCount;
        private static int totalConquest;

        /// <summary>장악한 정도의 평균(0~1). 웨이브가 얼마나 줄어들지는 이 값으로 정해진다.</summary>
        public static float AverageConquestRatio =>
            villageCount <= 0 ? 0f : Mathf.Clamp01(totalConquest / (float)(villageCount * 100));

        /// <summary>도메인 리로드를 꺼 두면 정적 값이 다음 플레이까지 살아남는다. 진입할 때마다 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            ConquestByParty.Clear();
            villageCount = 0;
            totalConquest = 0;
        }

        /// <summary>판이 시작될 때 마을 목록을 등록한다. 등록된 마을 수가 곧 평균의 분모다.</summary>
        public static void Register(AdventurerPartySO originParty, int startingConquest)
        {
            villageCount++;
            var clamped = Mathf.Clamp(startingConquest, 0, 100);
            totalConquest += clamped;

            if (originParty != null)
                ConquestByParty[originParty] = clamped;
        }

        public static void SetConquest(AdventurerPartySO originParty, int conquest)
        {
            var clamped = Mathf.Clamp(conquest, 0, 100);
            var previous = originParty != null && ConquestByParty.TryGetValue(originParty, out var stored)
                ? stored
                : 0;

            totalConquest += clamped - previous;

            if (originParty != null)
                ConquestByParty[originParty] = clamped;
        }

        /// <summary>
        /// 이 파티가 이번에 오지 않을 확률(0~1). 장악도가 곧 억제 확률이라
        /// 완전히 장악한 마을에서는 더 이상 습격대가 오지 않는다.
        /// </summary>
        public static float GetSuppression(AdventurerPartySO party)
        {
            if (party == null || !ConquestByParty.TryGetValue(party, out var conquest))
                return 0f;

            return Mathf.Clamp01(conquest / 100f);
        }
    }
}
