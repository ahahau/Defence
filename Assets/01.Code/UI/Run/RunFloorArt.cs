using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.UI.Run
{
    /// <summary>
    /// <see cref="RunFloorArtSO"/>의 조각을 실행 중 그림·타일로 한 번만 만들어 둔다. 전투를 다시 차려도 같은 것을 쓴다.
    /// </summary>
    public class RunFloorArt
    {
        private readonly Dictionary<(Texture2D, RectInt), Sprite> _sprites = new();
        private readonly Dictionary<Sprite, Tile> _tiles = new();
        private readonly float _pixelsPerUnit;

        public RunFloorArt(RunFloorArtSO data)
        {
            Data = data;
            _pixelsPerUnit = data.SheetPixelsPerTile / data.TileWorldSize;
        }

        public RunFloorArtSO Data { get; }

        public Sprite SpriteOf(RunArtPiece piece)
        {
            if (!piece.IsSet) return null;
            (Texture2D, RectInt) key = (piece.Texture, piece.Pixels);
            if (_sprites.TryGetValue(key, out Sprite sprite)) return sprite;
            RectInt p = piece.Pixels;
            Rect rect = new(p.x, piece.Texture.height - p.y - p.height, p.width, p.height);
            sprite = Sprite.Create(piece.Texture, rect, new Vector2(0.5f, 0.5f), _pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = $"{piece.Texture.name}_{p.x}_{p.y}";
            _sprites[key] = sprite;
            return sprite;
        }

        public Sprite[] SpritesOf(RunArtPiece[] pieces)
        {
            Sprite[] sprites = new Sprite[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) sprites[i] = SpriteOf(pieces[i]);
            return sprites;
        }

        public Tile TileOf(RunArtPiece piece)
        {
            Sprite sprite = SpriteOf(piece);
            if (sprite == null) return null;
            if (_tiles.TryGetValue(sprite, out Tile tile)) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.name = sprite.name;
            _tiles[sprite] = tile;
            return tile;
        }

        public void Dispose()
        {
            foreach (Tile tile in _tiles.Values) Object.Destroy(tile);
            foreach (Sprite sprite in _sprites.Values) Object.Destroy(sprite);
            _tiles.Clear();
            _sprites.Clear();
        }
    }
}
