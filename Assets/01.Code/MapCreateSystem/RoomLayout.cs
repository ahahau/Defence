using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 손으로 만든 방 프리팹(입구·특별 방). 칸마다 바닥('.')인지 막힌 곳('#', 기둥·빈 공간)인지와, 문이 난 변을 든다.
    /// 보통 방은 실행 중에 깎는다(<see cref="RoomShapeGenerator"/>). 타일은 모양으로 칠하고, 프리팹의 자식은 장식으로 방 위에 놓인다.
    /// </summary>
    public sealed class RoomLayout : MonoBehaviour
    {
        [SerializeField, Tooltip("저장에 남는 이름. 비우면 프리팹 이름을 쓴다. 이미 쓰인 이름은 바꾸지 않는다.")]
        private string layoutId;

        [SerializeField] private RoomSize size = RoomSize.Medium;

        [SerializeField, Tooltip("입구 방. 오른쪽 문은 바깥문이다.")]
        private bool isEntrance;

        [SerializeField] private RoomDoors doors = RoomDoors.Left | RoomDoors.Right;

        [SerializeField, Tooltip("위 줄부터. '.' 바닥, '#' 막힌 칸(기둥·빈 공간). 줄마다 길이가 같아야 한다.")]
        private string[] cellRows = { "......", "......", "......", "......", "......", "......" };

        public string LayoutId => string.IsNullOrEmpty(layoutId) ? name : layoutId;
        public RoomSize Size => size;
        public bool IsEntrance => isEntrance;
        public RoomDoors Doors => doors;
        public int Columns => cellRows != null && cellRows.Length > 0 && cellRows[0] != null ? cellRows[0].Length : 0;
        public int Rows => cellRows != null ? cellRows.Length : 0;

        public bool HasDoor(RoomDoors side) => RoomDoorRules.Has(doors, side);

        /// <summary>바닥 칸인가. 행은 아래가 0이다. 범위 밖은 바닥이 아니다.</summary>
        public bool IsFloor(int column, int row)
        {
            if (column < 0 || row < 0 || column >= Columns || row >= Rows)
                return false;

            var line = cellRows[Rows - 1 - row];
            return line != null && column < line.Length && line[column] == '.';
        }

        /// <summary>직렬화된 줄 그대로(위 줄이 0). 검사용.</summary>
        public string GetRowText(int index) =>
            cellRows != null && index >= 0 && index < cellRows.Length ? cellRows[index] : null;

        /// <summary>그리기·격자·저장이 쓰는 모양 데이터. 프리팹을 함께 들어 장식을 놓을 수 있게 한다.</summary>
        public RoomShape ToShape() => new(LayoutId, size, isEntrance, doors, cellRows, this);

        /// <summary>에디터 생성기와 테스트가 모양을 채운다.</summary>
        public void Configure(string id, RoomSize roomSize, bool entrance, RoomDoors roomDoors, string[] rows)
        {
            layoutId = id;
            size = roomSize;
            isEntrance = entrance;
            doors = roomDoors;
            cellRows = rows;
        }
    }
}
