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
        PillarBaseRight
    }
}
