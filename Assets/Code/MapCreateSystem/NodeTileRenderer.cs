using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.MapCreateSystem
{
    /// <summary>
    /// 노드 한 칸을 Dark Castle 타일셋의 바닥·벽·문 레이어로 그린다.
    /// </summary>
    public sealed class NodeTileRenderer : MonoBehaviour
    {
        [Header("Layers")]
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private Tilemap doorTilemap;

        [Header("Floor")]
        [SerializeField] private TileBase floorTile;
        [SerializeField] private TileBase floorAccentTile;

        [Header("Walls")]
        [SerializeField] private TileBase topLeftWallCapTile;
        [SerializeField] private TileBase topLeftCornerTile;
        [SerializeField] private TileBase topRightCornerTile;
        [SerializeField] private TileBase topRightWallCapTile;
        [SerializeField] private TileBase topWallTile;
        [SerializeField] private TileBase bottomLeftWallTile;
        [SerializeField] private TileBase bottomWallTile;
        [SerializeField] private TileBase bottomRightWallTile;
        [SerializeField] private TileBase leftWallTile;
        [SerializeField] private TileBase rightWallTile;

        [Header("Door")]
        [SerializeField] private TileBase entranceDoorBottomTile;
        [SerializeField] private TileBase entranceDoorMiddleTile;
        [SerializeField] private TileBase entranceDoorTopTile;
        [SerializeField, Min(3)] private int columns = 20;
        [SerializeField, Min(3)] private int rows = 20;

        private readonly Tilemap[] _layers = new Tilemap[3];

        private void Awake()
        {
            _layers[0] = floorTilemap;
            _layers[1] = wallTilemap;
            _layers[2] = doorTilemap;

            if (floorTilemap == null || wallTilemap == null || doorTilemap == null || floorTile == null
                || topLeftWallCapTile == null || topWallTile == null || bottomWallTile == null
                || leftWallTile == null || rightWallTile == null)
            {
                Debug.LogError($"{nameof(NodeTileRenderer)} requires its three tilemap layers and Dark Castle tiles.", this);
                enabled = false;
            }
        }

        /// <summary>
        /// 기존 노드의 물리 크기는 건드리지 않고, 타일 레이어만 역스케일해 방 외형을 그린다.
        /// </summary>
        public void Render(Vector3 nodeScale, Color tint, bool hasEntranceDoor)
        {
            if (!enabled)
                return;

            // 노드 루트의 실제 월드 크기를 유지하면서, 열·행 수만큼 셀을 균등하게 축소한다.
            var localScale = new Vector3(1f / columns, 1f / rows, 1f);
            var localPosition = new Vector3(-0.5f, -0.5f, 0.01f);

            for (var i = 0; i < _layers.Length; i++)
            {
                var layer = _layers[i];
                layer.ClearAllTiles();
                layer.transform.localScale = localScale;
                layer.transform.localPosition = localPosition;
                layer.color = tint;
            }

            PaintFloor();
            PaintWalls();
            PaintEntranceDoor(hasEntranceDoor);
        }

        private void PaintFloor()
        {
            for (var row = 1; row < rows - 1; row++)
            {
                for (var column = 1; column < columns - 1; column++)
                {
                    var tile = IsAccentFloor(column, row) ? floorAccentTile : floorTile;
                    floorTilemap.SetTile(new Vector3Int(column, row, 0), tile ?? floorTile);
                }
            }
        }

        private void PaintWalls()
        {
            for (var column = 2; column < columns - 2; column++)
            {
                wallTilemap.SetTile(new Vector3Int(column, rows - 1, 0), topWallTile);
            }

            for (var column = 1; column < columns - 1; column++)
            {
                wallTilemap.SetTile(new Vector3Int(column, 0, 0), bottomWallTile);
            }

            for (var row = 1; row < rows - 1; row++)
            {
                wallTilemap.SetTile(new Vector3Int(0, row, 0), leftWallTile);
                wallTilemap.SetTile(new Vector3Int(columns - 1, row, 0), rightWallTile);
            }

            wallTilemap.SetTile(new Vector3Int(0, rows - 1, 0), topLeftWallCapTile);
            wallTilemap.SetTile(new Vector3Int(1, rows - 1, 0), topLeftCornerTile ?? topWallTile);
            wallTilemap.SetTile(new Vector3Int(columns - 2, rows - 1, 0), topRightCornerTile ?? topWallTile);
            wallTilemap.SetTile(new Vector3Int(columns - 1, rows - 1, 0), topRightWallCapTile ?? topWallTile);
            wallTilemap.SetTile(new Vector3Int(0, 0, 0), bottomLeftWallTile ?? bottomWallTile);
            wallTilemap.SetTile(new Vector3Int(columns - 1, 0, 0), bottomRightWallTile ?? bottomWallTile);
        }

        private void PaintEntranceDoor(bool hasEntranceDoor)
        {
            if (!hasEntranceDoor || entranceDoorMiddleTile == null)
                return;

            var leftColumn = columns / 2 - 1;
            PaintDoorPart(leftColumn, 0, entranceDoorBottomTile);
            PaintDoorPart(leftColumn + 1, 0, entranceDoorMiddleTile);
            PaintDoorPart(leftColumn + 2, 0, entranceDoorTopTile);
        }

        private void PaintDoorPart(int column, int row, TileBase tile)
        {
            var position = new Vector3Int(column, row, 0);
            wallTilemap.SetTile(position, null);
            doorTilemap.SetTile(position, tile ?? entranceDoorMiddleTile);
            doorTilemap.SetTransformMatrix(position, Matrix4x4.identity);
        }

        private static bool IsAccentFloor(int column, int row)
        {
            return (column == 3 && row == 3) || (column == 6 && row == 6);
        }
    }
}
