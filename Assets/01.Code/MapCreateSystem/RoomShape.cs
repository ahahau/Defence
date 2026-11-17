using System;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 방 하나의 모양: 칸마다 바닥('.')인지 막힌 곳('#')인지, 문이 난 변, 크기.
    /// 실행 중에 시드로 깎은 방(<see cref="RoomShapeGenerator"/>)과 손으로 만든 특별 방 프리팹(<see cref="RoomLayout"/>)이
    /// 같은 모양 데이터로 그려지고 저장된다. 만든 뒤에는 바뀌지 않는다.
    /// </summary>
    public sealed class RoomShape
    {
        private readonly string[] _lines;

        public RoomShape(string id, RoomSize size, bool isEntrance, RoomDoors doors, string[] lines, RoomLayout prefab = null)
        {
            Id = id ?? string.Empty;
            Size = size;
            IsEntrance = isEntrance;
            Doors = doors;
            _lines = lines != null ? (string[])lines.Clone() : Array.Empty<string>();
            Prefab = prefab;
        }

        /// <summary>프리팹 방이면 프리팹 이름, 생성한 방이면 빈 값이 아닌 생성 표식.</summary>
        public string Id { get; }

        public RoomSize Size { get; }
        public bool IsEntrance { get; }
        public RoomDoors Doors { get; }

        /// <summary>손으로 만든 특별 방이면 그 프리팹(자식 장식을 방 위에 놓는다). 생성한 방은 null.</summary>
        public RoomLayout Prefab { get; }

        public int Columns => _lines.Length > 0 && _lines[0] != null ? _lines[0].Length : 0;
        public int Rows => _lines.Length;

        /// <summary>저장용 줄(위 줄이 0). 복사본을 준다.</summary>
        public string[] CopyLines() => (string[])_lines.Clone();

        /// <summary>직렬화된 줄 그대로(위 줄이 0). 검사용.</summary>
        public string GetRowText(int index) => index >= 0 && index < _lines.Length ? _lines[index] : null;

        public bool HasDoor(RoomDoors side) => RoomDoorRules.Has(Doors, side);

        /// <summary>바닥 칸인가. 행은 아래가 0이다. 범위 밖은 바닥이 아니다.</summary>
        public bool IsFloor(int column, int row)
        {
            if (column < 0 || row < 0 || column >= Columns || row >= Rows)
                return false;

            var line = _lines[Rows - 1 - row];
            return line != null && column < line.Length && line[column] == '.';
        }
    }
}
