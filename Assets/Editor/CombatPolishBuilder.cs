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
/// - 맞은 캐릭터를 흰색으로 번쩍이는 실루엣 재질(<c>Defence/Sprite Flash</c>).
/// - 피격·회복 파티클 재질(URP 파티클 재질 복사 + 4×4 흰 사각 그림).
/// - 사망 그림: 부하 = 해골 왕 팩 청록 혼불(effect2_standard), 모험가 = 해골 전사 팩 주황 폭발(plash explore/orange).
/// - 이펙트 팩(Effect_Vol.2·Magic Spell Effects 6): 그림을 <c>Assets/04.Sprites/Effects</c>로 옮기고 칸 단위로 잘라
///   근접 타격 불꽃(07)·치명타 고리(01)·마법 탄(05→06), 스킬마다 주문 그림(<see cref="SkillSpells"/>)에 건다.
/// - 위 것을 전투 연출 표(<see cref="CombatFxCatalogSO"/>)·애니메이션 묶음·스킬 데이터에 건다.
/// Unity CLI: <c>CombatPolishBuilder.BuildAll()</c>. 돌려준 문자열이 채운 개수다.
/// </summary>
public static class CombatPolishBuilder
{
    public const string AnimationFolder = "Assets/07.SO/Characters/Animations";
    public const string FlashShaderPath = "Assets/05.Materials/SpriteFlash.shader";
    public const string FlashMaterialPath = "Assets/05.Materials/SpriteFlash.mat";
    public const string SkillFolder = "Assets/07.SO/Skills";
    public const string EffectArtRoot = "Assets/04.Sprites/Effects";
    public const string Vol2Folder = EffectArtRoot + "/Vol2";
    public const string SpellFolder = EffectArtRoot + "/MagicSpell6";

    /// <summary>캐릭터 그림(PPU 28, 크기 1)과 같은 도트 밀도로 이펙트를 들인다.</summary>
    private const float EffectPixelsPerUnit = 28f;

    /// <summary>팩이 들어온 자리 → 옮길 자리. 데모 씬·스크립트·미리보기는 옮기지 않는다(게임에 쓰지 않음).</summary>
    private static readonly (string from, string to)[] PackArt =
    {
        ("Assets/Effect_Vol.2/Effect/Effect01.png", Vol2Folder + "/Effect01.png"),
        ("Assets/Effect_Vol.2/Effect/Effect05.png", Vol2Folder + "/Effect05.png"),
        ("Assets/Effect_Vol.2/Effect/Effect06.png", Vol2Folder + "/Effect06.png"),
        ("Assets/Effect_Vol.2/Effect/Effect07.png", Vol2Folder + "/Effect07.png"),
        ("Assets/Magic Spell Effects 6/Sprite Sheet/4_magic_balls_explosion-Sheet.png", SpellFolder + "/MagicBallsExplosion.png"),
        ("Assets/Magic Spell Effects 6/Sprite Sheet/Dark_spirit-Sheet.png", SpellFolder + "/DarkSpirit.png"),
        ("Assets/Magic Spell Effects 6/Sprite Sheet/Energy_beam-Sheet.png", SpellFolder + "/EnergyBeam.png"),
        ("Assets/Magic Spell Effects 6/Sprite Sheet/Kinetic_shield_strike-Sheet.png", SpellFolder + "/KineticShieldStrike.png"),
        ("Assets/Magic Spell Effects 6/Sprite Sheet/Lightning_bomb-Sheet.png", SpellFolder + "/LightningBomb.png"),
        ("Assets/Magic Spell Effects 6/Sprite Sheet/Storm_spell-Sheet.png", SpellFolder + "/StormSpell.png"),
    };

    /// <summary>시트 한 칸의 크기(픽셀)와 기준점. 칸 크기는 팩의 미리보기 GIF(3배)와 투명 열로 확인했다.</summary>
    private static readonly Dictionary<string, (int width, int height, SpriteAlignment pivot)> SheetCells = new()
    {
        { Vol2Folder + "/Effect01.png", (60, 60, SpriteAlignment.Center) },
        { Vol2Folder + "/Effect05.png", (80, 48, SpriteAlignment.Center) },
        { Vol2Folder + "/Effect06.png", (32, 32, SpriteAlignment.Center) },
        { Vol2Folder + "/Effect07.png", (48, 48, SpriteAlignment.Center) },
        { SpellFolder + "/MagicBallsExplosion.png", (160, 160, SpriteAlignment.BottomCenter) },
        { SpellFolder + "/DarkSpirit.png", (128, 208, SpriteAlignment.BottomCenter) },
        { SpellFolder + "/EnergyBeam.png", (96, 168, SpriteAlignment.BottomCenter) },
        { SpellFolder + "/KineticShieldStrike.png", (160, 96, SpriteAlignment.BottomCenter) },
        { SpellFolder + "/LightningBomb.png", (176, 160, SpriteAlignment.BottomCenter) },
        { SpellFolder + "/StormSpell.png", (96, 160, SpriteAlignment.BottomCenter) },
    };

    /// <summary>
    /// 스킬 → 주문 그림·틀 자리·배율. 충격파는 시전자 둘레(효과 반경 지름에 맞춘 배율), 나머지는 대상 발밑.
    /// 회복 스킬은 팩에 맞는 그림이 없어 시전 고리만 쓴다.
    /// </summary>
    public static readonly (string skill, string sheet, Code.Skills.SkillVisualAnchor anchor, float scale)[] SkillSpells =
    {
        ("Skill_Unit_Vanguard_ShieldBash", "KineticShieldStrike", Code.Skills.SkillVisualAnchor.Caster, 0.8f),
        ("Ultimate_Unit_Vanguard_Breakthrough", "KineticShieldStrike", Code.Skills.SkillVisualAnchor.Caster, 1.1f),
        ("Skill_Enemy_Sword_ShieldPush", "KineticShieldStrike", Code.Skills.SkillVisualAnchor.Caster, 0.65f),
        ("Ultimate_Enemy_Sword_KnightCharge", "KineticShieldStrike", Code.Skills.SkillVisualAnchor.Caster, 1f),
        ("Skill_Unit_BattleMage_FlameFall", "LightningBomb", Code.Skills.SkillVisualAnchor.Target, 0.7f),
        ("Ultimate_Unit_BattleMage_FrozenTempest", "StormSpell", Code.Skills.SkillVisualAnchor.Target, 1f),
        ("Skill_Unit_Scout_ShadowRaid", "DarkSpirit", Code.Skills.SkillVisualAnchor.Target, 0.45f),
        ("Ultimate_Unit_Scout_AssassinationMark", "DarkSpirit", Code.Skills.SkillVisualAnchor.Target, 0.45f),
        ("Skill_Enemy_Scout_Ambush", "DarkSpirit", Code.Skills.SkillVisualAnchor.Target, 0.45f),
        ("Ultimate_Enemy_Scout_SmokePoison", "DarkSpirit", Code.Skills.SkillVisualAnchor.Target, 0.6f),
        ("Ultimate_Unit_Arbalist_ArrowRain", "MagicBallsExplosion", Code.Skills.SkillVisualAnchor.Target, 0.7f),
        ("Ultimate_Enemy_Archer_AdventurerVolley", "MagicBallsExplosion", Code.Skills.SkillVisualAnchor.Target, 0.7f),
        ("Ultimate_Unit_Pikeman_PiercingFormation", "MagicBallsExplosion", Code.Skills.SkillVisualAnchor.Target, 0.7f),
        ("Skill_Enemy_Archer_AimedShot", "EnergyBeam", Code.Skills.SkillVisualAnchor.Target, 0.45f),
        ("Skill_Unit_Arbalist_VenomBolt", "EnergyBeam", Code.Skills.SkillVisualAnchor.Target, 0.45f),
        ("Skill_Unit_Pikeman_RuptureThrust", "EnergyBeam", Code.Skills.SkillVisualAnchor.Target, 0.45f),
        // 공용 스킬(해골 부하 등이 씀)
        ("Skill_ShieldShockwave", "KineticShieldStrike", Code.Skills.SkillVisualAnchor.Caster, 0.85f),
        ("Skill_MeteorStrike", "LightningBomb", Code.Skills.SkillVisualAnchor.Target, 0.7f),
        ("Skill_FrostField", "StormSpell", Code.Skills.SkillVisualAnchor.Target, 0.8f),
        ("Skill_ExposeWeakness", "DarkSpirit", Code.Skills.SkillVisualAnchor.Target, 0.45f),
        ("Skill_PoisonField", "DarkSpirit", Code.Skills.SkillVisualAnchor.Target, 0.6f),
        ("Skill_ShadowBackstab", "DarkSpirit", Code.Skills.SkillVisualAnchor.Target, 0.45f),
    };

    /// <summary>마법 탄(Effect05)에서 날아가는 칸만(앞은 모으는 고리, 뒤는 사라짐).</summary>
    private static readonly int[] MagicBoltFlightFrames = { 3, 4, 5 };

    public const string CatalogPath = "Assets/07.SO/Combat/CombatFxCatalog.asset";
    private const string SkeletonWarriorRoot = "Assets/04.Sprites/Characters/SkeletonWarrior";

    public const string ParticleMaterialPath = "Assets/05.Materials/CombatParticle.mat";
    public const string ParticleTexturePath = "Assets/04.Sprites/Effects/ParticleSquare.png";
    private const string UrpParticleMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/ParticlesUnlit.mat";
    private const int ParticleTextureSize = 4;

    private const string MinionDeathFolder = "Assets/04.Sprites/Characters/SkeletonKing/effects/effect2/effect2_standard";
    private const string IntruderDeathFolder = SkeletonWarriorRoot + "/effects/plash explore/orange";
    private const string MinionReferenceSet = AnimationFolder + "/UnitSkeletonSwordBlueAnim.asset";
    private const string IntruderReferenceSet = AnimationFolder + "/EnemyBasicAnim.asset";

    [MenuItem("Tools/Dungeon Keeper/Build Combat Polish")]
    public static string BuildAll()
    {
        var report = new StringBuilder();
        MovePackArt(report);

        foreach (var guid in AssetDatabase.FindAssets("t:CharacterAnimationSetSO", new[] { AnimationFolder }))
            report.AppendLine(FillAnimationSet(AssetDatabase.GUIDToAssetPath(guid)));

        report.AppendLine(FillSkillSpells());

        var material = EnsureFlashMaterial();
        var meleeHit = SliceSheet(Vol2Folder + "/Effect07.png");
        var critical = SliceSheet(Vol2Folder + "/Effect01.png");
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
            AssignSprites(serialized, "<MeleeHitFrames>k__BackingField", meleeHit);
            AssignSprites(serialized, "<CriticalHitFrames>k__BackingField", critical);
            serialized.FindProperty("<ParticleMaterial>k__BackingField").objectReferenceValue = particle;

            // 사망 그림은 대표 캐릭터(해골 검사·기본 모험가)의 공격 그림 크기(PPU)를 따른다.
            // 혼불은 발에서 솟도록 아래 기준점, 폭발은 몸 가운데서 터지도록 가운데 기준점.
            var minion = FindFrames(MinionDeathFolder, "effect2_standard");
            var intruder = FindFrames(IntruderDeathFolder, "orange");
            Assign(serialized, "<MinionDeathFrames>k__BackingField", minion, FirstAttackFramePath(MinionReferenceSet), SpriteAlignment.BottomCenter);
            Assign(serialized, "<IntruderDeathFrames>k__BackingField", intruder, FirstAttackFramePath(IntruderReferenceSet), SpriteAlignment.Center);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            report.AppendLine($"catalog flash={material != null} meleeHit={meleeHit.Count} critical={critical.Count} particle={particle != null}"
                              + $" minionDeath={minion.Count} intruderDeath={intruder.Count}");
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
        var (projectile, impact) = ResolveProjectile(folder, referencePath);

        Assign(serialized, "hurtFrames", hurt, referencePath, null);
        Assign(serialized, "attackAltFrames", alt, referencePath, null);
        AssignSprites(serialized, "projectileFrames", projectile);
        AssignSprites(serialized, "projectileImpactFrames", impact);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);

        return $"{name}: hurt={hurt.Count} alt={alt.Count} projectile={projectile.Count} impact={impact.Count}";
    }

    private static string FirstAttackFramePath(string setPath)
    {
        var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSetSO>(setPath);
        var frames = set != null ? set.GetFrames(CharacterClip.Attack) : System.Array.Empty<Sprite>();
        return frames.Length > 0 ? AssetDatabase.GetAssetPath(frames[0]) : string.Empty;
    }

    private static (List<Sprite> projectile, List<Sprite> impact) ResolveProjectile(string folder, string referencePath)
    {
        var leaf = Path.GetFileName(folder);
        var color = leaf.Contains('_') ? leaf.Substring(leaf.IndexOf('_') + 1) : "origin";

        if (folder.Contains("/ske_archer/"))
        {
            var arrow = $"{SkeletonWarriorRoot}/arrows/arrow_{color}/arrow_1_{color}.png";
            if (!File.Exists(arrow))
                arrow = $"{SkeletonWarriorRoot}/arrows/arrow_origin/arrow_1_origin.png";
            return (ImportFrames(File.Exists(arrow) ? new List<string> { arrow } : new List<string>(), referencePath), new List<Sprite>());
        }

        // 해골 마법사·자기 발사체 그림이 없는 모험가 팩 캐릭터(오크 주술사 등)는 이펙트 팩의 청록 마법 탄을 쏘고
        // 닿은 자리에 전기 불꽃이 튄다(근접 캐릭터는 쓰지 않는다). 캐릭터와 같은 PPU라 몸 크기 그대로 맞는다.
        if (folder.Contains("/ske_mage/"))
            return MagicBolt();

        var eff1 = FindFrames(folder, "eff1");
        if (eff1.Count > 0)
            return (ImportFrames(eff1, referencePath), new List<Sprite>());

        // 성직자 팩은 날아가는 빛(effect)과 닿는 폭발(explosion)이 따로 있다.
        var light = FindFrames(folder, "effect");
        if (light.Count > 0)
            return (ImportFrames(light, referencePath), ImportFrames(FindFrames(folder, "explosion"), referencePath));

        return folder.Contains("/SanctumPixel/") ? MagicBolt() : (new List<Sprite>(), new List<Sprite>());
    }

    private static (List<Sprite> projectile, List<Sprite> impact) MagicBolt()
    {
        var bolt = SliceSheet(Vol2Folder + "/Effect05.png");
        var flight = MagicBoltFlightFrames.Where(index => index < bolt.Count).Select(index => bolt[index]).ToList();
        return (flight, SliceSheet(Vol2Folder + "/Effect06.png"));
    }

    private static List<Sprite> ImportFrames(List<string> paths, string referencePath)
    {
        var sprites = new List<Sprite>();
        foreach (var path in paths)
        {
            MatchImport(path, referencePath, SpriteAlignment.Center);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites;
    }

    /// <summary>스킬 데이터마다 주문 그림·틀 자리·배율을 건다(<see cref="SkillSpells"/>). 표에 없는 스킬은 비운다.</summary>
    private static string FillSkillSpells()
    {
        var filled = 0;
        var missing = new List<string>();
        var bySkill = SkillSpells.ToDictionary(entry => entry.skill);
        foreach (var guid in AssetDatabase.FindAssets("t:SkillDataSO", new[] { SkillFolder }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<Code.Skills.SkillDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            var serialized = new SerializedObject(skill);
            if (bySkill.TryGetValue(skill.name, out var entry))
            {
                var frames = SliceSheet($"{SpellFolder}/{entry.sheet}.png");
                AssignSprites(serialized, "<SpellFrames>k__BackingField", frames);
                serialized.FindProperty("<SpellAnchor>k__BackingField").enumValueIndex = (int)entry.anchor;
                serialized.FindProperty("<SpellScale>k__BackingField").floatValue = entry.scale;
                if (frames.Count > 0)
                    filled++;
                bySkill.Remove(skill.name);
            }
            else
            {
                AssignSprites(serialized, "<SpellFrames>k__BackingField", new List<Sprite>());
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
        }

        missing.AddRange(bySkill.Keys);
        return $"skills spell={filled}" + (missing.Count > 0 ? $" 표에 있는데 없는 스킬: {string.Join(", ", missing)}" : string.Empty);
    }

    /// <summary>팩 그림을 이펙트 폴더로 옮긴다(guid 유지). 이미 옮겼으면 건너뛴다.</summary>
    private static void MovePackArt(StringBuilder report)
    {
        foreach (var (from, to) in PackArt)
        {
            if (File.Exists(to) || !File.Exists(from))
                continue;

            UiKit.EnsureFolder(Path.GetDirectoryName(to)!.Replace('\\', '/'));
            var error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                report.AppendLine($"옮기지 못함 {from}: {error}");
        }
    }

    /// <summary>
    /// 시트를 <see cref="SheetCells"/>의 칸 크기로 잘라(왼쪽 위부터 줄 순서, 빈 칸 제외) 프레임 목록을 돌려준다.
    /// 캐릭터와 같은 PPU·점 필터·무압축. 이미 같은 칸으로 잘려 있으면 다시 자르지 않는다(스프라이트 id 유지).
    /// </summary>
    private static List<Sprite> SliceSheet(string path)
    {
        if (!SheetCells.TryGetValue(path, out var cell) || !File.Exists(path))
            return new List<Sprite>();

        var baseName = Path.GetFileNameWithoutExtension(path);
        var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        pixels.LoadImage(File.ReadAllBytes(path));
        var columns = pixels.width / cell.width;
        var rows = pixels.height / cell.height;
        var pivot = cell.pivot == SpriteAlignment.BottomCenter ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);

        var block = pixels.GetPixels32();
        var cells = new List<Rect>();
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var rect = new Rect(column * cell.width, pixels.height - (row + 1) * cell.height, cell.width, cell.height);
                if (HasInk(block, pixels.width, rect))
                    cells.Add(rect);
            }
        }

        Object.DestroyImmediate(pixels);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var settingsChanged = importer.textureType != TextureImporterType.Sprite
                              || importer.spriteImportMode != SpriteImportMode.Multiple
                              || !Mathf.Approximately(importer.spritePixelsPerUnit, EffectPixelsPerUnit)
                              || importer.filterMode != FilterMode.Point
                              || importer.textureCompression != TextureImporterCompression.Uncompressed
                              || importer.mipmapEnabled;
        if (settingsChanged)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = EffectPixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        var factory = new UnityEditor.U2D.Sprites.SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects();
        var sameCells = existing.Length == cells.Count
                        && existing.Select((rect, i) => rect.name == $"{baseName}_{i}" && rect.rect == cells[i]
                                                         && rect.alignment == cell.pivot).All(match => match);
        if (!sameCells)
        {
            var rects = cells.Select((rect, i) =>
            {
                var name = $"{baseName}_{i}";
                var old = existing.FirstOrDefault(sprite => sprite.name == name);
                return new SpriteRect
                {
                    name = name,
                    rect = rect,
                    alignment = cell.pivot,
                    pivot = pivot,
                    spriteID = old != null ? old.spriteID : GUID.Generate()
                };
            }).ToArray();

            provider.SetSpriteRects(rects);
            var names = provider.GetDataProvider<UnityEditor.U2D.Sprites.ISpriteNameFileIdDataProvider>();
            names?.SetNameFileIdPairs(rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => int.Parse(sprite.name.Substring(sprite.name.LastIndexOf('_') + 1)))
            .ToList();
    }

    private static bool HasInk(Color32[] block, int textureWidth, Rect rect)
    {
        for (var y = (int)rect.y; y < (int)rect.yMax; y++)
        {
            for (var x = (int)rect.x; x < (int)rect.xMax; x++)
            {
                if (block[y * textureWidth + x].a > 0)
                    return true;
            }
        }

        return false;
    }

    private static void AssignSprites(SerializedObject serialized, string field, List<Sprite> sprites)
    {
        var property = serialized.FindProperty(field);
        property.arraySize = sprites.Count;
        for (var i = 0; i < sprites.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
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

    private static void Assign(SerializedObject serialized, string field, List<string> paths, string referencePath, SpriteAlignment? pivot)
    {
        var sprites = new List<Sprite>();
        foreach (var path in paths)
        {
            MatchImport(path, referencePath, pivot);
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
    /// 몸 동작은 기준점(발밑)까지 같게(<paramref name="pivot"/> null), 발사체·폭발은 가운데, 솟는 혼불은 아래 가운데로.
    /// </summary>
    private static void MatchImport(string path, string referencePath, SpriteAlignment? pivot)
    {
        var source = AssetImporter.GetAtPath(referencePath) as TextureImporter;
        var target = AssetImporter.GetAtPath(path) as TextureImporter;
        if (source == null || target == null)
            return;

        var settings = new TextureImporterSettings();
        source.ReadTextureSettings(settings);
        if (pivot.HasValue)
        {
            settings.spriteAlignment = (int)pivot.Value;
            settings.spritePivot = pivot.Value == SpriteAlignment.BottomCenter ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
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
}
