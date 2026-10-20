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

        [SerializeField, Tooltip("위쪽 벽 문 애니메이션(닫힘 → 열림). 비어 있으면 문을 그리지 않는다.")]
        private Sprite[] doorFrames;

        public DungeonFloorRuleTile Floor => floor;
        public DungeonWallRuleTile Wall => wall;
        public TileBase Pillar => pillar;
        public Sprite[] DoorFrames => doorFrames;

        public bool IsComplete => floor != null && wall != null && pillar != null;

#if UNITY_EDITOR
        public void EditorSet(DungeonFloorRuleTile floorTile, DungeonWallRuleTile wallTile, TileBase pillarTile)
        {
            floor = floorTile;
            wall = wallTile;
            pillar = pillarTile;
        }

        public void EditorSetDoorFrames(Sprite[] frames) => doorFrames = frames;
#endif
    }
}
