using UnityEngine;

namespace Code.MapCreateSystem
{
    /// <summary>방 장식 하나: 왼쪽 아래 타일, 갈래, 그림을 고를 0~1 값(방·자리마다 늘 같다).</summary>
    public readonly struct RoomDecorPlacement
    {
        public RoomDecorPlacement(Vector2Int tile, RoomDecorKind kind, float pick)
        {
            Tile = tile;
            Kind = kind;
            Pick = pick;
        }

        public Vector2Int Tile { get; }
        public RoomDecorKind Kind { get; }
        public float Pick { get; }
    }
}
