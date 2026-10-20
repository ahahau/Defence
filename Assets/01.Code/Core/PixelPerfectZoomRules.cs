using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Code.Core
{
    /// <summary>
    /// PixelPerfectCamera 확대 단계. 이 카메라는 카메라 크기를 기준 해상도로 직접 정해 매 프레임 덮어쓰므로,
    /// 확대·축소는 카메라 크기가 아니라 기준 해상도를 바꿔서 한다. 픽셀이 정수 배율로 그려져 그림이 늘 선명하다.
    ///
    /// 1920×1080 화면에서 단계별 카메라 크기: 480×270 → 4.8, 640×360 → 6.4, 960×540 → 9.6, 1280×720 → 19.3
    /// (정수 배율이라 1280×720은 1배로 내려가 크게 넓어진다).
    /// </summary>
    public static class PixelPerfectZoomRules
    {
        /// <summary>가까이(작은 기준 해상도) → 멀리.</summary>
        public static readonly Vector2Int[] Levels =
        {
            new(480, 270), new(640, 360), new(960, 540), new(1280, 720)
        };

        /// <summary>방을 고를 때 쓰는 단계(960×540): 큰 방 하나가 화면에 들어온다.</summary>
        public const int FocusLevel = 2;

        /// <summary>지금 기준 해상도에 가장 가까운 단계.</summary>
        public static int LevelOf(PixelPerfectCamera camera)
        {
            if (camera == null)
                return Levels.Length - 1;

            var best = 0;
            var bestDistance = int.MaxValue;
            for (var i = 0; i < Levels.Length; i++)
            {
                var distance = Mathf.Abs(Levels[i].y - camera.refResolutionY);
                if (distance >= bestDistance)
                    continue;

                best = i;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>그 단계로 맞춘다. 범위 밖이면 가장자리 단계로 자른다.</summary>
        public static int Apply(PixelPerfectCamera camera, int level)
        {
            level = Mathf.Clamp(level, 0, Levels.Length - 1);
            if (camera == null)
                return level;

            camera.refResolutionX = Levels[level].x;
            camera.refResolutionY = Levels[level].y;
            return level;
        }
    }
}
