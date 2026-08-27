using _01.Code.MapCreateSystem;
using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>
    /// 중앙 핵심부 슬롯 주변에 건물을 모아 지으면 결속 단계가 오른다.
    /// 공간을 방어 효율에 쓸지, 재보 보너스를 위한 핵심부 고리에 쓸지 선택하게 한다.
    /// </summary>
    public static class CoreCohesionSystem
    {
        public const int LinksPerTier = 2;
        public const int MaxTier = 3;
        public const int MaxContributingLinks = LinksPerTier * MaxTier;
        public const float RewardBonusPerTier = 0.05f;

        public static int CurrentLinkCount
        {
            get
            {
                var count = 0;
                foreach (var node in Node.ActiveNodes)
                {
                    if (node?.TrapGrid != null)
                        count += node.TrapGrid.CountBuildingsAdjacentToCentralSlot();
                }

                return Mathf.Clamp(count, 0, MaxContributingLinks);
            }
        }

        public static int CurrentTier => Mathf.Clamp(CurrentLinkCount / LinksPerTier, 0, MaxTier);
        public static int CurrentRewardBonusPercent => Mathf.RoundToInt(CurrentTier * RewardBonusPerTier * 100f);

        public static int ScaleGoldReward(int baseReward)
        {
            if (baseReward <= 0)
                return 0;

            return Mathf.Max(0, Mathf.RoundToInt(baseReward * (1f + CurrentTier * RewardBonusPerTier)));
        }

        public static string CurrentReport =>
            $"핵심부 결속 {CurrentLinkCount}/{MaxContributingLinks} · 다음 금고 수익 +{CurrentRewardBonusPercent}%";
    }
}
