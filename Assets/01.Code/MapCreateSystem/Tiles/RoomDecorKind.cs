namespace Code.MapCreateSystem
{
    /// <summary>방 장식의 갈래. 갈래마다 차지하는 타일 크기가 정해져 있다.</summary>
    public enum RoomDecorKind
    {
        /// <summary>위쪽 벽 횃불(타일 1×2, 벽돌 면 두 줄). 빛을 낸다.</summary>
        Torch,

        /// <summary>위쪽 벽 큰 장식(타일 2×2, 벽돌 면 두 줄): 가고일 석상.</summary>
        WallLarge,

        /// <summary>위쪽 벽 작은 장식(타일 1×1, 벽돌 면 아래 줄): 보옥 홈·촛대.</summary>
        WallSmall,

        /// <summary>바닥 소품(타일 1×1): 뼈·해골·핏자국·잔해·항아리.</summary>
        Floor
    }
}
