using Code.Run;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>규칙의 종류를 화면 의미색에 대응한다. 전투 판단은 하지 않는다.</summary>
    public static class RunVisualPalette
    {
        public static Color Affinity(RunAffinity affinity) => affinity switch
        {
            RunAffinity.Magic => HudPalette.AffinityMagic,
            RunAffinity.Curse => HudPalette.AffinityCurse,
            _ => HudPalette.AffinityPhysical,
        };

        public static Color RoomFill(RunRoomKind kind) => kind switch
        {
            RunRoomKind.Entrance => HudPalette.RoomEntrance,
            RunRoomKind.Trap => HudPalette.RoomTrap,
            RunRoomKind.HealingSpring => HudPalette.RoomSpring,
            RunRoomKind.DoorControl => HudPalette.RoomDoor,
            RunRoomKind.Vault => HudPalette.RoomVault,
            RunRoomKind.Heart => HudPalette.RoomHeart,
            _ => HudPalette.Surface,
        };

        public static Color RoomBorder(RunRoomKind kind) => kind switch
        {
            RunRoomKind.Entrance => HudPalette.RoomEntranceBorder,
            RunRoomKind.Trap => HudPalette.RoomTrapBorder,
            RunRoomKind.HealingSpring => HudPalette.RoomSpringBorder,
            RunRoomKind.DoorControl => HudPalette.RoomDoorBorder,
            RunRoomKind.Vault => HudPalette.RoomVaultBorder,
            RunRoomKind.Heart => HudPalette.RoomHeartBorder,
            _ => HudPalette.Border,
        };
    }
}
