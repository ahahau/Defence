using System.Linq;
using Code.Buildings;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 함정 프리팹에 발동 효과(<see cref="TrapTriggerVisual"/>)를 넣는다. 방식별로 그림을 고르고 함정마다 색을 달리한다:
/// 밟기 = 베기, 범위 = 폭발, 지속 = 솟는 불기둥, 투사체 = 화살. 메뉴 "Tools/Dungeon Keeper/Build Trap Effects".
/// </summary>
public static class TrapEffectBuilder
{
    private const string TrapFolder = "Assets/03.Prefabs/Buildings/Traps/";
    private const string SlashFolder = "Assets/04.Sprites/Characters/SkeletonWarrior/effects/plash attack/";
    private const string BurstFolder = "Assets/04.Sprites/Characters/SkeletonWarrior/effects/plash explore/";
    private const string PillarFolder = "Assets/04.Sprites/Characters/SkeletonKing/effects/effect2/";
    private const string ArrowPath = "Assets/04.Sprites/Characters/SkeletonWarrior/arrows/arrow_origin/arrow_1_origin.png";
    private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    private const float SlashFrameSeconds = 0.06f;
    private const float BurstFrameSeconds = 0.07f;
    private const float PillarFrameSeconds = 0.1f;

    /// <summary>프리팹 이름, 효과 폴더(없으면 화살), 프레임 간격, 아래 맞춤. 기본 Trap이 먼저여야 Variant가 덮어쓴다.</summary>
    private static readonly (string prefab, string frames, float seconds, bool bottom)[] Entries =
    {
        ("Trap", SlashFolder + "origin", SlashFrameSeconds, false),
        ("Trap_Spike", SlashFolder + "yellow", SlashFrameSeconds, false),
        ("Trap_Snare", SlashFolder + "green", SlashFrameSeconds, false),
        ("Generated/Trap_ArmorBreaker", SlashFolder + "blue", SlashFrameSeconds, false),
        ("Trap_Crusher", BurstFolder + "orange", BurstFrameSeconds, false),
        ("Generated/Trap_AmbushNet", BurstFolder + "green", BurstFrameSeconds, false),
        ("Trap_Blade", PillarFolder + "effect2_standard", PillarFrameSeconds, true),
        ("Generated/Trap_Caltrop", PillarFolder + "effect2_golden", PillarFrameSeconds, true),
        ("Generated/Trap_ExecutionSaw", PillarFolder + "effect2_hellfire", PillarFrameSeconds, true),
        ("Trap_Arrow", null, 0f, false),
    };

    [MenuItem("Tools/Dungeon Keeper/Build Trap Effects")]
    public static void BuildAll()
    {
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
