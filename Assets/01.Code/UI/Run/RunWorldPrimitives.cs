using TMPro;
using UnityEngine;

namespace Code.UI.Run
{
    /// <summary>단면도와 예고 표시가 공유하는 단순 그림. 생성은 화면 준비 때만 수행한다.</summary>
    public class RunWorldPrimitives
    {
        private readonly Transform _root;
        public Sprite Square { get; }
        public Sprite Dot { get; }
        public Sprite Ring { get; }

        public RunWorldPrimitives(Transform root)
        {
            _root = root;
            Texture2D white = Texture2D.whiteTexture;
            Square = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(0.5f, 0.5f), white.width);
            Dot = Circle(false);
            Ring = Circle(true);
        }

        public SpriteRenderer Quad(string name, Vector3 position, Vector2 size, Color color, int order, Transform parent = null)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public SpriteRenderer Line(string name, Vector3 from, Vector3 to, float width, Color color, int order, Transform parent = null)
        {
            SpriteRenderer line = Quad(name, Vector3.zero, Vector2.one, color, order, parent);
            Stretch(line.transform, from, to, width);
            return line;
        }

        public TextMeshPro Label(string name, Transform parent, float size, Color color, Vector2 bounds)
        {
            GameObject go = new(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            TextMeshPro text = go.AddComponent<TextMeshPro>();
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.rectTransform.sizeDelta = bounds;
            text.fontStyle = FontStyles.Bold;
            text.outlineWidth = 0.2f;
            text.outlineColor = HudPalette.Deep;
            text.sortingOrder = 60;
            text.raycastTarget = false;
            return text;
        }

        public static void Stretch(Transform line, Vector3 from, Vector3 to, float width)
        {
            Vector3 delta = to - from;
            line.position = (from + to) * 0.5f;
            line.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            line.localScale = new Vector3(delta.magnitude, width, 1f);
        }

        public void Dispose()
        {
            Object.Destroy(Square);
            Object.Destroy(Dot.texture);
            Object.Destroy(Dot);
            Object.Destroy(Ring.texture);
            Object.Destroy(Ring);
        }

        private static Sprite Circle(bool ring)
        {
            const int size = 48;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    pixels[y * size + x] = distance < 23f && (!ring || distance > 19f) ? Color.white : Color.clear;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
