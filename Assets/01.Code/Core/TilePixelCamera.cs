using Code.MapCreateSystem;
using UnityEngine;

namespace Code.Core
{
    /// <summary>
    /// 던전 타일 텍셀 하나가 늘 정수 픽셀로 그려지게 카메라 크기를 정한다(PixelPerfectCamera 대신).
    ///
    /// 타일은 16px 그림을 0.625유닛에 깔아 1유닛 = 25.6텍셀이다. PixelPerfectCamera는 정수 PPU만 받아 28(캐릭터 그림)로 맞춰 두었더니
    /// 텍셀 하나가 1.09픽셀이 되어 칸 경계마다 테두리 픽셀이 두 줄로 겹쳐 바닥에 칸 줄무늬가 생겼다.
    /// 확대 단계는 PixelPerfectCamera와 같이 기준 해상도로 고른다: 화면이 기준 해상도의 몇 배인지(정수, 최소 1)가 텍셀당 픽셀 수다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class TilePixelCamera : MonoBehaviour
    {
        /// <summary>던전 타일 그림 한 장의 픽셀 수.</summary>
        public const int TilePixels = 16;

        /// <summary>1유닛에 들어가는 타일 텍셀 수(25.6).</summary>
        public const float TexelsPerUnit = TilePixels / RoomLayoutRules.TileWorldSize;

        [SerializeField, Min(1)] private int refResolutionX = 1280;
        [SerializeField, Min(1)] private int refResolutionY = 720;

        private Camera _camera;

        public int RefResolutionX
        {
            get => refResolutionX;
            set => refResolutionX = Mathf.Max(1, value);
        }

        public int RefResolutionY
        {
            get => refResolutionY;
            set => refResolutionY = Mathf.Max(1, value);
        }

        /// <summary>화면 크기와 기준 해상도로 카메라 크기를 구한다. 텍셀당 픽셀 = 화면 ÷ 기준 해상도(정수 내림, 최소 1).</summary>
        public static float OrthographicSize(int screenWidth, int screenHeight, int refWidth, int refHeight)
        {
            var pixelsPerTexel = Mathf.Max(1, Mathf.Min(screenWidth / Mathf.Max(1, refWidth), screenHeight / Mathf.Max(1, refHeight)));
            return screenHeight / (2f * TexelsPerUnit * pixelsPerTexel);
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            _camera.orthographicSize = OrthographicSize(_camera.pixelWidth, _camera.pixelHeight, refResolutionX, refResolutionY);
        }
    }
}
