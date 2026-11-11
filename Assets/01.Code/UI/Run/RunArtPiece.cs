using System;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>그림 시트의 한 조각. 좌표는 시트 왼쪽 위 기준 픽셀이다(팩 원본의 잘라 쓰기 설정은 건드리지 않는다).</summary>
    [Serializable]
    public struct RunArtPiece
    {
        [SerializeField] private Texture2D texture;
        [SerializeField] private RectInt pixels;

        public RunArtPiece(Texture2D texture, RectInt pixels)
        {
            this.texture = texture;
            this.pixels = pixels;
        }

        public Texture2D Texture => texture;
        public RectInt Pixels => pixels;
        public bool IsSet => texture != null && pixels.width > 0 && pixels.height > 0;
    }
}
