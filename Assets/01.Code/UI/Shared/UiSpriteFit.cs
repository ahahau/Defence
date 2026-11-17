using UnityEngine;

namespace Code.UI
{
    /// <summary>
    /// 그림에서 실제로 칠해진 영역만 칸에 맞추는 규칙. 캐릭터 애니메이션 프레임은 무기 휘두를 자리까지 투명하게 비워 둔
    /// 큰 캔버스라, 그림 전체를 칸에 맞추면 몸이 칸의 절반 남짓으로 작게 보였다. 칠해진 영역은 Tight 메시 꼭짓점으로 잰다.
    /// 그림은 칸보다 커지므로 칸 쪽에 <see cref="UnityEngine.UI.RectMask2D"/>를 두어 넘친 여백을 자른다.
    /// </summary>
    public static class UiSpriteFit
    {
        /// <summary>그림 칸(픽셀, 왼쪽 아래 원점) 안에서 칠해진 영역. 메시가 없으면 그림 칸 전체.</summary>
        public static Rect OpaqueRect(Sprite sprite)
        {
            var size = sprite.rect.size;
            var vertices = sprite.vertices;
            if (vertices == null || vertices.Length == 0)
                return new Rect(Vector2.zero, size);

            var min = vertices[0];
            var max = vertices[0];
            for (var i = 1; i < vertices.Length; i++)
            {
                min = Vector2.Min(min, vertices[i]);
                max = Vector2.Max(max, vertices[i]);
            }

            // 꼭짓점은 기준점 기준 유닛 좌표다. 픽셀로 바꿔 그림 칸 왼쪽 아래 기준으로 옮긴다.
            var ppu = sprite.pixelsPerUnit;
            var pixelMin = min * ppu + sprite.pivot;
            var pixelMax = max * ppu + sprite.pivot;
            return Rect.MinMaxRect(pixelMin.x, pixelMin.y, pixelMax.x, pixelMax.y);
        }

        /// <summary>
        /// 칠해진 영역이 칸(<paramref name="box"/>)에 꼭 맞게 들어가도록 그림 전체의 크기와, 칸 가운데에서 그림 가운데까지의 거리를 정한다.
        /// 그림 RectTransform은 칸 가운데에 앵커·피벗을 둔다.
        /// </summary>
        public static (Vector2 size, Vector2 offset) Fit(Vector2 spriteSize, Rect opaque, Vector2 box)
        {
            if (opaque.width <= 0f || opaque.height <= 0f || spriteSize.x <= 0f || spriteSize.y <= 0f)
                return (box, Vector2.zero);

            var scale = Mathf.Min(box.x / opaque.width, box.y / opaque.height);
            return (spriteSize * scale, (spriteSize * 0.5f - opaque.center) * scale);
        }

        /// <summary>그림을 칸에 맞춰 놓는다. 그림이 없으면 칸 크기로 되돌린다.</summary>
        public static void Apply(UnityEngine.UI.Image image, Vector2 box)
        {
            var rt = image.rectTransform;
            var sprite = image.sprite;
            if (sprite == null)
            {
                rt.sizeDelta = box;
                rt.anchoredPosition = Vector2.zero;
                return;
            }

            var (size, offset) = Fit(sprite.rect.size, OpaqueRect(sprite), box);
            image.preserveAspect = false;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
        }
    }
}
