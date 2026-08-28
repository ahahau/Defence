namespace _01.Code.Core.Stats
{
    /// <summary>
    /// 스탯 자산의 번호. 자산 파일의 AssetIndex와 짝이 맞아야 한다.
    ///
    /// 번호를 코드에 흩뿌리면 3이 방어인지 공격 주기인지 읽는 사람이 알 수 없고,
    /// 자산 하나를 새로 만들 때 번호가 겹쳐도 드러나지 않는다. 이름을 여기 한 곳에 모아 둔다.
    /// </summary>
    public static class StatIndex
    {
        public const int MaxHealth = 1;
        public const int AttackDamage = 2;
        public const int Defense = 3;
        public const int AttackInterval = 4;
        public const int EvasionChance = 5;
    }
}
