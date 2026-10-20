using UnityEngine;

namespace Code.Progression
{
    /// <summary>
    /// 메타 진행도로 새 판 후보에 들어오는 콘텐츠(유닛·함정·시설·사건) 규칙.
    ///
    /// 시작 보정만 열리면 판을 거듭해도 보는 것이 같다. 후반 유닛·함정·승급 시설·일부 사건을 진행도로
    /// 잠가 두면, 버틴 만큼 다음 판에 처음 보는 선택지가 섞인다. 진행도는 판이 끝날 때만 오르므로
    /// 한 판 안에서는 열리고 닫히는 것이 바뀌지 않는다.
    /// </summary>
    public static class MetaUnlockRules
    {
        /// <summary>콘텐츠가 열리는 진행도 단계. 데이터의 필요 진행도도 이 값 중 하나로 둔다.</summary>
        public static readonly int[] Tiers = { 10, 25, 50 };

        /// <summary>지금 메타 기록의 누적 진행도.</summary>
        public static int CurrentProgress => MetaProfileStore.Profile.progress;

        /// <summary>필요 진행도를 채웠는가. 0이면 기록을 읽지 않고 바로 연다.</summary>
        public static bool IsMet(int required) => required <= 0 || CurrentProgress >= required;

        /// <summary>진행도가 before에서 after로 오르며 이번에 열렸는가.</summary>
        public static bool BecameUnlocked(int required, int before, int after) =>
            required > 0 && before < required && after >= required;

        /// <summary>다음으로 열리는 단계. 다 열렸으면 0.</summary>
        public static int NextTier(int progress)
        {
            foreach (var tier in Tiers)
            {
                if (progress < tier)
                    return tier;
            }

            return 0;
        }

        /// <summary>잠긴 콘텐츠 안내 앞에 붙이는 말.</summary>
        public static string LockPrefix(int required) => $"진행도 {Mathf.Max(0, required)} 필요";
    }
}
