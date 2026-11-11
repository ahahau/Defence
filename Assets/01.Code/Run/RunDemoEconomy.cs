namespace Code.Run
{
    /// <summary>
    /// 상인 새 항목·글 사건 교환 값(2026-11-11 사용자 "이 표로 시뮬레이션"). 기본값이 게임 값이다.
    /// 후보 비교 시뮬레이션에서만 사본의 값을 메모리에서 바꾼다.
    /// </summary>
    internal sealed class RunDemoEconomy
    {
        internal int MerchantCapacityPrice { get; set; } = 60;
        internal int NewMinionPrice { get; set; } = 50;
        internal int RelicPrice { get; set; } = 40;

        /// <summary>한 상인 방문에서 살 수 있는 항목 수. 0이면 제한 없음(금화가 되는 만큼).</summary>
        internal int MerchantPurchaseLimit { get; set; }

        internal int HeartForGoldHeart { get; set; } = 10;
        internal int HeartForGoldGold { get; set; } = 40;
        internal int UpgradePrice { get; set; } = 40;
        internal int UpgradeHealth { get; set; } = 4;
        internal int UpgradeAttack { get; set; } = 1;

        /// <summary>"부하 체력 → 유물"에서 잃는 최대 체력 비율.</summary>
        internal float HealthForRelicRatio { get; set; } = 0.5f;

        /// <summary>"방 내구 → 마력"이 다음 방어에 더하는 마력 용량.</summary>
        internal int RoomForManaCapacity { get; set; } = 1;
    }
}
