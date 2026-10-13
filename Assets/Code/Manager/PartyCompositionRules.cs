using System.Collections.Generic;
using UnityEngine;

namespace Code.Manager
{
    /// <summary>
    /// 파티가 올 때마다 인원이 조금씩 달라지는 규칙(기획서: 확률형 개별 입장, 시간대·자산·등급 기반 인원).
    ///
    /// 짜 둔 파티가 늘 같은 인원으로 오면 몇 번 보고 나서 배치가 굳는다. 리더는 반드시 오고, 나머지는
    /// 확률로 빠지거나(밤에는 더 잘 빠진다) 한 명이 더 따라온다(던전 등급과 시설이 클수록 잘 붙는다).
    /// 짜 둔 일차의 성격(누가 리더이고 어떤 역할이 섞였는지)은 그대로 남는다.
    /// </summary>
    public static class PartyCompositionRules
    {
        public const float BaseDropChance = 0.15f;
        public const float NightDropBonus = 0.1f;
        public const float BaseExtraChance = 0.05f;
        public const float ExtraChancePerGrade = 0.03f;
        public const float ExtraChancePerFacility = 0.02f;
        public const float MaxExtraChance = 0.4f;

        /// <summary>리더가 아닌 파티원이 이번에 빠질 확률.</summary>
        public static float DropChance(bool isNight) => BaseDropChance + (isNight ? NightDropBonus : 0f);

        /// <summary>한 명이 더 따라올 확률.</summary>
        public static float ExtraChance(int dungeonGrade, int facilityCount) =>
            Mathf.Min(MaxExtraChance,
                BaseExtraChance + ExtraChancePerGrade * Mathf.Max(0, dungeonGrade)
                                + ExtraChancePerFacility * Mathf.Max(0, facilityCount));

        /// <summary>
        /// 파티원을 굴려 이번에 올 인원을 정한다. 첫 멤버(리더)는 늘 남는다.
        /// </summary>
        public static void Vary<T>(List<T> members, System.Random random, float dropChance, float extraChance)
        {
            if (members == null || members.Count == 0 || random == null)
                return;

            for (var i = members.Count - 1; i >= 1; i--)
            {
                if (random.NextDouble() < dropChance)
                    members.RemoveAt(i);
            }

            if (random.NextDouble() < extraChance)
            {
                // 더 오는 한 명은 리더가 아닌 파티원 중에서 고른다. 혼자 남았으면 리더와 같은 직업이다.
                var pick = members.Count > 1 ? random.Next(1, members.Count) : 0;
                members.Add(members[pick]);
            }
        }
    }
}
