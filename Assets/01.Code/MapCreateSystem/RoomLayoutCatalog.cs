using System.Collections.Generic;
using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 던전에 쓰는 방 프리팹 목록. 확장 후보는 여기서 크기와 문을 보고 고르고, 저장은 이름으로 되찾는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Dungeon Keeper/Room Layout Catalog", fileName = "RoomLayoutCatalog")]
    public sealed class RoomLayoutCatalog : ScriptableObject
    {
        [SerializeField] private RoomLayout entrance;
        [SerializeField] private List<RoomLayout> rooms = new();

        public RoomLayout Entrance => entrance;
        public IReadOnlyList<RoomLayout> Rooms => rooms;

        /// <summary>저장된 이름으로 방을 찾는다. 없으면 null.</summary>
        public RoomLayout Find(string layoutId)
        {
            if (string.IsNullOrEmpty(layoutId))
                return null;

            if (entrance != null && entrance.LayoutId == layoutId)
                return entrance;

            for (var i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] != null && rooms[i].LayoutId == layoutId)
                    return rooms[i];
            }

            return null;
        }

        public RoomLayout Pick(RoomSize size, RoomDoors requiredDoor, float roll01) =>
            RoomLayoutRules.Pick(rooms, size, requiredDoor, roll01);

#if UNITY_EDITOR
        /// <summary>에디터 생성기가 목록을 채운다.</summary>
        public void EditorSet(RoomLayout entranceLayout, List<RoomLayout> roomLayouts)
        {
            entrance = entranceLayout;
            rooms = roomLayouts;
        }
#endif
    }
}
