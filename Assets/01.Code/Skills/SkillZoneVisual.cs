using DG.Tweening;
using UnityEngine;

namespace _01.Code.Skills
{
    /// <summary>장판형 스킬 공용 비주얼 유틸. 원형 스프라이트 생성과 장판 GO 구성을 담당한다.</summary>
    public static class SkillZoneVisual
    {
        private static Sprite _circleSprite;

        public static Sprite CircleSprite
        {
            get
            {
                if (_circleSprite != null) return _circleSprite;

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                var radius = size * 0.48f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dist = Vector2.Distance(new Vector2(x, y), center);
                        texture.SetPixel(x, y, dist <= radius ? Color.white : Color.clear);
                    }
                }
                texture.Apply();
                _circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                return _circleSprite;
            }
        }

        /// <summary>반경 radius의 원형 장판 GO를 만들어 SpriteRenderer를 돌려준다.</summary>
        public static SpriteRenderer CreateZone(string zoneName, Vector3 center, float radius, Color color, int sortingOrder = 5)
        {
            var zone = new GameObject(zoneName);
            zone.transform.position = center;

            var renderer = zone.AddComponent<SpriteRenderer>();
            renderer.sprite = CircleSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            zone.transform.localScale = Vector3.one * (radius * 2f);
            return renderer;
        }

        /// <summary>은은한 알파 펄스를 넣어 장판이 활성 상태임을 보여준다.</summary>
        public static void AddPulse(SpriteRenderer renderer, float dimAlphaScale = 0.55f, float pulseDuration = 0.55f)
        {
            if (renderer == null) return;

            var baseColor = renderer.color;
            var dimColor = baseColor;
            dimColor.a = baseColor.a * dimAlphaScale;

            renderer.DOColor(dimColor, pulseDuration)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine)
                .SetLink(renderer.gameObject);
        }
    }
}
