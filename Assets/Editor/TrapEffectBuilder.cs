using System.Linq;
using Code.Buildings;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 함정 프리팹에 발동 효과(<see cref="TrapTriggerVisual"/>)를 넣는다: 불 화로 = 지옥불 폭발, 핏빛 웅덩이 = 솟는 지옥불 기둥,
/// 마력 수정 = 파란 폭발(방식 기본값: 밟기 = 베기, 투사체 = 화살). 2026-10-27: 속박 수정 = 녹색 폭발, 충격 수정 = 주황 베기,
/// 노출 수정 = 금빛 기둥, 번개 구름 = 번개 시트(<see cref="TrapBodyBuilder.StormSheetPath"/>)에서 잘라 낸 낱장 프레임.
/// 메뉴 "Tools/Dungeon Keeper/Build Trap Effects".
/// </summary>
public static class TrapEffectBuilder
{
    private const string TrapFolder = "Assets/03.Prefabs/Buildings/Traps/";
    private const string SlashFolder = "Assets/04.Sprites/Characters/SkeletonWarrior/effects/plash attack/";
    private const string BurstFolder = "Assets/04.Sprites/Characters/SkeletonWarrior/effects/plash explore/";
    private const string PillarFolder = "Assets/04.Sprites/Characters/SkeletonKing/effects/effect2/";
    private const string FlareFolder = "Assets/04.Sprites/Characters/SkeletonKing/effects/effect1/";
    private const string ArrowPath = "Assets/04.Sprites/Characters/SkeletonWarrior/arrows/arrow_origin/arrow_1_origin.png";
    private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    private const float SlashFrameSeconds = 0.06f;
    private const float BurstFrameSeconds = 0.07f;
    private const float PillarFrameSeconds = 0.1f;

    /// <summary>프리팹 이름, 효과 폴더(없으면 화살), 프레임 간격, 아래 맞춤. 기본 Trap(Variant 원본)이 먼저여야 Variant가 덮어쓴다.</summary>
    private static readonly (string prefab, string frames, float seconds, bool bottom)[] Entries =
    {
        ("Trap", SlashFolder + "origin", SlashFrameSeconds, false),
        ("Trap_FireBrazier", FlareFolder + "effect1_hellfire", SlashFrameSeconds, true),
        ("Trap_BloodPool", PillarFolder + "effect2_hellfire", PillarFrameSeconds, true),
        ("Trap_ManaCrystal", BurstFolder + "blue", BurstFrameSeconds, false),
        ("Trap_SnareCrystal", BurstFolder + "green", BurstFrameSeconds, false),
        ("Trap_ShockCrystal", SlashFolder + "orange", SlashFrameSeconds, false),
        ("Trap_ExposeCrystal", PillarFolder + "effect2_golden", PillarFrameSeconds, true),
        ("Trap_StormCloud", StormFxFolder, StormFrameSeconds, true),
    };

    private const string StormFxFolder = "Assets/04.Sprites/Tilesets/Generated/TrapFx_StormCloud";
    private const float StormFrameSeconds = 0.07f;
    private const int StormFrameWidth = 96;
    private const int StormFrameHeight = 160;

    /// <summary>번개 시트에서 번개가 내리꽂히는 칸(가로 칸, 줄). 구름이 흩어지는 뒤쪽 칸은 몸체가 남아 있어 쓰지 않는다.</summary>
    private static readonly Vector2Int[] StormStrikeCells =
    {
        new(5, 0), new(6, 0), new(7, 0), new(8, 0), new(9, 0), new(1, 1), new(2, 1), new(3, 1), new(4, 1)
    };

    /// <summary>
    /// 프레임 위쪽의 구름은 잘라 낸다(위에서부터 이 픽셀 아래만 쓴다). 몸체 구름은 바닥에 있어 연출에 구름이 하나 더 보이면
    /// 두 개처럼 읽힌다. 번개 아래 끝은 함정 칸 아래에 맞춘다(<c>anchorBottom</c>).
    /// </summary>
    private const int StormCropTop = 44;

    /// <summary>
    /// 프레임 아래에 붙이는 빈 픽셀. 범위 연출은 그림의 아래 끝을 범위 사각형 아래 끝(구름보다 한 칸 아래)에 맞추고 폭을 범위 폭
    /// (2×2 + 반경 1 = 4칸 = 96픽셀)에 맞춘다. 비워 두면 번개가 구름을 지나 한 칸(24픽셀) 더 내려가, 방 아래쪽에 깔면 벽 밖까지 뻗었다.
    /// 시트의 번개 끝 아래에 이미 5픽셀이 비어 있어 19만 더한다(<c>StormCloud_LightningEndsOnTheCloud</c>).
    /// </summary>
    private const int StormPadBottom = 19;

    [MenuItem("Tools/Dungeon Keeper/Build Trap Effects")]
    public static void BuildAll()
    {
        WriteStormFrames();
        var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
        var arrow = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowPath);
        foreach (var entry in Entries)
        {
            var frames = entry.frames != null ? LoadFrames(entry.frames) : null;
            Wire(TrapFolder + entry.prefab + ".prefab", frames, entry.seconds, entry.bottom,
                entry.frames == null ? arrow : null, material);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"함정 발동 효과 {Entries.Length}종 배선 완료");
    }

    private static void Wire(string path, Sprite[] frames, float seconds, bool bottom, Sprite projectile, Material material)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var visual = root.GetComponent<TrapTriggerVisual>() ?? root.AddComponent<TrapTriggerVisual>();
            var serialized = new SerializedObject(visual);
            var array = serialized.FindProperty("triggerFrames");
            array.arraySize = frames?.Length ?? 0;
            for (var i = 0; i < array.arraySize; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            if (seconds > 0f)
                serialized.FindProperty("frameSeconds").floatValue = seconds;
            serialized.FindProperty("anchorBottom").boolValue = bottom;
            serialized.FindProperty("projectileSprite").objectReferenceValue = projectile;
            serialized.FindProperty("effectMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var trap = new SerializedObject(root.GetComponent<Trap>());
            trap.FindProperty("triggerVisual").objectReferenceValue = visual;
            trap.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>번개 시트의 내리꽂는 칸을 낱장 PNG로 자른다(다시 돌려도 같은 파일). 점 필터·압축 없음.</summary>
    private static void WriteStormFrames()
    {
        if (!AssetDatabase.IsValidFolder(StormFxFolder))
            AssetDatabase.CreateFolder("Assets/04.Sprites/Tilesets/Generated", "TrapFx_StormCloud");

        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        sheet.LoadImage(System.IO.File.ReadAllBytes(TrapBodyBuilder.StormSheetPath));
        try
        {
            var height = StormFrameHeight - StormCropTop;
            for (var i = 0; i < StormStrikeCells.Length; i++)
            {
                var cell = StormStrikeCells[i];
                var sourceY = sheet.height - (cell.y + 1) * StormFrameHeight;
                var frame = new Texture2D(StormFrameWidth, height + StormPadBottom, TextureFormat.RGBA32, false);
                frame.SetPixels32(new Color32[StormFrameWidth * (height + StormPadBottom)]);
                frame.SetPixels(0, StormPadBottom, StormFrameWidth, height,
                    sheet.GetPixels(cell.x * StormFrameWidth, sourceY, StormFrameWidth, height));
                frame.Apply();
                var path = $"{StormFxFolder}/StormStrike_{i + 1}.png";
                System.IO.File.WriteAllBytes(path, frame.EncodeToPNG());
                Object.DestroyImmediate(frame);

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }
        finally
        {
            Object.DestroyImmediate(sheet);
        }
    }

    /// <summary>폴더의 프레임을 파일 이름 끝 번호 순으로 읽는다(1, 2, … 10).</summary>
    private static Sprite[] LoadFrames(string folder)
    {
        return AssetDatabase.FindAssets("t:Sprite", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct()
            .OrderBy(FrameNumber)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(sprite => sprite != null)
            .ToArray();
    }

    private static int FrameNumber(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        var digits = new string(name.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var number) ? number : 0;
    }
}
