using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방을 칠할 룰 타일 셋: 바닥(무늬 무작위), 벽(3/4 시점 조각), 방 안 기둥.
    /// 에디터 생성기(<c>RoomLayoutBuilder</c>)가 Dark Castle 예제 씬이 쓰는 칸으로 규칙을 채운다.
    /// 같은 룰 타일을 타일 팔레트에 올려 프리팹·씬을 손으로 칠할 때도 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Keeper/Room Tile Set", fileName = "RoomTileSet")]
    public sealed class RoomTileSet : ScriptableObject
    {
        [SerializeField] private DungeonFloorRuleTile floor;
        [SerializeField] private DungeonWallRuleTile wall;
        [SerializeField] private TileBase pillar;

        [SerializeField, Tooltip("칸 기둥(방 안 기둥·칸막이) 조각. 순서는 PillarBlockKinds. 비어 있으면 기둥 룰 타일로 그린다.")]
        private TileBase[] pillarBlocks;

        [SerializeField, Tooltip("위쪽 벽 문 애니메이션(닫힘 → 열림). 비어 있으면 문을 그리지 않는다.")]
        private Sprite[] doorFrames;

        [Header("Decor")]
        [SerializeField, Tooltip("위쪽 벽 횃불(애니메이션 타일, 그림 타일 1×2).")]
        private TileBase torch;

        [SerializeField, Tooltip("위쪽 벽 큰 장식(타일 2×2).")]
        private RoomDecorPiece[] wallLarge;

        [SerializeField, Tooltip("위쪽 벽 작은 장식(타일 1×1).")]
        private TileBase[] wallSmall;

        [SerializeField, Tooltip("바닥 소품(타일 1×1).")]
        private TileBase[] floorDecor;

        public DungeonFloorRuleTile Floor => floor;
        public DungeonWallRuleTile Wall => wall;
        public TileBase Pillar => pillar;
        public Sprite[] DoorFrames => doorFrames;
        public TileBase Torch => torch;

        /// <summary><see cref="pillarBlocks"/>의 순서.</summary>
        public static readonly RoomWallKind[] PillarBlockKinds =
        {
            RoomWallKind.PillarTopLeft, RoomWallKind.PillarTop, RoomWallKind.PillarTopRight,
            RoomWallKind.PillarTopLowerLeft, RoomWallKind.PillarTopLower, RoomWallKind.PillarTopLowerRight,
            RoomWallKind.PillarBaseLeft, RoomWallKind.PillarBase, RoomWallKind.PillarBaseRight,
            RoomWallKind.PillarTopMidLeft, RoomWallKind.PillarTopMid, RoomWallKind.PillarTopMidRight
        };

        /// <summary>칸 기둥 조각 타일. 없으면 기둥 룰 타일.</summary>
        public TileBase PillarBlock(RoomWallKind kind)
        {
            var index = System.Array.IndexOf(PillarBlockKinds, kind);
            return pillarBlocks != null && index >= 0 && index < pillarBlocks.Length && pillarBlocks[index] != null
                ? pillarBlocks[index]
                : pillar;
        }

        /// <summary>그 갈래의 그림 수. 0이면 그 갈래는 그리지 않는다.</summary>
        public int DecorCount(RoomDecorKind kind) => kind switch
        {
            RoomDecorKind.Torch => torch != null ? 1 : 0,
            RoomDecorKind.WallLarge => wallLarge?.Length ?? 0,
            RoomDecorKind.WallSmall => wallSmall?.Length ?? 0,
            _ => floorDecor?.Length ?? 0
        };

        /// <summary>장식의 (dx, dy) 타일. pick(0~1)으로 그림을 고른다.</summary>
        public TileBase DecorTile(RoomDecorKind kind, float pick, int dx, int dy)
        {
            var count = DecorCount(kind);
            if (count == 0)
                return null;

            var index = Mathf.Clamp(Mathf.FloorToInt(pick * count), 0, count - 1);
            return kind switch
            {
                RoomDecorKind.Torch => dx == 0 && dy == 0 ? torch : null,
                RoomDecorKind.WallLarge => wallLarge[index]?.At(dx, dy, 2),
                RoomDecorKind.WallSmall => dx == 0 && dy == 0 ? wallSmall[index] : null,
                _ => dx == 0 && dy == 0 ? floorDecor[index] : null
            };
        }

        public bool IsComplete => floor != null && wall != null && pillar != null;

#if UNITY_EDITOR
        public void EditorSet(DungeonFloorRuleTile floorTile, DungeonWallRuleTile wallTile, TileBase pillarTile)
        {
            floor = floorTile;
            wall = wallTile;
            pillar = pillarTile;
        }

        public void EditorSetPillarBlocks(TileBase[] tiles) => pillarBlocks = tiles;

        public void EditorSetDoorFrames(Sprite[] frames) => doorFrames = frames;

        public void EditorSetDecor(TileBase torchTile, RoomDecorPiece[] large, TileBase[] small, TileBase[] floorTiles)
        {
            torch = torchTile;
            wallLarge = large;
            wallSmall = small;
            floorDecor = floorTiles;
        }
#endif
    }
}
