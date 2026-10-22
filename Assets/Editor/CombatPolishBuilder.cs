using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Code.Combat;
using Code.Entities;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 전투 연출 에셋을 채운다.
/// - 애니메이션 묶음마다 팩에 있는 둘째 공격(attack2)·맞는 동작(hit)·발사체(화살·eff1·마법 구슬)·발사체 폭발 그림.
///   첫 공격 그림이 있는 폴더를 기준으로 찾는다. 임포트 설정은 그 캐릭터의 공격 그림을 따르고, 발사체만 기준점을 가운데로 둔다.
/// - 맞은 캐릭터를 흰색으로 번쩍이는 실루엣 재질(<c>Defence/Sprite Flash</c>)과 근접 베기 섬광 그림(코드로 그린 초승달).
/// - 피격·회복 파티클 재질(URP 파티클 재질 복사 + 4×4 흰 사각 그림).
/// - 둘을 전투 연출 표(<see cref="CombatFxCatalogSO"/>)에 건다.
/// Unity CLI: <c>CombatPolishBuilder.BuildAll()</c>. 돌려준 문자열이 묶음별로 채운 개수다.
/// </summary>
public static class CombatPolishBuilder
{
    public const string AnimationFolder = "Assets/07.SO/Characters/Animations";
    public const string FlashShaderPath = "Assets/05.Materials/SpriteFlash.shader";
    public const string FlashMaterialPath = "Assets/05.Materials/SpriteFlash.mat";
    public const string SlashSpritePath = "Assets/04.Sprites/Effects/MeleeSlash.png";
    public const string CatalogPath = "Assets/07.SO/Combat/CombatFxCatalog.asset";
    private const string SkeletonWarriorRoot = "Assets/04.Sprites/Characters/SkeletonWarrior";

    public const string ParticleMaterialPath = "Assets/05.Materials/CombatParticle.mat";
    public const string ParticleTexturePath = "Assets/04.Sprites/Effects/ParticleSquare.png";
    private const string UrpParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    private const int ParticleTextureSize = 4;

    private const int SlashSize = 32;
    private const float SlashPixelsPerUnit = 40f;

    [MenuItem("Tools/Dungeon Keeper/Build Combat Polish")]
    public static string BuildAll()
    {
        var report = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterAnimationSetSO", new[] { AnimationFolder }))
            report.AppendLine(FillAnimationSet(AssetDatabase.GUIDToAssetPath(guid)));

        var material = EnsureFlashMaterial();
        var slash = EnsureSlashSprite();
        var particle = EnsureParticleMaterial();
        var catalog = AssetDatabase.LoadAssetAtPath<CombatFxCatalogSO>(CatalogPath);
        if (catalog == null)
        {
            report.AppendLine($"전투 연출 표가 없습니다: {CatalogPath}");
        }
        else
        {
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("<HitFlashMaterial>k__BackingField").objectReferenceValue = material;
            serialized.FindProperty("<MeleeSlashSprite>k__BackingField").objectReferenceValue = slash;
            serialized.FindProperty("<ParticleMaterial>k__BackingField").objectReferenceValue = particle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            report.AppendLine($"catalog flash={material != null} slash={slash != null} particle={particle != null}");
        }

        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    private static string FillAnimationSet(string setPath)
    {
        var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSetSO>(setPath);
        var serialized = new SerializedObject(set);
        var attack = serialized.FindProperty("attackFrames");
        var reference = attack.arraySize > 0 ? attack.GetArrayElementAtIndex(0).objectReferenceValue as Sprite : null;
        var name = Path.GetFileNameWithoutExtension(setPath);
        if (reference == null)
            return $"{name}: 공격 그림이 없어 건너뜀";

        var referencePath = AssetDatabase.GetAssetPath(reference);
        var folder = Path.GetDirectoryName(referencePath)!.Replace('\\', '/');
        var referenceName = Path.GetFileNameWithoutExtension(referencePath);

        var hurt = FindFrames(folder, "hit");
        // 해골 왕의 attack2는 attack2_loop로 이어지는 긴 동작이라 평타와 번갈아 쓰지 않는다.
        var alt = referenceName.StartsWith("attack1_") && !File.Exists($"{folder}/attack2_loop_1.png")
            ? FindFrames(folder, "attack2")
            : new List<string>();
        ResolveProjectile(folder, out var projectile, out var impact);

        Assign(serialized, "hurtFrames", hurt, referencePath, false);
        Assign(serialized, "attackAltFrames", alt, referencePath, false);
        Assign(serialized, "projectileFrames", projectile, referencePath, true);
        Assign(serialized, "projectileImpactFrames", impact, referencePath, true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);

        return $"{name}: hurt={hurt.Count} alt={alt.Count} projectile={projectile.Count} impact={impact.Count}";
    }

    private static void ResolveProjectile(string folder, out List<string> projectile, out List<string> impact)
    {
        impact = new List<string>();
        var leaf = Path.GetFileName(folder);
        var color = leaf.Contains('_') ? leaf.Substring(leaf.IndexOf('_') + 1) : "origin";

        if (folder.Contains("/ske_archer/"))
        {
            var arrow = $"{SkeletonWarriorRoot}/arrows/arrow_{color}/arrow_1_{color}.png";
            if (!File.Exists(arrow))
                arrow = $"{SkeletonWarriorRoot}/arrows/arrow_origin/arrow_1_origin.png";
            projectile = File.Exists(arrow) ? new List<string> { arrow } : new List<string>();
            return;
        }

        if (folder.Contains("/ske_mage/"))
        {
            // 마법 구슬 팩에는 빨강이 없어 주황을 쓴다.
            var orb = color == "red" ? "orange" : color;
            if (!Directory.Exists($"{SkeletonWarriorRoot}/effects/plash attack/{orb}"))
                orb = "origin";
            projectile = FindFrames($"{SkeletonWarriorRoot}/effects/plash attack/{orb}", orb);
            impact = FindFrames($"{SkeletonWarriorRoot}/effects/plash explore/{orb}", orb);
            return;
        }

        projectile = FindFrames(folder, "eff1");
        if (projectile.Count > 0)
            return;

        // 성직자 팩은 날아가는 빛(effect)과 닿는 폭발(explosion)이 따로 있다.
        projectile = FindFrames(folder, "effect");
        impact = FindFrames(folder, "explosion");
        // 자기 발사체 그림이 없는 모험가 팩 캐릭터(오크 주술사 등)는 해골 팩의 초록 마법 구슬을 빌린다(근접은 쓰지 않는다).
        // 모험가 팩끼리만 빌린다 — 팩마다 PPU가 달라, 다른 팩이 같은 그림을 빌리면 임포트 크기가 서로 덮인다.
        if (projectile.Count > 0 || !folder.Contains("/SanctumPixel/"))
            return;

        projectile = FindFrames($"{SkeletonWarriorRoot}/effects/plash attack/green", "green");
        impact = FindFrames($"{SkeletonWarriorRoot}/effects/plash explore/green", "green");
    }

    /// <summary>폴더의 "<prefix>_번호.png"를 번호 순으로. 없으면 빈 목록.</summary>
    private static List<string> FindFrames(string folder, string prefix)
    {
        if (!Directory.Exists(folder))
            return new List<string>();

        var pattern = new Regex($"^{Regex.Escape(prefix)}_(\\d+)\\.png$");
        return Directory.GetFiles(folder, "*.png")
            .Select(path => (path: path.Replace('\\', '/'), match: pattern.Match(Path.GetFileName(path))))
            .Where(entry => entry.match.Success)
            .OrderBy(entry => int.Parse(entry.match.Groups[1].Value))
            .Select(entry => entry.path)
            .ToList();
    }

    private static void Assign(SerializedObject serialized, string field, List<string> paths, string referencePath, bool centerPivot)
    {
        var sprites = new List<Sprite>();
        foreach (var path in paths)
        {
            MatchImport(path, referencePath, centerPivot);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites.Add(sprite);
        }

        var property = serialized.FindProperty(field);
        property.arraySize = sprites.Count;
        for (var i = 0; i < sprites.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    /// <summary>
    /// 그 캐릭터의 공격 그림과 같은 크기(PPU)·필터·압축으로 맞춘다. 팩마다 PPU가 달라(28·100) 그대로 두면 크기가 어긋난다.
    /// 몸 동작은 기준점(발밑)까지 같게, 발사체는 가운데로.
    /// </summary>
    private static void MatchImport(string path, string referencePath, bool centerPivot)
    {
        var source = AssetImporter.GetAtPath(referencePath) as TextureImporter;
        var target = AssetImporter.GetAtPath(path) as TextureImporter;
        if (source == null || target == null)
            return;

        var settings = new TextureImporterSettings();
        source.ReadTextureSettings(settings);
        if (centerPivot)
        {
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
        }

        var current = new TextureImporterSettings();
        target.ReadTextureSettings(current);
        var same = target.textureType == TextureImporterType.Sprite
                   && current.spriteMode == settings.spriteMode
                   && Mathf.Approximately(current.spritePixelsPerUnit, settings.spritePixelsPerUnit)
                   && current.filterMode == settings.filterMode
                   && current.spriteAlignment == settings.spriteAlignment
                   && current.spritePivot == settings.spritePivot
                   && target.textureCompression == source.textureCompression;
        if (same)
            return;

        target.SetTextureSettings(settings);
        target.textureCompression = source.textureCompression;
        target.SaveAndReimport();
    }

    private static Material EnsureFlashMaterial()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(FlashShaderPath);
        if (shader == null)
        {
            Debug.LogError($"{nameof(CombatPolishBuilder)}: {FlashShaderPath} 셰이더를 찾지 못했습니다.");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(FlashMaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, FlashMaterialPath);
        }

        material.shader = shader;
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// 피격·회복 파티클 재질. URP 기본 파티클 재질(반투명 섞기, 색 곱하기)을 복사해 그림만 4×4 흰 사각(점 필터)으로 바꾼다 —
    /// 둥근 기본 그림은 도트 화면에서 흐릿했다. 파티클 색(노랑→빨강·초록)은 그대로 나온다.
    /// </summary>
    private static Material EnsureParticleMaterial()
    {
        if (!File.Exists(ParticleTexturePath))
        {
            UiKit.EnsureFolder(Path.GetDirectoryName(ParticleTexturePath)!.Replace('\\', '/'));
            var texture = new Texture2D(ParticleTextureSize, ParticleTextureSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[ParticleTextureSize * ParticleTextureSize];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(ParticleTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(ParticleTexturePath);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(ParticleTexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var square = AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleTexturePath);

        var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        if (material == null)
        {
            if (!AssetDatabase.CopyAsset(UrpParticleMaterialPath, ParticleMaterialPath))
            {
                Debug.LogError($"{nameof(CombatPolishBuilder)}: {UrpParticleMaterialPath}를 복사하지 못했습니다.");
                return null;
            }

            material = AssetDatabase.LoadAssetAtPath<Material>(ParticleMaterialPath);
        }

        material.SetTexture("_BaseMap", square);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", square);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// 오른쪽으로 불룩한 초승달(")" 모양)을 그린다. 휘두르는 쪽을 보게 돌려 쓴다.
    /// 가운데가 두껍고 양 끝으로 갈수록 옅어진다(도트 단계 3개).
    /// </summary>
    private static Sprite EnsureSlashSprite()
    {
        if (!File.Exists(SlashSpritePath))
        {
            UiKit.EnsureFolder(Path.GetDirectoryName(SlashSpritePath)!.Replace('\\', '/'));
            var texture = new Texture2D(SlashSize, SlashSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[SlashSize * SlashSize];
            var outer = new Vector2(6f, 15.5f);
            var inner = new Vector2(1.5f, 15.5f);
            for (var y = 0; y < SlashSize; y++)
            {
                for (var x = 0; x < SlashSize; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    var inside = Vector2.Distance(p, outer) <= 14f && Vector2.Distance(p, inner) > 13.5f && p.x >= outer.x;
                    if (!inside)
                        continue;

                    var fromMiddle = Mathf.Abs(p.y - outer.y) / 14f;
                    var alpha = fromMiddle < 0.45f ? 255 : fromMiddle < 0.75f ? 180 : 100;
                    pixels[y * SlashSize + x] = new Color32(255, 255, 255, (byte)alpha);
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(SlashSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SlashSpritePath);
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(SlashSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = SlashPixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SlashSpritePath);
    }
}
