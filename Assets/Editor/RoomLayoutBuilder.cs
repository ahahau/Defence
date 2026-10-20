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

    /// <summary>바닥 무늬 비율: 기본 24장 + 무늬 8장 = 무늬 25%.</summary>
    private const int FloorMainWeight = 24;

    private const float FloorPerlinScale = 3.7f;
    private const int F = DungeonWallRuleTile.Neighbor.Floor;
    private const int W = DungeonWallRuleTile.Neighbor.NotFloor;
    private const int This = RuleTile.TilingRuleOutput.Neighbor.This;
    private const int NotThis = RuleTile.TilingRuleOutput.Neighbor.NotThis;

    private static readonly (int column, int row) FloorCell = (6, 17);
    private static readonly (int column, int row) VoidCell = (1, 2);

    private static readonly (int column, int row)[] FloorVariantCells =
    {
        (7, 17), (8, 17), (9, 17), (10, 17), (6, 18), (7, 18), (8, 18), (9, 18)
    };

    /// <summary>예제 씬이 쓰는 조각 칸(열.행, 시트 왼쪽 위 기준 16px).</summary>
    private static readonly Dictionary<RoomWallKind, (int column, int row)> PieceCells = new()
    {
        { RoomWallKind.FaceLower, (10, 9) },
        { RoomWallKind.FaceUpper, (10, 8) },
        { RoomWallKind.FaceUpperStart, (4, 2) },
        { RoomWallKind.FaceUpperEnd, (5, 2) },
        { RoomWallKind.RailTop, (10, 7) },
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
        (RoomWallKind.FaceLower, new[] { (0, -1, F) }),
        (RoomWallKind.InnerTopLeft, new[] { (0, 1, F), (-1, 0, F) }),
        (RoomWallKind.InnerTopRight, new[] { (0, 1, F), (1, 0, F) }),
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
        var main = Find(sprites, FloorCell);
        var pool = new List<Sprite>();
        for (var i = 0; i < FloorMainWeight; i++)
            pool.Add(main);
        foreach (var cell in FloorVariantCells)
            pool.Add(Find(sprites, cell));

        tile.m_DefaultSprite = main;
        tile.m_DefaultColliderType = Tile.ColliderType.None;
        tile.m_TilingRules = new List<RuleTile.TilingRule>
        {
            new()
            {
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random,
                m_Sprites = pool.ToArray(),
                m_PerlinScale = FloorPerlinScale,
                m_ColliderType = Tile.ColliderType.None,
                m_NeighborPositions = new List<Vector3Int>(),
                m_Neighbors = new List<int>()
            }
        };
        EditorUtility.SetDirty(tile);
        return tile;
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
        EditorUtility.SetDirty(tile);
        return tile;
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
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();
        return set;
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
