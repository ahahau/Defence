using System.IO;
using Code.Run;
using Code.UI.Run;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 전투 평면도 그림 묶음(<see cref="RunFloorArtSO"/>)을 채우고 DungeonRun의 <see cref="RunBattleDirector"/>에 잇는다.
/// 고른 칸은 2026-11-11 사용자가 채택한 목업 그대로다: ElvGames Dark Castle 바닥·벽·횃불·철문·웅덩이, StormSpell 번개.
/// 치유 샘은 붉은 웅덩이의 물만 파랗게 바꾼 사본(<see cref="SpringPath"/>)을 만들어 쓴다(팩 원본은 그대로). Unity CLI: <c>RunFloorArtBuilder.BuildAll()</c>.
/// </summary>
public static class RunFloorArtBuilder
{
    public const string AssetPath = "Assets/07.SO/Run/RunFloorArt.asset";
    public const string SpringPath = "Assets/04.Sprites/Tilesets/Generated/RunSpringPool_Blue.png";
    private const string ScenePath = "Assets/00.Scenes/DungeonRun.unity";
    private const string DarkCastle = "Assets/04.Sprites/Tilesets/ElvGames/Farm Game Assets/Tilesets/Dark Castle/";
    private const string SheetPath = DarkCastle + "Sprites/FG_Dark_Dungeon.png";
    private const string DoorPath = DarkCastle + "Animated Tiles/FG_Dark_Castle_Door01.png";
    private const string PoolPath = DarkCastle + "Animated Tiles/FG_Dark_Castle_Purple_Pool.png";
    private const string TorchPath = DarkCastle + "Animated Tiles/FG_Dark_Castle_Torch01.png";
    private const string StormPath = "Assets/04.Sprites/Effects/MagicSpell6/StormSpell.png";
    private const int Tile = 16;

    [MenuItem("Tools/Dungeon Keeper/Build Run Floor Art")]
    public static void BuildAll()
    {
        BuildAsset();
        Wire();
    }

    public static RunFloorArtSO BuildAsset()
    {
        Texture2D spring = BuildBlueSpring();
        Texture2D sheet = Load(SheetPath);
        Texture2D door = Load(DoorPath);
        Texture2D torch = Load(TorchPath);
        Texture2D storm = Load(StormPath);

        RunFloorArtSO art = AssetDatabase.LoadAssetAtPath<RunFloorArtSO>(AssetPath);
        if (art == null)
        {
            art = ScriptableObject.CreateInstance<RunFloorArtSO>();
            AssetDatabase.CreateAsset(art, AssetPath);
        }
        SerializedObject so = new(art);

        RunArtPiece[] floors = new RunArtPiece[10];
        for (int i = 0; i < 10; i++) floors[i] = Cell(sheet, i % 5, 17 + i / 5);
        Pieces(so, "Floors", floors);
        RunArtPiece[] lit = new RunArtPiece[5];
        for (int i = 0; i < 5; i++) lit[i] = Cell(sheet, i, 16);
        Pieces(so, "LitFloors", lit);
        RunArtPiece[] faces = new RunArtPiece[6];
        for (int i = 0; i < 6; i++) faces[i] = Cell(sheet, i % 3, 4 + i / 3);
        Pieces(so, "WallFaces", faces);
        RunArtPiece[] rim = new RunArtPiece[9];
        for (int i = 0; i < 9; i++) rim[i] = Cell(sheet, i % 3, 1 + i / 3);
        Pieces(so, "Rim", rim);

        RunArtPiece[] torchFrames = new RunArtPiece[8];
        for (int i = 0; i < 8; i++) torchFrames[i] = Px(torch, i * 16, 0, 16, 32);
        Pieces(so, "TorchFrames", torchFrames);
        // 북쪽 벽은 방 이름판이 덮으므로 횃불은 양옆 벽 위쪽에 단다(2026-11-11 사용자 선택 "횃불을 옆벽으로").
        Vectors(so, "TorchCells", new Vector2(-1f, 0f), new Vector2(6f, 0f));

        RunArtPiece[] water = new RunArtPiece[8];
        for (int i = 0; i < 8; i++) water[i] = Px(spring, i * 48, 48, 48, 48);
        Pieces(so, "SpringFrames", water);
        Prop(so, "SpringCell").vector2Value = new Vector2(1.5f, 1.5f);
        Prop(so, "SpringTint").colorValue = Color.white;

        Piece(Prop(so, "TrapVentIdle"), Cell(sheet, 5, 27));
        Piece(Prop(so, "TrapVentCharged"), Cell(sheet, 6, 27));
        Vectors(so, "TrapVentCells", new Vector2(1f, 1f), new Vector2(4f, 1f), new Vector2(1f, 4f), new Vector2(4f, 4f));

        RunRoomDecor[] decor =
        {
            new(RunRoomKind.Vault, Cell(sheet, 22, 29), new Vector2(1f, 1f)),
            new(RunRoomKind.Vault, Cell(sheet, 24, 29), new Vector2(2f, 1f)),
            new(RunRoomKind.Vault, Cell(sheet, 21, 29), new Vector2(4f, 1f)),
            new(RunRoomKind.Vault, Cell(sheet, 26, 30, 2, 2), new Vector2(3f, 4f)),
            new(RunRoomKind.DoorControl, Cell(sheet, 1, 28, 4, 4), new Vector2(1f, 1f)),
            new(RunRoomKind.DoorControl, Cell(sheet, 8, 28, 2, 2), new Vector2(2f, 1f)),
            new(RunRoomKind.Heart, Cell(sheet, 30, 25, 2, 3), new Vector2(2f, 1f)),
        };
        SerializedProperty decorList = Prop(so, "Decor");
        decorList.arraySize = decor.Length;
        for (int i = 0; i < decor.Length; i++)
        {
            SerializedProperty item = decorList.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("kind").enumValueIndex = (int)decor[i].Kind;
            Piece(item.FindPropertyRelative("piece"), decor[i].Piece);
            item.FindPropertyRelative("cell").vector2Value = decor[i].Cell;
        }

        Piece(Prop(so, "DoorClosed"), Px(door, 0, 0, 48, 32));
        Piece(Prop(so, "DoorOpen"), Px(door, 336, 32, 48, 32));
        Piece(Prop(so, "DoorPost"), Cell(sheet, 11, 20, 1, 4));

        RunArtPiece[] strike = new RunArtPiece[20];
        for (int i = 0; i < 20; i++) strike[i] = Px(storm, (i % 10) * 96, (i / 10) * 160, 96, 160);
        Pieces(so, "StrikeFrames", strike);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();
        return art;
    }

    public static void Wire()
    {
        RunFloorArtSO art = AssetDatabase.LoadAssetAtPath<RunFloorArtSO>(AssetPath);
        if (EditorSceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath);
        RunBattleDirector director = Object.FindAnyObjectByType<RunBattleDirector>(FindObjectsInactive.Include);
        if (director == null)
        {
            Debug.LogError("RunFloorArtBuilder: DungeonRun에 RunBattleDirector가 없습니다.");
            return;
        }
        SerializedObject so = new(director);
        so.FindProperty("floorArt").objectReferenceValue = art;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(director.gameObject.scene);
        EditorSceneManager.SaveScene(director.gameObject.scene);
    }

    /// <summary>붉은 웅덩이에서 채도 높은 붉은 물 픽셀만 푸른 물로 돌린 사본을 만든다. 테두리 돌은 채도가 낮아 그대로 남는다.</summary>
    private static Texture2D BuildBlueSpring()
    {
        Texture2D source = new(2, 2, TextureFormat.RGBA32, false);
        source.LoadImage(File.ReadAllBytes(PoolPath));
        Color32[] pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            if (p.a == 0) continue;
            Color.RGBToHSV(p, out float h, out float s, out float v);
            bool redWater = s > 0.55f && (h < 0.06f || h > 0.88f);
            if (!redWater) continue;
            Color blue = Color.HSVToRGB(0.56f, Mathf.Min(1f, s * 0.85f), Mathf.Min(1f, v * 1.15f));
            pixels[i] = new Color32((byte)(blue.r * 255f), (byte)(blue.g * 255f), (byte)(blue.b * 255f), p.a);
        }
        source.SetPixels32(pixels);
        source.Apply();
        File.WriteAllBytes(SpringPath, source.EncodeToPNG());
        Object.DestroyImmediate(source);
        AssetDatabase.ImportAsset(SpringPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpringPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Tile;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return Load(SpringPath);
    }

    private static Texture2D Load(string path)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null) Debug.LogError($"RunFloorArtBuilder: 그림이 없습니다 {path}");
        return texture;
    }

    private static RunArtPiece Cell(Texture2D texture, int column, int row, int width = 1, int height = 1) =>
        new(texture, new RectInt(column * Tile, row * Tile, width * Tile, height * Tile));

    private static RunArtPiece Px(Texture2D texture, int x, int y, int width, int height) =>
        new(texture, new RectInt(x, y, width, height));

    private static SerializedProperty Prop(SerializedObject so, string name) => so.FindProperty($"<{name}>k__BackingField");

    private static void Piece(SerializedProperty property, RunArtPiece piece)
    {
        property.FindPropertyRelative("texture").objectReferenceValue = piece.Texture;
        property.FindPropertyRelative("pixels").rectIntValue = piece.Pixels;
    }

    private static void Pieces(SerializedObject so, string name, RunArtPiece[] pieces)
    {
        SerializedProperty list = Prop(so, name);
        list.arraySize = pieces.Length;
        for (int i = 0; i < pieces.Length; i++) Piece(list.GetArrayElementAtIndex(i), pieces[i]);
    }

    private static void Vectors(SerializedObject so, string name, params Vector2[] values)
    {
        SerializedProperty list = Prop(so, name);
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) list.GetArrayElementAtIndex(i).vector2Value = values[i];
    }
}
