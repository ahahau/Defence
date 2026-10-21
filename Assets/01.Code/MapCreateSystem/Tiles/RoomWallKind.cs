namespace Code.MapCreateSystem
{
    /// <summary>
    /// 3/4 시점 벽 조각의 종류. Dark Castle 예제 씬(Dark_Castle_001)이 쓰는 조각과 같다.
    /// 위쪽 벽은 난간 + 벽돌 면 2줄, 옆·아래 벽은 바닥 쪽 가장자리에 난간만 있다. 벽·기둥 룰 타일의 규칙 하나가 조각 하나다.
    /// </summary>
    public enum RoomWallKind
    {
        FaceLower,
        FaceUpper,
        FaceUpperStart,
        FaceUpperEnd,
        RailTop,
        RailTopStart,
        RailTopEnd,
        WallLeft,
        WallLeftBottom,
        WallLeftFaceUpper,
        WallLeftTop,
        WallRight,
        WallRightBottom,
        WallRightFaceUpper,
        WallRightTop,
        RailBottom,
        RailBottomStart,
        RailBottomEnd,
        CornerBottomLeft,
        CornerBottomRight,
        InnerTopLeft,
        InnerTopRight,
        PillarCapLeft,
        PillarCap,
        PillarCapRight,
        PillarMidLeft,
        PillarMid,
        PillarMidRight,
        PillarBaseLeft,
        PillarBase,
        PillarBaseRight,

        /// <summary>
        /// 위쪽 벽이 계단처럼 올라가는 곳: 낮은 벽의 난간 줄에서 오른쪽 위가 높은 쪽 바닥이면, 높은 쪽의 왼쪽 옆 벽이
        /// 그 줄까지 내려온다. 난간 조각(속이 검다)을 두면 검은 칸으로 보였다.
        /// </summary>
        StepRailLeft,

        /// <summary><see cref="StepRailLeft"/>의 좌우 대칭(└).</summary>
        StepRailRight,

        /// <summary>
        /// 위쪽 벽 아래 줄인데 옆(왼쪽)도 바닥인 곳: 방 안으로 튀어나온 벽 덩어리·홈의 끝. 옆 벽 조각(속이 검다)을 두면
        /// 검은 세로 줄로 보여 왼쪽 가장자리가 있는 벽돌 면을 둔다.
        /// </summary>
        FaceLowerStart,

        /// <summary><see cref="FaceLowerStart"/>의 좌우 대칭(오른쪽 가장자리).</summary>
        FaceLowerEnd
    }
}
