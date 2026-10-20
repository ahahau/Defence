using UnityEngine;

namespace Code.Buildings
{
    /// <summary>함정 발동 효과의 프레임·자리 계산.</summary>
    public static class TrapEffectRules
    {
        /// <summary>
        /// 흐른 시간에 보여 줄 프레임. 한 번만 도는 효과가 끝났으면 -1이다.
        /// 반복 효과는 끝나는 시점을 부르는 쪽이 정한다.
        /// </summary>
        public static int FrameAt(float elapsed, float frameSeconds, int frameCount, bool loop)
        {
            if (frameCount <= 0)
                return -1;

            var index = Mathf.FloorToInt(Mathf.Max(0f, elapsed) / Mathf.Max(0.001f, frameSeconds));
            if (loop)
                return index % frameCount;

            return index < frameCount ? index : -1;
        }

        /// <summary>여러 칸 함정에서 (column, row)번째 칸의 중심. 함정의 자리는 덮은 칸들의 가운데다.</summary>
        public static Vector2 TileCenter(Vector2 center, Vector2Int footprint, float cellSize, int column, int row)
        {
            var offsetX = (column - (Mathf.Max(1, footprint.x) - 1) * 0.5f) * cellSize;
            var offsetY = (row - (Mathf.Max(1, footprint.y) - 1) * 0.5f) * cellSize;
            return center + new Vector2(offsetX, offsetY);
        }

        /// <summary>범위 함정의 폭발 폭(칸). 덮은 칸에 사방 범위를 더해 닿는 곳만큼 터져 보이게 한다.</summary>
        public static float AreaWidthCells(Vector2Int footprint, float radiusCells) =>
            Mathf.Max(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y)) + 2f * Mathf.Max(0f, radiusCells);
    }
}
