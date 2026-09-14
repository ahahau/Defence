using UnityEngine;

namespace _01.Code.Manager
{
    /// <summary>웨이브 전리품 계산. 장면 상태 없이 예고 보상·적 수·처치 수만으로 결정한다.</summary>
    public static class WaveRewardRules
    {
        public static int ResolveClearGold(int promisedGold, int enemyCount, int killCount)
        {
            if (promisedGold <= 0)
                return 0;

            var total = Mathf.Max(0, enemyCount);
            if (total == 0)
                return promisedGold;

            var killed = Mathf.Clamp(killCount, 0, total);
            return Mathf.RoundToInt(promisedGold * (killed / (float)total));
        }
    }
}
