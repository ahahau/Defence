using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>
    /// 전투 평면도 그림 묶음(2026-11-11 사용자 "이 방향 채택"). 바닥·벽 타일, 방 장치, 문, 함정 번개를 담는다.
    /// 생성기 <c>RunFloorArtBuilder</c>가 채운다. 방 한 칸(길이 3)이 타일 6장이다.
    /// </summary>
    [CreateAssetMenu(fileName = "RunFloorArt", menuName = "DungeonKeeper/Run/Floor Art")]
    public class RunFloorArtSO : ScriptableObject
    {
        [field: SerializeField, Min(0.1f), Tooltip("타일 한 장의 월드 크기.")] public float TileWorldSize { get; private set; } = 0.5f;
        [field: SerializeField, Min(1)] public int SheetPixelsPerTile { get; private set; } = 16;
        [field: SerializeField, Min(0), Tooltip("평면도 둘레에 더 까는 벽 칸 수(가로·세로).")] public Vector2Int WallMargin { get; private set; } = new(24, 12);
        [field: SerializeField, Min(0f), Tooltip("입구 굴을 화면 밖까지 늘이는 길이.")] public float EntranceExtra { get; private set; } = 2.5f;

        [field: Header("바닥·벽")]
        [field: SerializeField] public RunArtPiece[] Floors { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField, Tooltip("벽돌 면 바로 아래 바닥.")] public RunArtPiece[] LitFloors { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField, Tooltip("바닥 위쪽 벽의 벽돌 면.")] public RunArtPiece[] WallFaces { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField, Tooltip("벽 윗면 테두리 9조각(위·가운데·아래 줄 × 왼·가운데·오른쪽).")] public RunArtPiece[] Rim { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField, Tooltip("테두리 안쪽 모서리 4조각(바닥이 왼쪽 위·오른쪽 위·왼쪽 아래·오른쪽 아래 대각선). 비면 바깥 모서리로 대신한다.")]
        public RunArtPiece[] RimInner { get; private set; } = System.Array.Empty<RunArtPiece>();

        [field: Header("장치")]
        [field: SerializeField] public RunRoomDecor[] Decor { get; private set; } = System.Array.Empty<RunRoomDecor>();
        [field: SerializeField] public RunArtPiece[] TorchFrames { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField, Tooltip("방 왼쪽 위에서 잰 횃불 자리(타일 칸).")] public Vector2[] TorchCells { get; private set; } = System.Array.Empty<Vector2>();
        [field: SerializeField] public RunArtPiece[] SpringFrames { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField] public Vector2 SpringCell { get; private set; }
        [field: SerializeField] public Color SpringTint { get; private set; } = Color.white;
        [field: SerializeField] public RunArtPiece TrapVentIdle { get; private set; }
        [field: SerializeField] public RunArtPiece TrapVentCharged { get; private set; }
        [field: SerializeField] public Vector2[] TrapVentCells { get; private set; } = System.Array.Empty<Vector2>();
        [field: SerializeField, Min(0.01f)] public float FrameSeconds { get; private set; } = 0.12f;

        [field: Header("문")]
        [field: SerializeField, Tooltip("세로 복도 문(닫힘).")] public RunArtPiece DoorClosed { get; private set; }
        [field: SerializeField, Tooltip("세로 복도 문(열림).")] public RunArtPiece DoorOpen { get; private set; }
        [field: SerializeField, Tooltip("가로 복도 문(잠기면 세움).")] public RunArtPiece DoorPost { get; private set; }
        [field: SerializeField, Min(0.1f)] public float DoorPostScale { get; private set; } = 0.75f;

        [field: Header("방 상태(2026-11-12 사용자 목업 승인)")]
        [field: SerializeField, Tooltip("꺼진 방의 횃불 자리에 놓는 불 없는 화로.")] public RunArtPiece TorchOff { get; private set; }
        [field: SerializeField, Range(0f, 1f), Tooltip("꺼진 방 장치·샘의 회색 밝기.")] public float OffDeviceGray { get; private set; } = 0.55f;
        [field: SerializeField, Tooltip("고장 난 방 바닥에서 깜빡이는 불길 장면.")] public RunArtPiece[] BrokenFlameFrames { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField, Tooltip("불길 자리(방 왼쪽 위에서 잰 타일 칸).")] public Vector2[] BrokenFlameCells { get; private set; } = System.Array.Empty<Vector2>();
        [field: SerializeField, Min(0.1f), Tooltip("불길 크기 배율. 횃불 불꽃만 자른 그림이라 원래 크기로는 너무 작다.")] public float BrokenFlameScale { get; private set; } = 2f;
        [field: SerializeField, Tooltip("고장 난 방 바닥의 파편·불씨. 자리는 같은 순서의 BrokenDebrisCells.")] public RunArtPiece[] BrokenDebris { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField] public Vector2[] BrokenDebrisCells { get; private set; } = System.Array.Empty<Vector2>();
        [field: SerializeField, Tooltip("심장 방 핏자국. 앞 HeartBloodFirstCount개는 체력 2/3 아래, 나머지는 1/3 아래에서 보인다.")] public RunArtPiece[] HeartBlood { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField] public Vector2[] HeartBloodCells { get; private set; } = System.Array.Empty<Vector2>();
        [field: SerializeField, Min(0)] public int HeartBloodFirstCount { get; private set; } = 2;
        [field: SerializeField, Range(0f, 1f), Tooltip("심장 체력 0일 때 기둥 밝기.")] public float HeartDarkest { get; private set; } = 0.35f;

        [field: Header("함정 발동")]
        [field: SerializeField] public RunArtPiece[] StrikeFrames { get; private set; } = System.Array.Empty<RunArtPiece>();
        [field: SerializeField, Min(0.01f)] public float StrikeFrameSeconds { get; private set; } = 0.04f;
        [field: SerializeField, Min(0.1f)] public float StrikeScale { get; private set; } = 0.55f;
        [field: SerializeField, Tooltip("방 가운데에서 잰 번개 그림 중심(월드).")] public Vector2 StrikeOffset { get; private set; } = new(0f, 1.1f);
    }
}
