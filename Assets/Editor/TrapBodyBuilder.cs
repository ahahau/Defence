using System.Collections.Generic;
using Code.Buildings;
using Code.MapCreateSystem;
using Code.StatusEffects;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 팩 애니메이션 그림이 있는 함정(불 화로·핏빛 웅덩이·마력 수정)을 만든다(2026-10-21 사용자 선택 — 함정 그림이 없는 함정은 지웠다).
/// 함정마다 프레임 그림, <c>Trap.prefab</c> Variant, 데이터, 상태이상(화상)을 만들고 다시 돌려도 같은 결과가 나온다.
/// 그림은 바닥 타일과 같은 텍셀 크기(25.6/유닛)라 칸 하나 = 32픽셀이고, 모든 프레임을 같은 사각형으로 빈 여백 없이 자른 뒤
/// 기준점을 덮은 칸 가운데에 둔다. 몸체(Visual)는 1/칸 수로 줄여 두어 설치 때 칸 수만큼 늘어나도(<c>NodeTrapGrid.TryPlace</c>)
/// 텍셀 크기 그대로이고, 바닥·기둥 타일 위·캐릭터 아래에 그린다(<see cref="Trap.BodySortingOrder"/>).
/// 발동 효과는 <see cref="TrapEffectBuilder"/>가 넣는다. 메뉴 "Tools/Dungeon Keeper/Build Traps".
/// </summary>
public static class TrapBodyBuilder
{
    private const string TrapFolder = "Assets/03.Prefabs/Buildings/Traps/";
    private const string BasePrefabPath = TrapFolder + "Trap.prefab";
    private const string DataFolder = "Assets/07.SO/Buildings/";
    private const string StatusFolder = "Assets/07.SO/StatusEffects/";
    private const string OutputFolder = "Assets/04.Sprites/Tilesets/Generated";
    private const string AnimatedFolder = "Assets/04.Sprites/Tilesets/ElvGames/Farm Game Assets/Tilesets/Dark Castle/Animated Tiles/";
    private const string ObjectFolder = "Assets/04.Sprites/Tilesets/ElvGames/Farm Game Assets/Objects/";
    private const string BurnStatusPath = StatusFolder + "BurnStatusEffect.asset";
    private const int CellPixels = 32;
    private const float PixelsPerUnit = 25.6f;
    private const float FrameSeconds = 0.12f;

    /// <summary>설치된 함정의 기본 발동 확률(사용자 결정: 쉽게 밟히지 않게). 모험가 기동성이 더 깎는다.</summary>
    private const float TriggerChance = 0.6f;

    /// <summary>함정 하나. 그림은 시트의 한 줄(<c>row</c>)에 가로로 늘어선 프레임이다.</summary>
    private sealed class Spec
    {
        public string Prefab;
        public string Data;
        public string DisplayName;
        public string Texture;
        public Vector2Int FrameSize;
        public int Row;
        public int Frames;
        public Vector2Int Footprint;
        public TrapTriggerMode Mode;
        public int Cost;
        public int Danger;
        public int Grade;
        public int Damage;
        public float DamagePerDay;
        public float Cooldown;
        public float PersistentSeconds = 3f;
        public float AreaRadius = 1f;
        public string Status;
    }

    private static readonly Spec[] Specs =
    {
        new()
        {
            Prefab = "Trap_FireBrazier", Data = "FireBrazierTrapBuildingData", DisplayName = "불 화로",
            Texture = AnimatedFolder + "FG_Dark_Castle_Purple_Torch02.png", FrameSize = new Vector2Int(16, 32), Frames = 8,
            Footprint = Vector2Int.one, Mode = TrapTriggerMode.Step, Cost = 20, Danger = 1, Grade = 1,
            Damage = 6, DamagePerDay = 0.45f, Cooldown = 4f, Status = BurnStatusPath
        },
        new()
        {
            Prefab = "Trap_BloodPool", Data = "BloodPoolTrapBuildingData", DisplayName = "핏빛 웅덩이",
            Texture = AnimatedFolder + "FG_Dark_Castle_Purple_Pool.png", FrameSize = new Vector2Int(48, 48), Row = 1, Frames = 8,
            Footprint = new Vector2Int(2, 2), Mode = TrapTriggerMode.Persistent, Cost = 55, Danger = 3, Grade = 2,
            Damage = 14, DamagePerDay = 0.6f, Cooldown = 5f, Status = StatusFolder + "BleedStatusEffect.asset"
        },
        new()
        {
            Prefab = "Trap_ManaCrystal", Data = "ManaCrystalTrapBuildingData", DisplayName = "마력 수정",
            Texture = ObjectFolder + "FG_Crystal_Blue_1.png", FrameSize = new Vector2Int(16, 16), Frames = 8,
            Footprint = Vector2Int.one, Mode = TrapTriggerMode.Area, Cost = 45, Danger = 3, Grade = 2,
            Damage = 10, DamagePerDay = 0.55f, Cooldown = 6f, Status = StatusFolder + "VulnerableStatusEffect.asset"
        },
    };

    /// <summary>만드는 함정의 데이터 경로(해금 목록·씬 설치 목록 배선, 테스트가 쓴다).</summary>
    public static IEnumerable<string> DataPaths
    {
        get
        {
            foreach (var spec in Specs)
                yield return DataFolder + spec.Data + ".asset";
        }
    }

    [MenuItem("Tools/Dungeon Keeper/Build Traps")]
    public static void BuildAll()
    {
        EnsureBurnStatus();
        ShakeBodyNotRoot();
        foreach (var spec in Specs)
        {
            var frames = WriteFrames(spec);
            var prefab = BuildPrefab(spec, frames);
            BuildData(spec, frames[0], prefab);
        }

        AssetDatabase.SaveAssets();
        TrapEffectBuilder.BuildAll();
        Debug.Log($"함정 {Specs.Length}종 완료");
    }

    /// <summary>
    /// 바탕 Trap.prefab이 닳을 때 몸체(Visual)만 흔들게 한다. 비워 두면 루트를 흔드는데, 기준 위치를 노드(배율 15)에 붙기 전
    /// 월드 좌표로 잡아 첫 발동 뒤 함정이 방 밖 멀리로 튀었다. Variant는 이 값을 물려받는다.
    /// </summary>
    private static void ShakeBodyNotRoot()
    {
        var root = PrefabUtility.LoadPrefabContents(BasePrefabPath);
        try
        {
            Transform body = null;
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == "Visual")
                    body = renderer.transform;
            }

            var trap = new SerializedObject(root.GetComponent<Trap>());
            trap.FindProperty("damageAnimationTarget").objectReferenceValue = body;
            trap.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BasePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureBurnStatus()
    {
        if (AssetDatabase.LoadAssetAtPath<StatusEffectDataSO>(BurnStatusPath) != null)
            return;

        var status = ScriptableObject.CreateInstance<StatusEffectDataSO>();
        AssetDatabase.CreateAsset(status, BurnStatusPath);
        var serialized = new SerializedObject(status);
        serialized.FindProperty("displayName").stringValue = "화상";
        serialized.FindProperty("description").stringValue = "2개 방을 지날 동안 공격이 15% 느려지고 함정에게 받는 피해가 20% 늘어납니다.";
        serialized.FindProperty("durationNodeVisits").intValue = 2;
        serialized.FindProperty("attackIntervalMultiplier").floatValue = 1.15f;
        serialized.FindProperty("trapDamageTakenMultiplier").floatValue = 1.2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>프레임을 함정 칸 크기 캔버스 가운데에 놓고, 모든 프레임을 덮는 한 사각형으로 잘라 저장한다(프레임이 흔들리지 않게).</summary>
    private static Sprite[] WriteFrames(Spec spec)
    {
        var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        sheet.LoadImage(System.IO.File.ReadAllBytes(spec.Texture));
        var width = spec.Footprint.x * CellPixels;
        var height = spec.Footprint.y * CellPixels;
        var canvases = new List<Texture2D>();
        try
        {
            var crop = new RectInt();
            var hasContent = false;
            for (var i = 0; i < spec.Frames; i++)
            {
                var canvas = new Texture2D(width, height, TextureFormat.RGBA32, false);
                canvas.SetPixels32(new Color32[width * height]);
                var sourceX = i * spec.FrameSize.x;
                var sourceY = sheet.height - (spec.Row + 1) * spec.FrameSize.y;
                var left = (width - spec.FrameSize.x) / 2;
                var bottom = (height - spec.FrameSize.y) / 2;
                canvas.SetPixels(left, bottom, spec.FrameSize.x, spec.FrameSize.y,
                    sheet.GetPixels(sourceX, sourceY, spec.FrameSize.x, spec.FrameSize.y));
                canvas.Apply();
                canvases.Add(canvas);

                if (!TryOpaqueBounds(canvas, out var bounds))
                    continue;

                crop = hasContent ? Union(crop, bounds) : bounds;
                hasContent = true;
            }

            if (!hasContent)
                crop = new RectInt(0, 0, width, height);

            var pivot = new Vector2((width * 0.5f - crop.x) / crop.width, (height * 0.5f - crop.y) / crop.height);
            var sprites = new Sprite[spec.Frames];
            for (var i = 0; i < canvases.Count; i++)
            {
                var path = $"{OutputFolder}/TrapBody_{spec.Prefab}_{i}.png";
                var cropped = new Texture2D(crop.width, crop.height, TextureFormat.RGBA32, false);
                cropped.SetPixels(canvases[i].GetPixels(crop.x, crop.y, crop.width, crop.height));
                cropped.Apply();
                System.IO.File.WriteAllBytes(path, cropped.EncodeToPNG());
                Object.DestroyImmediate(cropped);
                sprites[i] = ImportFrame(path, pivot);
            }

            return sprites;
        }
        finally
        {
            Object.DestroyImmediate(sheet);
            foreach (var canvas in canvases)
                Object.DestroyImmediate(canvas);
        }
    }

    private static Sprite ImportFrame(string path, Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Trap BuildPrefab(Spec spec, Sprite[] frames)
    {
        var path = TrapFolder + spec.Prefab + ".prefab";
        GameObject root;
        var exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
        if (exists)
        {
            root = PrefabUtility.LoadPrefabContents(path);
        }
        else
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BasePrefabPath));
            root.name = spec.Prefab;
        }

        try
        {
            var trap = new SerializedObject(root.GetComponent<Trap>());
            trap.FindProperty("triggerMode").intValue = (int)spec.Mode;
            trap.FindProperty("footprint").vector2IntValue = spec.Footprint;
            trap.FindProperty("triggerChance").floatValue = TriggerChance;
            trap.FindProperty("damage").intValue = spec.Damage;
            trap.FindProperty("damagePerDay").floatValue = spec.DamagePerDay;
            trap.FindProperty("injuryChance").floatValue = 1f;
            trap.FindProperty("injuryStatusEffect").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<StatusEffectDataSO>(spec.Status);
            trap.FindProperty("cooldownSeconds").floatValue = spec.Cooldown;
            trap.FindProperty("persistentSeconds").floatValue = spec.PersistentSeconds;
            trap.FindProperty("areaRadiusCells").floatValue = spec.AreaRadius;
            trap.FindProperty("<DangerIncreaseOnTrigger>k__BackingField").intValue = spec.Danger;
            trap.ApplyModifiedPropertiesWithoutUndo();

            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name != "Visual")
                    continue;

                renderer.sprite = frames[0];
                renderer.sortingOrder = Trap.BodySortingOrder;
                renderer.transform.localScale = new Vector3(1f / spec.Footprint.x, 1f / spec.Footprint.y, 1f);
                if (!renderer.TryGetComponent<TrapBodyAnimation>(out var animation))
                    animation = renderer.gameObject.AddComponent<TrapBodyAnimation>();
                var serialized = new SerializedObject(animation);
                var array = serialized.FindProperty("frames");
                array.arraySize = frames.Length;
                for (var i = 0; i < frames.Length; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                serialized.FindProperty("frameSeconds").floatValue = FrameSeconds;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<Trap>();
        }
        finally
        {
            if (exists)
                PrefabUtility.UnloadPrefabContents(root);
            else
                Object.DestroyImmediate(root);
        }
    }

    private static void BuildData(Spec spec, Sprite board, Trap prefab)
    {
        var path = DataFolder + spec.Data + ".asset";
        var data = AssetDatabase.LoadAssetAtPath<BuildingDataSO>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<BuildingDataSO>();
            AssetDatabase.CreateAsset(data, path);
        }

        var serialized = new SerializedObject(data);
        serialized.FindProperty("<Grade>k__BackingField").intValue = spec.Grade;
        serialized.FindProperty("<BoardSprite>k__BackingField").objectReferenceValue = board;
        serialized.FindProperty("<DisplayName>k__BackingField").stringValue = spec.DisplayName;
        serialized.FindProperty("<Cost>k__BackingField").intValue = spec.Cost;
        serialized.FindProperty("<Prefab>k__BackingField").objectReferenceValue = prefab;
        serialized.FindProperty("<BaseDanger>k__BackingField").intValue = spec.Danger;
        serialized.FindProperty("<Category>k__BackingField").intValue = 2;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool TryOpaqueBounds(Texture2D texture, out RectInt bounds)
    {
        int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
        for (var y = 0; y < texture.height; y++)
        for (var x = 0; x < texture.width; x++)
        {
            if (texture.GetPixel(x, y).a <= 0f)
                continue;

            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }

        bounds = maxX < 0 ? default : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        return maxX >= 0;
    }

    private static RectInt Union(RectInt a, RectInt b)
    {
        var minX = Mathf.Min(a.xMin, b.xMin);
        var minY = Mathf.Min(a.yMin, b.yMin);
        return new RectInt(minX, minY, Mathf.Max(a.xMax, b.xMax) - minX, Mathf.Max(a.yMax, b.yMax) - minY);
    }
}
