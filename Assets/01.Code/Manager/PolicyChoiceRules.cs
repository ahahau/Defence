using System.Collections.Generic;

namespace Code.Manager
{
    /// <summary>
    /// 운영 방침 창의 고를 수 있음 판정. 상태 없는 규칙이라 테스트에서 바로 부른다.
    /// 예전에는 금화가 모자라도 그대로 차감했다(사용자 결정 2026-10-22: 모자라면 잠근다).
    /// </summary>
    public static class PolicyChoiceRules
    {
        /// <summary>그 방침을 지금 금화로 공포할 수 있는가. 금화를 받는 방침·0인 방침은 늘 된다.</summary>
        public static bool IsAffordable(int goldDeltaOnSelect, int currentGold) =>
            goldDeltaOnSelect >= 0 || currentGold >= -goldDeltaOnSelect;

        /// <summary>
        /// "이번에는 공포하지 않음"을 열지. 제시된 방침이 모두 금화 부족으로 잠겼을 때만 연다 —
        /// 그때 강제 선택 창이 아무것도 고를 수 없는 채로 시간을 멈춰 두면 판이 굳는다.
        /// </summary>
        public static bool ShouldOfferDecline(IReadOnlyList<bool> affordable)
        {
            if (affordable == null || affordable.Count == 0)
                return false;

            foreach (var value in affordable)
            {
                if (value)
                    return false;
            }

            return true;
        }
    }
}
