using System.Collections.Generic;
using Code.Buildings;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 함정 몸체를 Dark Castle 시트의 바닥 장치 조각(발판·격자·발사구·문양판·구덩이 틀)으로 만든다.
/// 함정 그림(가시·톱·그물)은 팩에 없어서, 함정마다 다른 조각을 칸 가운데에 놓은 그림을 만든다(2026-10-21 사용자 선택).
/// 그림은 바닥 타일과 같은 텍셀 크기(25.6/유닛)라 칸 하나 = 32픽셀이다. 몸체(Visual)는 1/칸 수로 줄여 두어,
/// 설치 때 칸 수만큼 늘어나도(<c>NodeTrapGrid.TryPlace</c>) 덮은 칸을 정확히 채운다.
/// 몸체를 덮던 노란 테두리(Border)는 기본 Trap 프리팹에서 지운다. 메뉴 "Tools/Dungeon Keeper/Build Trap Bodies".
/// </summary>
public static class TrapBodyBuilder
{
    private const string TrapFolder = "Assets/03.Prefabs/Buildings/Traps/";
    private const string DataFolder = "Assets/07.SO/Buildings/";
    private const string OutputFolder = "Assets/04.Sprites/Tilesets/Generated";
    private const int TilePixels = 16;
    private const int CellPixels = 32;
    private const float PixelsPerUnit = 25.6f;

    /// <summary>시트 조각(왼쪽 위 기준 열·행, 타일 폭·높이).</summary>
    private readonly struct Piece
    {
        public Piece(int column, int row, int width = 1, int height = 1)
        {
            Column = column;
            Row = row;
            Width = width;
            Height = height;
        }

        public int Column { get; }
        public int Row { get; }
        public int Width { get; }
        public int Height { get; }
    }

    /// <summary>
    /// 프리팹, 데이터, 칸마다 놓을 조각(왼쪽 → 오른쪽, 아래 → 위). 조각이 하나뿐이고 칸이 여럿이면 덮은 칸 전체 가운데에 놓는다.
    /// 기본 Trap이 먼저여야 Variant가 테두리 삭제를 물려받는다.
    /// </summary>
    private static readonly (string prefab, string data, Piece[] pieces)[] Entries =
    {
        ("Trap", "TrapBuildingData", new[] { new Piece(1, 25) }),
        ("Trap_Spike", "SpikeTrapBuildingData", new[] { new Piece(1, 27) }),
        ("Trap_Snare", "SnareTrapBuildingData", new[] { new Piece(1, 26) }),
        ("Generated/Trap_ArmorBreaker", "Generated/ArmorBreakerTrapBuildingData", new[] { new Piece(2, 25) }),
        ("Trap_Crusher", "CrusherTrapBuildingData", new[] { new Piece(12, 29, 3, 3) }),
        ("Generated/Trap_AmbushNet", "Generated/AmbushNetTrapBuildingData", new[] { new Piece(0, 27), new Piece(0, 27) }),
        ("Trap_Blade", "BladeTrapBuildingData", new[] { new Piece(2, 27), new Piece(2, 27) }),
        ("Generated/Trap_Caltrop", "Generated/CaltropTrapBuildingData", new[] { new Piece(10, 30, 1, 2) }),
        ("Generated/Trap_ExecutionSaw", "Generated/ExecutionSawTrapBuildingData",
            new[] { new Piece(6, 30, 1, 2), new Piece(7, 30, 1, 2) }),
        ("Trap_Arrow", "ArrowTrapBuildingData", new[] { new Piece(3, 27) }),
    };

    [MenuItem("Tools/Dungeon Keeper/Build Trap Bodies")]
    public static void BuildAll()
    {
        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        sheet.LoadImage(System.IO.File.ReadAllBytes(RoomLayoutBuilder.TexturePath));
        try
        {
            foreach (var entry in Entries)
            {
                var prefabPath = TrapFolder + entry.prefab + ".prefab";
                var footprint = AssetDatabase.LoadAssetAtPath<Trap>(prefabPath)?.Footprint ?? Vector2Int.one;
                var sprite = WriteBody(sheet, System.IO.Path.GetFileName(entry.prefab), footprint, entry.pieces);
                WirePrefab(prefabPath, sprite, footprint);
                WireData(DataFolder + entry.data + ".asset", sprite);
            }
        }
        finally
        {
            Object.DestroyImmediate(sheet);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"함정 몸체 {Entries.Length}종 완료");
    }

    private static Sprite WriteBody(Texture2D sheet, string name, Vector2Int footprint, IReadOnlyList<Piece> pieces)
    {
        var width = footprint.x * CellPixels;
        var height = footprint.y * CellPixels;
        var body = new Texture2D(width, height, TextureFormat.RGBA32, false);
        body.SetPixels32(new Color32[width * height]);

        if (pieces.Count == 1)
        {
            Stamp(sheet, body, pieces[0], width / 2, height / 2);
        }
        else
        {
            for (var i = 0; i < pieces.Count; i++)
            {
                var column = i % footprint.x;
                var row = i / footprint.x;
                Stamp(sheet, body, pieces[i], column * CellPixels + CellPixels / 2, row * CellPixels + CellPixels / 2);
            }
        }

        body.Apply();
        var path = $"{OutputFolder}/TrapBody_{name}.png";
        System.IO.File.WriteAllBytes(path, body.EncodeToPNG());
        Object.DestroyImmediate(body);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>조각을 (centerX, centerY)를 가운데로 놓는다. 시트는 왼쪽 위 기준이고 텍스처 픽셀은 왼쪽 아래 기준이다.</summary>
    private static void Stamp(Texture2D sheet, Texture2D body, Piece piece, int centerX, int centerY)
    {
        var pieceWidth = piece.Width * TilePixels;
        var pieceHeight = piece.Height * TilePixels;
        var sourceX = piece.Column * TilePixels;
        var sourceY = sheet.height - (piece.Row + piece.Height) * TilePixels;
        var pixels = sheet.GetPixels32();
        var left = centerX - pieceWidth / 2;
        var bottom = centerY - pieceHeight / 2;
        for (var y = 0; y < pieceHeight; y++)
        for (var x = 0; x < pieceWidth; x++)
        {
            var color = pixels[(sourceY + y) * sheet.width + sourceX + x];
            if (color.a > 0)
                body.SetPixel(left + x, bottom + y, color);
        }
    }

    private static void WirePrefab(string path, Sprite sprite, Vector2Int footprint)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == "Border")
                {
                    // Variant에서는 물려받은 자식을 지울 수 없다. 기본 Trap에서 지우면 따라온다.
                    if (!PrefabUtility.IsPartOfPrefabInstance(renderer.gameObject))
                        Object.DestroyImmediate(renderer.gameObject);
                    continue;
                }

                if (renderer.name != "Visual")
                    continue;

                renderer.sprite = sprite;
                renderer.transform.localScale = new Vector3(1f / footprint.x, 1f / footprint.y, 1f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireData(string path, Sprite sprite)
    {
        var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(path);
        if (data == null)
        {
            Debug.LogError($"{path} 함정 데이터를 찾지 못했습니다.");
            return;
        }

        var serialized = new SerializedObject(data);
        serialized.FindProperty("<BoardSprite>k__BackingField").objectReferenceValue = sprite;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
