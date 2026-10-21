using System.Collections.Generic;
using System.Linq;
using Code.MapCreateSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 던전 룰 타일(바닥·벽·기둥)과 룰 타일 셋, 입구·특별 방 프리팹, 방 목록, 타일 팔레트를 만들고
/// SampleScene의 <see cref="DungeonNodeManager"/>와 노드 프리팹의 <see cref="NodeTileRenderer"/>에 연결한다.
/// Unity CLI: <c>RoomLayoutBuilder.BuildAll()</c>.
///
/// 보통 방은 실행 중에 깎는다(<see cref="RoomShapeGenerator"/>). 여기서 만드는 프리팹은 입구와 손으로 만든 특별 방뿐이다.
/// 이미 있는 프리팹은 모양·문만 다시 쓰고 자식(장식)은 건드리지 않는다. 룰 타일의 조각 칸은 Dark Castle 예제 씬
/// (Dark_Castle_001)이 쓰는 칸이다.
/// </summary>
public static class RoomLayoutBuilder
{
    public const string PrefabFolder = "Assets/03.Prefabs/Map/Rooms";
    public const string CatalogPath = "Assets/07.SO/Map/RoomLayoutCatalog.asset";
    public const string TileSetPath = "Assets/07.SO/Map/RoomTileSet.asset";
    public const string RuleTileFolder = "Assets/07.SO/Map/Tiles";
    public const string FloorTilePath = RuleTileFolder + "/DungeonFloorRuleTile.asset";
    public const string WallTilePath = RuleTileFolder + "/DungeonWallRuleTile.asset";
    public const string PillarTilePath = RuleTileFolder + "/DungeonPillarRuleTile.asset";
    public const string PaletteFolder = "Assets/04.Sprites/Tilesets/Palettes";
    public const string PaletteName = "DungeonRoomPalette";
    public const string TexturePath =
        "Assets/04.Sprites/Tilesets/ElvGames/Farm Game Assets/Tilesets/Dark Castle/Sprites/FG_Dark_Dungeon_Purple.png";

    private const string NodePrefabPath = "Assets/03.Prefabs/Map/Node.prefab";
    private const string ScenePath = "Assets/00.Scenes/SampleScene.unity";

    /// <summary>바닥 무늬 비율: 기본 29장 + 금 무늬 4장 = 무늬 약 12%. 금은 원래 두 칸에 걸친 그림이라 흩어지면 끊겨 보인다.</summary>
    private const int FloorMainWeight = 29;

    private const float FloorPerlinScale = 3.7f;
    private const int F = DungeonWallRuleTile.Neighbor.Floor;
    private const int W = DungeonWallRuleTile.Neighbor.NotFloor;
    private const int This = RuleTile.TilingRuleOutput.Neighbor.This;
    private const int NotThis = RuleTile.TilingRuleOutput.Neighbor.NotThis;

    private static readonly (int column, int row) FloorCell = (6, 17);
    private static readonly (int column, int row) VoidCell = (1, 2);

    private static readonly (int column, int row)[] FloorVariantCells = { (7, 17), (8, 17), (9, 17), (10, 17) };

    /// <summary>
    /// 18행은 17행과 무늬가 같고 한 톤 어두운 그림자 바닥이다. 벽·기둥 바로 아래 줄에만 깐다
    /// (무작위로 섞으면 방 한가운데에 구멍 같은 칸이 생긴다).
    /// </summary>
    private const int FloorShadowRowOffset = 1;

    /// <summary>예제 씬이 쓰는 조각 칸(열.행, 시트 왼쪽 위 기준 16px).</summary>
    private static readonly Dictionary<RoomWallKind, (int column, int row)> PieceCells = new()
    {
        { RoomWallKind.FaceLower, (10, 9) },
        { RoomWallKind.FaceLowerStart, (4, 3) },
        { RoomWallKind.FaceLowerEnd, (5, 3) },
        { RoomWallKind.FaceUpper, (10, 8) },
        { RoomWallKind.FaceUpperStart, (4, 2) },
        { RoomWallKind.FaceUpperEnd, (5, 2) },
        { RoomWallKind.RailTop, (10, 7) },
        // 계단 모서리는 높은 쪽 옆 벽이 낮은 벽 난간 줄까지 내려온 것이다. 옆 벽 조각을 그대로 쓴다.
        { RoomWallKind.StepRailLeft, (3, 3) },
        { RoomWallKind.StepRailRight, (12, 9) },
        { RoomWallKind.RailTopStart, (4, 1) },
        { RoomWallKind.RailTopEnd, (5, 1) },
        { RoomWallKind.WallLeft, (3, 3) },
        { RoomWallKind.WallLeftBottom, (3, 4) },
        { RoomWallKind.WallLeftFaceUpper, (3, 2) },
        { RoomWallKind.WallLeftTop, (3, 1) },
        { RoomWallKind.WallRight, (12, 9) },
        { RoomWallKind.WallRightBottom, (6, 4) },
        { RoomWallKind.WallRightFaceUpper, (6, 2) },
        { RoomWallKind.WallRightTop, (6, 1) },
        { RoomWallKind.RailBottom, (10, 12) },
        { RoomWallKind.RailBottomStart, (4, 5) },
        { RoomWallKind.RailBottomEnd, (5, 5) },
        { RoomWallKind.CornerBottomLeft, (3, 5) },
        { RoomWallKind.CornerBottomRight, (6, 5) },
        { RoomWallKind.InnerTopLeft, (0, 1) },
        { RoomWallKind.InnerTopRight, (2, 1) },
        { RoomWallKind.PillarCapLeft, (7, 0) },
        { RoomWallKind.PillarCap, (8, 0) },
        { RoomWallKind.PillarCapRight, (9, 0) },
        { RoomWallKind.PillarMidLeft, (7, 1) },
        { RoomWallKind.PillarMid, (8, 1) },
        { RoomWallKind.PillarMidRight, (9, 1) },
        { RoomWallKind.PillarBaseLeft, (7, 2) },
        { RoomWallKind.PillarBase, (8, 2) },
        { RoomWallKind.PillarBaseRight, (9, 2) }
    };

    /// <summary>
    /// 벽 규칙(위에서부터 먼저 맞는 것). 이웃 칸은 (dx, dy), F = 바닥, W = 바닥 아님.
    /// 바닥 바로 위는 벽돌 면 아래 줄, 그 위 면 윗줄, 그 위 난간. 옆은 세로 난간, 아래는 가로 난간. 위쪽 벽 양 끝은 세로 난간 위쪽 조각.
    /// </summary>
    private static readonly (RoomWallKind kind, (int dx, int dy, int need)[] neighbors)[] WallRules =
    {
        // 위쪽 벽(면·난간)이 옆에도 바닥을 둔 곳(방 안 벽 덩어리·홈·복도 옆)은 옆 벽보다 앞면이 먼저 보인다.
        // 옆 벽 조각은 속이 검어 검은 세로 줄로 보였다. 가장자리가 있는 면·난간 조각을 둔다.
        (RoomWallKind.FaceLowerStart, new[] { (0, -1, F), (-1, 0, F) }),
        (RoomWallKind.FaceLowerEnd, new[] { (0, -1, F), (1, 0, F) }),
        (RoomWallKind.FaceLower, new[] { (0, -1, F) }),
        (RoomWallKind.InnerTopLeft, new[] { (0, 1, F), (-1, 0, F) }),
        (RoomWallKind.InnerTopRight, new[] { (0, 1, F), (1, 0, F) }),
        (RoomWallKind.FaceUpperStart, new[] { (0, -2, F), (0, -1, W), (-1, 0, F) }),
        (RoomWallKind.FaceUpperEnd, new[] { (0, -2, F), (0, -1, W), (1, 0, F) }),
        (RoomWallKind.RailTopStart, new[] { (0, -3, F), (0, -1, W), (0, -2, W), (-1, 0, F) }),
        (RoomWallKind.RailTopEnd, new[] { (0, -3, F), (0, -1, W), (0, -2, W), (1, 0, F) }),
        (RoomWallKind.WallLeft, new[] { (1, 0, F), (1, -1, F) }),
        (RoomWallKind.WallLeftBottom, new[] { (1, 0, F) }),
        (RoomWallKind.WallRight, new[] { (-1, 0, F), (-1, -1, F) }),
        (RoomWallKind.WallRightBottom, new[] { (-1, 0, F) }),
        (RoomWallKind.RailBottomStart, new[] { (0, 1, F), (-1, 1, W) }),
        (RoomWallKind.RailBottomEnd, new[] { (0, 1, F), (1, 1, W) }),
        (RoomWallKind.RailBottom, new[] { (0, 1, F) }),
        (RoomWallKind.FaceUpperStart, new[] { (0, -2, F), (-1, -2, W) }),
        (RoomWallKind.FaceUpperEnd, new[] { (0, -2, F), (1, -2, W) }),
        (RoomWallKind.FaceUpper, new[] { (0, -2, F) }),
        // 계단 모서리: 낮은 벽 난간 줄인데 바로 옆 아래가 높은 쪽 바닥이면 난간이 위로 꺾인다.
        (RoomWallKind.StepRailLeft, new[] { (0, -3, F), (0, -1, W), (1, -1, F), (1, 0, W) }),
        (RoomWallKind.StepRailRight, new[] { (0, -3, F), (0, -1, W), (-1, -1, F), (-1, 0, W) }),
        (RoomWallKind.RailTopStart, new[] { (0, -3, F), (-1, -3, W) }),
        (RoomWallKind.RailTopEnd, new[] { (0, -3, F), (1, -3, W) }),
        (RoomWallKind.RailTop, new[] { (0, -3, F) }),
        (RoomWallKind.WallLeft, new[] { (1, -1, F) }),
        (RoomWallKind.WallLeftFaceUpper, new[] { (1, -2, F) }),
        (RoomWallKind.WallLeftTop, new[] { (1, -3, F) }),
        (RoomWallKind.WallRight, new[] { (-1, -1, F) }),
        (RoomWallKind.WallRightFaceUpper, new[] { (-1, -2, F) }),
        (RoomWallKind.WallRightTop, new[] { (-1, -3, F) }),
        (RoomWallKind.CornerBottomLeft, new[] { (1, 1, F) }),
        (RoomWallKind.CornerBottomRight, new[] { (-1, 1, F) })
    };

    /// <summary>기둥 규칙. 기둥 블록의 맨 윗줄은 머리, 맨 아랫줄은 받침, 양 끝은 왼쪽·오른쪽 조각.</summary>
    private static readonly (RoomWallKind kind, (int dx, int dy, int need)[] neighbors)[] PillarRules =
    {
        (RoomWallKind.PillarCapLeft, new[] { (0, 1, NotThis), (-1, 0, NotThis) }),
        (RoomWallKind.PillarCapRight, new[] { (0, 1, NotThis), (1, 0, NotThis) }),
        (RoomWallKind.PillarCap, new[] { (0, 1, NotThis) }),
        (RoomWallKind.PillarBaseLeft, new[] { (0, -1, NotThis), (-1, 0, NotThis) }),
        (RoomWallKind.PillarBaseRight, new[] { (0, -1, NotThis), (1, 0, NotThis) }),
        (RoomWallKind.PillarBase, new[] { (0, -1, NotThis) }),
        (RoomWallKind.PillarMidLeft, new[] { (-1, 0, NotThis) }),
        (RoomWallKind.PillarMidRight, new[] { (1, 0, NotThis) })
    };

    /// <summary>손으로 만든 특별 방(칸 하나를 2×2로 늘려 쓴다). 어느 부모 쪽 문에도 맞도록 네 변 모두 문이 있다.</summary>
    private static readonly (string name, RoomSize size, string[] rows)[] SpecialRooms =
    {
        ("Room_Special_S_Crypt", RoomSize.Small, new[]
        {
            "#...#",
            ".....",
            ".....",
            "...#.",
            "#...#"
        }),
        ("Room_Special_M_PillarHall", RoomSize.Medium, new[]
        {
            "......",
            ".#..#.",
            "......",
            "......",
            ".#..#.",
            "......"
        }),
        ("Room_Special_L_Colonnade", RoomSize.Large, new[]
        {
            ".........",
            ".##...##.",
            ".##...##.",
            ".........",
            ".........",
            ".##...##.",
            ".##...##.",
            "........."
        })
    };

    private const RoomDoors AllDoors = RoomDoors.Up | RoomDoors.Right | RoomDoors.Down | RoomDoors.Left;

    /// <summary>
    /// 칸마다 2×2로 늘린다. 방을 두 배로 키우면서(노드 간격 30u) 손으로 그린 특별 방도 같은 비율로 키우고,
    /// 기둥이 2×2 덩어리라 큰 방에서도 기둥답게 보인다.
    /// </summary>
    private static string[] Doubled(string[] rows)
    {
        var result = new string[rows.Length * 2];
        for (var i = 0; i < rows.Length; i++)
        {
            var builder = new System.Text.StringBuilder(rows[i].Length * 2);
            foreach (var c in rows[i])
                builder.Append(c).Append(c);
            result[i * 2] = builder.ToString();
            result[i * 2 + 1] = result[i * 2];
        }

        return result;
    }

    [MenuItem("Tools/Dungeon Keeper/Build Room Layouts")]
    public static void BuildAll()
    {
        EnsureFolder(PrefabFolder);
        EnsureFolder(RuleTileFolder);
        EnsureFolder(PaletteFolder);

        var sprites = LoadSprites();
        var floor = BuildFloorTile(sprites);
        var wall = BuildWallTile(sprites);
        var pillar = BuildPillarTile(sprites);
        var tileSet = BuildTileSet(floor, wall, pillar);
        WireNodePrefab(tileSet);
        BuildPalette(floor, wall, pillar);

        var built = new HashSet<string>();
        var entranceShape = RoomShapeGenerator.Generate(RoomSize.Medium, AllDoors, true, 9001);
        var entrance = SavePrefab("Room_Entrance", entranceShape, built);

        var specials = new List<RoomLayout>();
        foreach (var (name, size, rows) in SpecialRooms)
        {
            var shape = new RoomShape(name, size, false, AllDoors, Doubled(rows));
            if (!RoomLayoutRules.Validate(shape, out var problem))
            {
                Debug.LogError($"특별 방을 만들지 않았습니다: {problem}");
                continue;
            }

            specials.Add(SavePrefab(name, shape, built));
        }

        RemoveStalePrefabs(built);

        var catalog = AssetDatabase.LoadAssetAtPath<RoomLayoutCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<RoomLayoutCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        catalog.EditorSet(entrance, specials);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        WireScene(catalog);
        Debug.Log($"룰 타일 3종·타일 셋·팔레트와 입구 + 특별 방 {specials.Count}개를 만들었습니다.");
    }

    private static Dictionary<(int, int), Sprite> LoadSprites()
    {
        var sprites = new Dictionary<(int, int), Sprite>();
        foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(TexturePath).OfType<Sprite>())
        {
            var column = Mathf.RoundToInt(sprite.rect.x / 16f);
            var row = Mathf.RoundToInt((sprite.texture.height - sprite.rect.y - sprite.rect.height) / 16f);
            sprites[(column, row)] = sprite;
        }

        return sprites;
    }

    private static Sprite Find(Dictionary<(int, int), Sprite> sprites, (int column, int row) cell)
    {
        if (sprites.TryGetValue(cell, out var sprite))
            return sprite;

        Debug.LogError($"Purple 시트 {cell.column:00}.{cell.row:00} 칸의 스프라이트를 찾지 못했습니다.");
        return null;
    }

    private static DungeonFloorRuleTile BuildFloorTile(Dictionary<(int, int), Sprite> sprites)
    {
        var tile = LoadOrCreate<DungeonFloorRuleTile>(FloorTilePath);
        tile.m_DefaultSprite = Find(sprites, FloorCell);
        tile.m_DefaultColliderType = Tile.ColliderType.None;
        tile.m_TilingRules = new List<RuleTile.TilingRule>
        {
            // 위가 바닥이 아니면(벽·기둥) 그림자 바닥. 규칙은 위에서부터 맞춰 보므로 먼저 둔다.
            FloorRule(sprites, FloorShadowRowOffset, new List<Vector3Int> { Vector3Int.up }, new List<int> { NotThis }),
            FloorRule(sprites, 0, new List<Vector3Int>(), new List<int>())
        };
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static RuleTile.TilingRule FloorRule(Dictionary<(int, int), Sprite> sprites, int rowOffset,
        List<Vector3Int> neighborPositions, List<int> neighbors)
    {
        var main = Find(sprites, (FloorCell.column, FloorCell.row + rowOffset));
        var pool = new List<Sprite>();
        for (var i = 0; i < FloorMainWeight; i++)
            pool.Add(main);
        foreach (var cell in FloorVariantCells)
            pool.Add(Find(sprites, (cell.column, cell.row + rowOffset)));

        return new RuleTile.TilingRule
        {
            m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random,
            m_Sprites = pool.ToArray(),
            m_PerlinScale = FloorPerlinScale,
            m_ColliderType = Tile.ColliderType.None,
            m_NeighborPositions = neighborPositions,
            m_Neighbors = neighbors
        };
    }

    private static DungeonWallRuleTile BuildWallTile(Dictionary<(int, int), Sprite> sprites)
    {
        var tile = LoadOrCreate<DungeonWallRuleTile>(WallTilePath);
        tile.m_DefaultSprite = Find(sprites, VoidCell);
        tile.m_DefaultColliderType = Tile.ColliderType.None;
        tile.m_TilingRules = BuildRules(sprites, WallRules);
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static RuleTile BuildPillarTile(Dictionary<(int, int), Sprite> sprites)
    {
        var tile = LoadOrCreate<RuleTile>(PillarTilePath);
        tile.m_DefaultSprite = Find(sprites, PieceCells[RoomWallKind.PillarMid]);
        tile.m_DefaultColliderType = Tile.ColliderType.None;
        tile.m_TilingRules = BuildRules(sprites, PillarRules);
        // 시트의 기둥 머리는 윗면이 검게 비어 있다(어둠 아래 붙여 쓰는 조각). 방 한가운데에 서면 구멍처럼 보여 반쪽 기둥으로 읽혀서,
        // 윗면을 돌 색으로 채운 머리 그림을 만들어 쓴다.
        for (var i = 0; i < PillarRules.Length; i++)
        {
            var kind = PillarRules[i].kind;
            if (kind is RoomWallKind.PillarCapLeft or RoomWallKind.PillarCap or RoomWallKind.PillarCapRight)
                tile.m_TilingRules[i].m_Sprites = new[] { SolidPillarCap(kind, Find(sprites, PieceCells[kind])) };
        }

        EditorUtility.SetDirty(tile);
        return tile;
    }

    /// <summary>바닥·벽·기둥 룰 타일만 다시 만든다(방 프리팹·씬은 건드리지 않음). 메뉴: Tools/Dungeon Keeper/Rebuild Wall Tiles.</summary>
    [MenuItem("Tools/Dungeon Keeper/Rebuild Wall Tiles")]
    public static void RebuildRuleTiles()
    {
        var sprites = LoadSprites();
        BuildFloorTile(sprites);
        BuildWallTile(sprites);
        BuildPillarTile(sprites);
        AssetDatabase.SaveAssets();
    }

    private const string GeneratedTileSpriteFolder = "Assets/04.Sprites/Tilesets/Generated";

    /// <summary>기둥 머리의 검은 윗면(시트 색 19090E)을 돌 바탕색(5C283C)으로 채운 그림. 테두리 무늬는 그대로다.</summary>
    private static Sprite SolidPillarCap(RoomWallKind kind, Sprite source)
    {
        if (source == null)
            return null;

        var path = $"{GeneratedTileSpriteFolder}/{kind}.png";
        if (!System.IO.File.Exists(path))
        {
            EnsureFolder(GeneratedTileSpriteFolder);
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sheet.LoadImage(System.IO.File.ReadAllBytes(TexturePath));
            var rect = source.rect;
            var cap = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
            var hollow = new Color32(0x19, 0x09, 0x0E, 0xFF);
            var stone = new Color32(0x5C, 0x28, 0x3C, 0xFF);
            for (var y = 0; y < cap.height; y++)
            for (var x = 0; x < cap.width; x++)
            {
                Color32 c = sheet.GetPixel((int)rect.x + x, (int)rect.y + y);
                cap.SetPixel(x, y, c.r == hollow.r && c.g == hollow.g && c.b == hollow.b && c.a > 0 ? stone : c);
            }

            cap.Apply();
            System.IO.File.WriteAllBytes(path, cap.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Object.DestroyImmediate(cap);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point || !Mathf.Approximately(importer.spritePixelsPerUnit, 16f))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static List<RuleTile.TilingRule> BuildRules(Dictionary<(int, int), Sprite> sprites,
        (RoomWallKind kind, (int dx, int dy, int need)[] neighbors)[] rules)
    {
        var result = new List<RuleTile.TilingRule>();
        for (var i = 0; i < rules.Length; i++)
        {
            var (kind, neighbors) = rules[i];
            result.Add(new RuleTile.TilingRule
            {
                m_Id = i,
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
                m_Sprites = new[] { Find(sprites, PieceCells[kind]) },
                m_ColliderType = Tile.ColliderType.None,
                m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_NeighborPositions = neighbors.Select(n => new Vector3Int(n.dx, n.dy, 0)).ToList(),
                m_Neighbors = neighbors.Select(n => n.need).ToList()
            });
        }

        return result;
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static RoomTileSet BuildTileSet(DungeonFloorRuleTile floor, DungeonWallRuleTile wall, RuleTile pillar)
    {
        var set = LoadOrCreate<RoomTileSet>(TileSetPath);
        set.EditorSet(floor, wall, pillar);
        set.EditorSetDoorFrames(LoadDoorFrames());
        BuildDecor(set);
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        return set;
    }

    private const string DecorFolder = RuleTileFolder + "/Decor";
    private const string TorchSheetPath =
        "Assets/04.Sprites/Tilesets/ElvGames/Farm Game Assets/Tilesets/Dark Castle/Animated Tiles/FG_Dark_Castle_Purple_Torch01.png";

    /// <summary>
    /// 위쪽 벽 가고일(2×2): 시트 위 칸 (열, 위 행). 아래 행은 위 행 + 1. 열 3·4 가고일만 쓴다 —
    /// 열 0~2 가고일은 날개가 세 칸에 걸치고 받침이 한 칸뿐이라 2×2에 맞지 않는다.
    /// </summary>
    private static readonly (int column, int topRow)[] WallLargeCells =
    {
        (3, 19), (3, 21), (3, 23)
    };

    /// <summary>위쪽 벽 작은 장식: 보옥 홈·촛대.</summary>
    private static readonly (int column, int row)[] WallSmallCells =
    {
        (1, 25), (2, 25), (3, 25), (5, 28), (5, 29), (5, 30), (5, 31)
    };

    /// <summary>바닥 소품: 뼈·해골·핏자국·항아리. 검은 잔해(22·23.30)는 어두운 방에서 바닥 구멍처럼 보여 뺐다.</summary>
    private static readonly (int column, int row)[] FloorDecorCells =
    {
        (16, 29), (17, 29), (18, 29), (19, 29), (20, 29), (15, 29),
        (16, 30), (18, 30), (20, 30), (25, 29), (25, 30)
    };

    /// <summary>횃불 불꽃 넘기는 빠르기(초당 장 수).</summary>
    private const float TorchFramesPerSecond = 8f;

    /// <summary>장식 타일·횃불 애니메이션 타일을 만들어 타일 셋에 넣는다.</summary>
    private static void BuildDecor(RoomTileSet set)
    {
        EnsureFolder(DecorFolder);
        var sprites = LoadSprites();

        var large = new List<RoomDecorPiece>();
        foreach (var (column, topRow) in WallLargeCells)
        {
            large.Add(new RoomDecorPiece(new[]
            {
                DecorTile(sprites, (column, topRow + 1)), DecorTile(sprites, (column + 1, topRow + 1)),
                DecorTile(sprites, (column, topRow)), DecorTile(sprites, (column + 1, topRow))
            }));
        }

        var small = WallSmallCells.Select(cell => (TileBase)DecorTile(sprites, cell)).ToArray();
        var floor = FloorDecorCells.Select(cell => (TileBase)DecorTile(sprites, cell)).ToArray();

        var torch = LoadOrCreate<AnimatedTile>($"{DecorFolder}/Decor_Torch.asset");
        var frames = AssetDatabase.LoadAllAssetsAtPath(TorchSheetPath).OfType<Sprite>().ToList();
        frames.Sort((a, b) => FrameNumber(a.name).CompareTo(FrameNumber(b.name)));
        torch.m_AnimatedSprites = frames.ToArray();
        torch.m_MinSpeed = TorchFramesPerSecond;
        torch.m_MaxSpeed = TorchFramesPerSecond;
        torch.m_TileColliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(torch);

        set.EditorSetDecor(torch, large.ToArray(), small, floor);
    }

    private static Tile DecorTile(Dictionary<(int, int), Sprite> sprites, (int column, int row) cell)
    {
        // 새 에셋은 값을 채운 뒤에 만든다. 만든 직후 고친 값은 첫 실행에서 저장되지 않은 일이 있었다.
        var path = $"{DecorFolder}/Decor_{cell.column:00}_{cell.row:00}.asset";
        var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
        var created = tile == null;
        if (created)
            tile = ScriptableObject.CreateInstance<Tile>();

        tile.sprite = Find(sprites, cell);
        tile.colliderType = Tile.ColliderType.None;
        if (created)
            AssetDatabase.CreateAsset(tile, path);
        else
            EditorUtility.SetDirty(tile);
        return tile;
    }

    private const string DoorSheetPath =
        "Assets/04.Sprites/Tilesets/ElvGames/Farm Game Assets/Tilesets/Dark Castle/Animated Tiles/FG_Dark_Castle_Purple_Door01.png";

    /// <summary>위쪽 벽 문 애니메이션 16장(닫힘 → 열림), 시트 이름 번호순.</summary>
    private static Sprite[] LoadDoorFrames()
    {
        var frames = new List<Sprite>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(DoorSheetPath))
        {
            if (asset is Sprite sprite)
                frames.Add(sprite);
        }

        frames.Sort((a, b) => FrameNumber(a.name).CompareTo(FrameNumber(b.name)));
        if (frames.Count == 0)
            Debug.LogError($"문 그림을 찾지 못했습니다: {DoorSheetPath}");
        return frames.ToArray();
    }

    private static int FrameNumber(string spriteName)
    {
        var underscore = spriteName.LastIndexOf('_');
        return underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out var number) ? number : 0;
    }

    /// <summary>타일 팔레트에 룰 타일 세 장을 올려 둔다. 프리팹·씬을 손으로 칠할 때 쓴다.</summary>
    private static void BuildPalette(TileBase floor, TileBase wall, TileBase pillar)
    {
        var path = $"{PaletteFolder}/{PaletteName}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            GridPaletteUtility.CreateNewPalette(PaletteFolder, PaletteName, GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var tilemap = root.GetComponentInChildren<Tilemap>();
            if (tilemap == null)
                return;

            tilemap.ClearAllTiles();
            tilemap.SetTile(new Vector3Int(0, 0, 0), floor);
            tilemap.SetTile(new Vector3Int(1, 0, 0), wall);
            tilemap.SetTile(new Vector3Int(2, 0, 0), pillar);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static RoomLayout SavePrefab(string name, RoomShape shape, HashSet<string> built)
    {
        built.Add(name);
        var path = $"{PrefabFolder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var root = existing != null ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
        var layout = root.GetComponent<RoomLayout>();
        if (layout == null)
            layout = root.AddComponent<RoomLayout>();
        layout.Configure(name, shape.Size, shape.IsEntrance, shape.Doors, shape.CopyLines());

        PrefabUtility.SaveAsPrefabAsset(root, path);
        if (existing != null)
            PrefabUtility.UnloadPrefabContents(root);
        else
            Object.DestroyImmediate(root);

        return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<RoomLayout>();
    }

    /// <summary>이 생성기가 예전에 만든 방 중 이번에 만들지 않은 것을 지운다(폴더 안 RoomLayout 프리팹만).</summary>
    private static void RemoveStalePrefabs(HashSet<string> built)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponent<RoomLayout>() == null || built.Contains(prefab.name))
                continue;

            AssetDatabase.DeleteAsset(path);
        }
    }

    private static void WireNodePrefab(RoomTileSet tileSet)
    {
        var root = PrefabUtility.LoadPrefabContents(NodePrefabPath);
        try
        {
            var renderer = root.GetComponentInChildren<NodeTileRenderer>(true);
            if (renderer == null)
            {
                Debug.LogError("노드 프리팹에서 NodeTileRenderer를 찾지 못했습니다.");
                return;
            }

            var serialized = new SerializedObject(renderer);
            var property = serialized.FindProperty("tileSet");
            if (property.objectReferenceValue == tileSet)
                return;

            property.objectReferenceValue = tileSet;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, NodePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireScene(RoomLayoutCatalog catalog)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            Debug.LogWarning($"{ScenePath}가 열려 있지 않아 DungeonNodeManager 연결을 건너뜁니다.");
            return;
        }

        var manager = Object.FindAnyObjectByType<DungeonNodeManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogError("씬에서 DungeonNodeManager를 찾지 못했습니다.");
            return;
        }

        var wasDirty = scene.isDirty;
        var changed = false;
        var serialized = new SerializedObject(manager);
        var property = serialized.FindProperty("layoutCatalog");
        if (property.objectReferenceValue != catalog)
        {
            property.objectReferenceValue = catalog;
            changed = true;
        }

        changed |= SetFloat(serialized, "gridSpacing", GridSpacing);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var edges = Object.FindAnyObjectByType<DungeonEdgeManager>(FindObjectsInactive.Include);
        if (edges != null)
        {
            var edgeSerialized = new SerializedObject(edges);
            changed |= SetFloat(edgeSerialized, "gridSpacing", GridSpacing);
            edgeSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 방이 두 배라 화면도 두 배를 본다. 카메라 크기는 PixelPerfectCamera가 기준 해상도로 정하므로 그쪽을 바꾼다.
        var camera = Camera.main;
        var pixelPerfect = camera != null
            ? camera.GetComponent<UnityEngine.Rendering.Universal.PixelPerfectCamera>()
            : null;
        if (pixelPerfect != null)
        {
            var cameraSerialized = new SerializedObject(pixelPerfect);
            changed |= SetInt(cameraSerialized, "m_RefResolutionX", RefResolutionX);
            changed |= SetInt(cameraSerialized, "m_RefResolutionY", RefResolutionY);
            cameraSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 던전은 어둡게: 방 빛·횃불 빛(RoomLighting)이 비추는 곳만 보인다.
        foreach (var light in Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(FindObjectsInactive.Include))
        {
            if (light.lightType != UnityEngine.Rendering.Universal.Light2D.LightType.Global)
                continue;

            var lightSerialized = new SerializedObject(light);
            changed |= SetFloat(lightSerialized, "m_Intensity", GlobalLightIntensity);
            var color = lightSerialized.FindProperty("m_Color");
            if (color != null && color.colorValue != GlobalLightColor)
            {
                color.colorValue = GlobalLightColor;
                changed = true;
            }

            lightSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        if (!changed)
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        if (wasDirty)
        {
            Debug.LogWarning("씬에 저장하지 않은 다른 변경이 있어 저장은 하지 않았습니다. 확인 후 저장하세요.");
            return;
        }

        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>방 사이 간격(월드). 방 틀 최대 18×16칸(타일 36×32)에 벽·복도가 들어간다.</summary>
    private const float GridSpacing = 30f;

    /// <summary>PixelPerfectCamera 기준 해상도. 640×360(방 간격 15u 시절)의 두 배라 화면에 담기는 넓이도 두 배다.</summary>
    private const int RefResolutionX = 1280;
    private const int RefResolutionY = 720;

    /// <summary>던전 전체 빛(예전 1.05). 방 빛·횃불 빛이 그 위에 더해진다.</summary>
    private const float GlobalLightIntensity = 0.35f;
    private static readonly Color GlobalLightColor = new(0.55f, 0.6f, 0.85f, 1f);

    private static bool SetInt(SerializedObject serialized, string name, int value)
    {
        var property = serialized.FindProperty(name);
        if (property == null || property.intValue == value)
            return false;

        property.intValue = value;
        return true;
    }

    private static bool SetFloat(SerializedObject serialized, string name, float value)
    {
        var property = serialized.FindProperty(name);
        if (property == null || Mathf.Approximately(property.floatValue, value))
            return false;

        property.floatValue = value;
        return true;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
