using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Code.BT;
using Code.Enemies;
using Code.Entities;
using Code.Manager;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 29일 이후 순환 보스 셋(대마법사·오크 대족장·기마 두목)과 호위·증원의 데이터를 만든다(Docs/Design/RotatingBossDesign.md).
/// - 팩 그림(PPU 100, 가운데 기준점)을 기존 모험가 그림과 같은 임포트 설정으로 맞추고 애니메이션 묶음을 만든다.
/// - 적 데이터·보스 파티·증원 파티를 만들거나 같은 경로의 에셋을 덮어 쓴다(guid 유지).
/// - WaveConfig의 순환 보스 목록(<c>rotatingBosses</c>)만 채운다(<see cref="WireWaveConfig"/>). 다른 칸은 건드리지 않는다.
/// Unity CLI: <c>RotatingBossBuilder.BuildAll()</c>. 돌려준 문자열이 만든 것의 목록이다.
/// </summary>
public static class RotatingBossBuilder
{
    private const string PackRoot = "Assets/04.Sprites/Characters/SanctumPixel/42_pixel_art_characters_bundle";
    private const string EnemyFolder = "Assets/07.SO/Enemies";
    private const string BossFolder = EnemyFolder + "/Bosses";
    private const string PartyFolder = "Assets/07.SO/Waves/Bosses";
    private const string WaveConfigPath = "Assets/07.SO/WaveConfig.asset";

    /// <summary>임포트 설정을 따라 할 기존 모험가 그림(같은 팩·PPU 28·점 필터·무압축).</summary>
    private const string ReferenceSet = CombatPolishBuilder.AnimationFolder + "/EnemyArcherAnim.asset";

    /// <summary>자기 발사체 그림이 없는 원거리(오크 궁수)는 팩 캐릭터 공용 마법 탄을 쓴다(<c>CombatPolishBuilder</c>와 같은 그림).</summary>
    private const string MagicBoltSet = CombatPolishBuilder.AnimationFolder + "/UnitShroomAnim.asset";

    private const string ArcherPrefab = "Assets/03.Prefabs/Characters/Enemies/Generated/Enemy_Archer.prefab";
    private const string SwordPrefab = "Assets/03.Prefabs/Characters/Enemies/Generated/Enemy_Sword.prefab";
    private const string ScoutPrefab = "Assets/03.Prefabs/Characters/Enemies/Generated/Enemy_Scout.prefab";

    private enum Projectile
    {
        None,
        PackEffect,
        MagicBolt
    }

    private sealed class EnemySpec
    {
        public string Asset;
        public string Folder;
        public string Art;
        public string Animation;
        public Projectile Projectile;
        public string Name;
        public BattleRole Role;
        public EntityGrade Grade;
        public string Prefab;
        public int Health;
        public int Attack;
        public float Interval;
        public int Defense;
        public float Evasion;
        public int Fear;
        public int Greed;
        public int Mobility;
        public Code.Audio.GameSfxCue Sfx;
        public bool DisarmsTraps;
    }

    // 수치는 초기값이다(밸런스는 아직 보지 않는다). 보스는 기존 보스처럼 기본값을 낮게 두고 보스 정의 배율로 키운다.
    private static readonly EnemySpec[] Enemies =
    {
        new()
        {
            Asset = "Boss_Archmage", Folder = BossFolder, Art = "wizard-topdown-package/Sprite/wizard-topdown/7",
            Animation = "BossArchmageAnim", Projectile = Projectile.PackEffect, Name = "대마법사",
            Role = BattleRole.Ranged, Grade = EntityGrade.Grade1, Prefab = ArcherPrefab,
            Health = 22, Attack = 3, Interval = 1.4f, Defense = 6, Evasion = 0f, Fear = 0, Greed = 0, Mobility = 0,
            Sfx = Code.Audio.GameSfxCue.AttackBow, DisarmsTraps = true
        },
        new()
        {
            Asset = "ApprenticeMageEnemy", Folder = EnemyFolder, Art = "wizard-topdown-package/Sprite/wizard-topdown/2",
            Animation = "EnemyApprenticeMageAnim", Projectile = Projectile.PackEffect, Name = "견습 마법사",
            Role = BattleRole.Ranged, Grade = EntityGrade.Grade2, Prefab = ArcherPrefab,
            Health = 9, Attack = 2, Interval = 1.5f, Defense = 0, Evasion = 0.12f, Fear = 2, Greed = 1, Mobility = 3,
            Sfx = Code.Audio.GameSfxCue.AttackBow
        },
        new()
        {
            Asset = "Boss_OrcWarchief", Folder = BossFolder, Art = "orc-topdown-package/Sprite/orc-topdown/leader/1",
            Animation = "BossOrcWarchiefAnim", Name = "오크 대족장",
            Role = BattleRole.Tank, Grade = EntityGrade.Grade1, Prefab = SwordPrefab,
            Health = 30, Attack = 3, Interval = 1.1f, Defense = 18, Evasion = 0.02f, Fear = 0, Greed = 1, Mobility = 1,
            Sfx = Code.Audio.GameSfxCue.Attack
        },
        new()
        {
            Asset = "OrcWarriorEnemy", Folder = EnemyFolder, Art = "orc-topdown-package/Sprite/orc-topdown/warrior/1",
            Animation = "EnemyOrcWarriorAnim", Name = "오크 전사",
            Role = BattleRole.Tank, Grade = EntityGrade.Grade3, Prefab = SwordPrefab,
            Health = 16, Attack = 2, Interval = 1.1f, Defense = 10, Evasion = 0.03f, Fear = 0, Greed = 2, Mobility = 1,
            Sfx = Code.Audio.GameSfxCue.Attack
        },
        new()
        {
            Asset = "OrcArcherEnemy", Folder = EnemyFolder, Art = "orc-topdown-package/Sprite/orc-topdown/archer/1",
            Animation = "EnemyOrcArcherAnim", Projectile = Projectile.MagicBolt, Name = "오크 궁수",
            Role = BattleRole.Ranged, Grade = EntityGrade.Grade2, Prefab = ArcherPrefab,
            Health = 9, Attack = 2, Interval = 1.4f, Defense = 0, Evasion = 0.15f, Fear = 1, Greed = 1, Mobility = 4,
            Sfx = Code.Audio.GameSfxCue.AttackBow
        },
        new()
        {
            Asset = "Boss_BanditRider", Folder = BossFolder, Art = "bandit-topdown-package/bandit-topdown/Sprite/axehorse/1",
            Animation = "BossBanditRiderAnim", Name = "기마 두목",
            Role = BattleRole.Melee, Grade = EntityGrade.Grade1, Prefab = ScoutPrefab,
            Health = 24, Attack = 4, Interval = 0.9f, Defense = 8, Evasion = 0.1f, Fear = 0, Greed = 6, Mobility = 9,
            Sfx = Code.Audio.GameSfxCue.Attack
        },
        new()
        {
            Asset = "BanditSpearmanEnemy", Folder = EnemyFolder, Art = "bandit-topdown-package/bandit-topdown/Sprite/spearman/1",
            Animation = "EnemyBanditSpearmanAnim", Name = "도적 창병",
            Role = BattleRole.Melee, Grade = EntityGrade.Grade2, Prefab = SwordPrefab,
            Health = 12, Attack = 2, Interval = 1f, Defense = 4, Evasion = 0.1f, Fear = 2, Greed = 4, Mobility = 5,
            Sfx = Code.Audio.GameSfxCue.Attack
        },
    };

    /// <summary>파티 이름 → (표시 이름, 멤버 에셋 이름 순서). 보스 파티는 첫 멤버가 보스다.</summary>
    private static readonly (string asset, string name, string[] members)[] Parties =
    {
        ("BossParty_Rotation_Archmage", "대마법사의 탑", new[] { "Boss_Archmage", "ApprenticeMageEnemy", "ApprenticeMageEnemy" }),
        ("BossParty_Rotation_OrcWarchief", "오크 전쟁단", new[] { "Boss_OrcWarchief", "OrcWarriorEnemy", "OrcWarriorEnemy", "OrcArcherEnemy" }),
        ("BossParty_Rotation_BanditRider", "도적 기마대", new[] { "Boss_BanditRider", "BanditSpearmanEnemy", "BanditSpearmanEnemy", "BanditSpearmanEnemy" }),
        ("BossReinforcement_OrcWarriors", "오크 증원", new[] { "OrcWarriorEnemy", "OrcWarriorEnemy", "OrcWarriorEnemy" }),
    };

    [MenuItem("Tools/Dungeon Keeper/Build Rotating Bosses")]
    public static string BuildAll()
    {
        var report = new StringBuilder();
        var reference = FirstAttackFramePath(ReferenceSet);
        if (string.IsNullOrEmpty(reference))
            return $"기준 그림을 찾지 못했습니다: {ReferenceSet}";

        var built = new Dictionary<string, EnemyDataSO>();
        foreach (var spec in Enemies)
        {
            var set = BuildAnimationSet(spec, reference, report);
            if (set == null)
                continue;

            built[spec.Asset] = BuildEnemy(spec, set);
            report.AppendLine($"{spec.Asset}: idle={set.GetFrames(CharacterClip.Idle).Length} projectile={set.ProjectileFrames.Length}");
        }

        foreach (var (asset, name, members) in Parties)
        {
            var missing = members.Where(member => !built.ContainsKey(member)).Distinct().ToArray();
            if (missing.Length > 0)
            {
                report.AppendLine($"{asset}: 멤버가 없어 건너뜀({string.Join(", ", missing)})");
                continue;
            }

            BuildParty(asset, name, members.Select(member => built[member]).ToArray());
            report.AppendLine($"{asset}: {members.Length}명");
        }

        report.AppendLine(WireWaveConfig());
        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    /// <summary>
    /// 순환 보스 정의 셋(순서 = 35·42·49일). 배율·패배 조건·단계는 기획서 표를 따른다.
    /// 기마 두목은 금고 침입에 시간 제한을 함께 둔다 — 금고가 없는 판에서 패배 조건이 사라지지 않게(2026-10-28 실측).
    /// </summary>
    public static string WireWaveConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(WaveConfigPath);
        if (config == null)
            return $"WaveConfig를 찾지 못했습니다: {WaveConfigPath}";

        var archmage = AssetDatabase.LoadAssetAtPath<AdventurerPartySO>($"{PartyFolder}/BossParty_Rotation_Archmage.asset");
        var warchief = AssetDatabase.LoadAssetAtPath<AdventurerPartySO>($"{PartyFolder}/BossParty_Rotation_OrcWarchief.asset");
        var rider = AssetDatabase.LoadAssetAtPath<AdventurerPartySO>($"{PartyFolder}/BossParty_Rotation_BanditRider.asset");
        var reinforcement = AssetDatabase.LoadAssetAtPath<AdventurerPartySO>($"{PartyFolder}/BossReinforcement_OrcWarriors.asset");
        if (archmage == null || warchief == null || rider == null || reinforcement == null)
            return "순환 보스 파티가 없어 WaveConfig를 잇지 않았습니다.";

        var serialized = new SerializedObject(config);
        var list = serialized.FindProperty("rotatingBosses");
        list.arraySize = 3;

        var entry = list.GetArrayElementAtIndex(0);
        SetBoss(entry, archmage, "대마법사", "밟은 함정을 해체한다 — 함정보다 부하로 막아라", 6f, 2.5f, 1.6f, church: false);
        entry.FindPropertyRelative("timeLimitSeconds").floatValue = 180f;
        entry.FindPropertyRelative("enableFinalPhase").boolValue = true;
        entry.FindPropertyRelative("phaseSubtitle").stringValue = "호위 전멸 — 대마법사 방어 약화 · 공격 가속";

        entry = list.GetArrayElementAtIndex(1);
        SetBoss(entry, warchief, "오크 대족장", "오크 전쟁단이 밀고 들어온다 — 얇은 경비는 버티지 못한다", 7.5f, 2.2f, 1.7f, church: true);
        entry.FindPropertyRelative("loseAfterUnitsDowned").intValue = 4;
        entry.FindPropertyRelative("enableReinforcementPhase").boolValue = true;
        entry.FindPropertyRelative("reinforcementHealthRatio").floatValue = 0.55f;
        entry.FindPropertyRelative("reinforcementParty").objectReferenceValue = reinforcement;
        entry.FindPropertyRelative("reinforcementCount").intValue = 3;
        entry.FindPropertyRelative("reinforcementSubtitle").stringValue = "전쟁 북이 울렸다 — 오크 전사가 더 온다";

        entry = list.GetArrayElementAtIndex(2);
        SetBoss(entry, rider, "기마 두목", "말 탄 도적이 금고로 내달린다 — 입구 가까운 금고를 지켜라", 5f, 2.5f, 1.6f, church: false);
        entry.FindPropertyRelative("loseOnTreasuryBreach").boolValue = true;
        entry.FindPropertyRelative("timeLimitSeconds").floatValue = 240f;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return "WaveConfig rotatingBosses=3";
    }

    /// <summary>한 칸을 기본값으로 비운 뒤 공통 값을 넣는다(배열을 늘릴 때 앞 칸 값이 복사되기 때문).</summary>
    private static void SetBoss(SerializedProperty entry, AdventurerPartySO party, string title, string subtitle,
        float health, float attack, float visualScale, bool church)
    {
        entry.FindPropertyRelative("targetDay").intValue = 1;
        entry.FindPropertyRelative("party").objectReferenceValue = party;
        entry.FindPropertyRelative("title").stringValue = title;
        entry.FindPropertyRelative("subtitle").stringValue = subtitle;
        entry.FindPropertyRelative("healthMultiplier").floatValue = health;
        entry.FindPropertyRelative("attackMultiplier").floatValue = attack;
        entry.FindPropertyRelative("visualScale").floatValue = visualScale;
        entry.FindPropertyRelative("enableFinalPhase").boolValue = false;
        entry.FindPropertyRelative("phaseDefensePenalty").intValue = 8;
        entry.FindPropertyRelative("phaseAttackIntervalMultiplier").floatValue = 0.75f;
        entry.FindPropertyRelative("phaseSubtitle").stringValue = string.Empty;
        entry.FindPropertyRelative("enableReinforcementPhase").boolValue = false;
        entry.FindPropertyRelative("reinforcementHealthRatio").floatValue = 0.5f;
        entry.FindPropertyRelative("reinforcementParty").objectReferenceValue = null;
        entry.FindPropertyRelative("reinforcementCount").intValue = 0;
        entry.FindPropertyRelative("reinforcementSubtitle").stringValue = string.Empty;
        entry.FindPropertyRelative("timeLimitSeconds").floatValue = 0f;
        entry.FindPropertyRelative("loseOnTreasuryBreach").boolValue = false;
        entry.FindPropertyRelative("loseAfterUnitsDowned").intValue = 0;
        entry.FindPropertyRelative("allowChurchRevival").boolValue = church;
        foreach (var text in new[] { "<ForecastText>k__BackingField", "<VictorySummary>k__BackingField", "<VictoryText>k__BackingField" })
            entry.FindPropertyRelative(text).stringValue = string.Empty;
    }

    private static CharacterAnimationSetSO BuildAnimationSet(EnemySpec spec, string reference, StringBuilder report)
    {
        var folder = $"{PackRoot}/{spec.Art}";
        var clips = new Dictionary<string, List<Sprite>>();
        foreach (var prefix in new[] { "stand", "walk", "attack", "die" })
        {
            clips[prefix] = ImportFrames(FindFrames(folder, prefix), reference, null);
            if (clips[prefix].Count == 0)
            {
                report.AppendLine($"{spec.Asset}: {folder}에 {prefix} 그림이 없어 건너뜀");
                return null;
            }
        }

        var projectile = new List<Sprite>();
        var impact = new List<Sprite>();
        if (spec.Projectile == Projectile.PackEffect)
        {
            projectile = ImportFrames(FindFrames(folder, "eff1"), reference, SpriteAlignment.Center);
        }
        else if (spec.Projectile == Projectile.MagicBolt)
        {
            var bolt = AssetDatabase.LoadAssetAtPath<CharacterAnimationSetSO>(MagicBoltSet);
            if (bolt != null)
            {
                projectile = bolt.ProjectileFrames.ToList();
                impact = bolt.ProjectileImpactFrames.ToList();
            }
        }

        var path = $"{CombatPolishBuilder.AnimationFolder}/{spec.Animation}.asset";
        var set = LoadOrCreate<CharacterAnimationSetSO>(path);
        var serialized = new SerializedObject(set);
        AssignSprites(serialized, "idleFrames", clips["stand"]);
        AssignSprites(serialized, "walkFrames", clips["walk"]);
        AssignSprites(serialized, "attackFrames", clips["attack"]);
        AssignSprites(serialized, "defeatedFrames", clips["die"]);
        AssignSprites(serialized, "projectileFrames", projectile);
        AssignSprites(serialized, "projectileImpactFrames", impact);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(set);
        return set;
    }

    private static EnemyDataSO BuildEnemy(EnemySpec spec, CharacterAnimationSetSO set)
    {
        UiKit.EnsureFolder(spec.Folder);
        var data = LoadOrCreate<EnemyDataSO>($"{spec.Folder}/{spec.Asset}.asset");
        var serialized = new SerializedObject(data);
        var idle = set.GetFrames(CharacterClip.Idle);
        var attack = set.GetFrames(CharacterClip.Attack);
        var defeated = set.GetFrames(CharacterClip.Defeated);

        serialized.FindProperty("<Name>k__BackingField").stringValue = spec.Name;
        serialized.FindProperty("<Role>k__BackingField").intValue = (int)spec.Role;
        serialized.FindProperty("<Grade>k__BackingField").intValue = (int)spec.Grade;
        serialized.FindProperty("<Prefab>k__BackingField").objectReferenceValue = LoadEnemyPrefab(spec.Prefab);
        serialized.FindProperty("<MaxHealth>k__BackingField").intValue = spec.Health;
        serialized.FindProperty("<AttackDamage>k__BackingField").intValue = spec.Attack;
        serialized.FindProperty("<AttackInterval>k__BackingField").floatValue = spec.Interval;
        serialized.FindProperty("<Defense>k__BackingField").intValue = spec.Defense;
        serialized.FindProperty("<EvasionChance>k__BackingField").floatValue = spec.Evasion;
        serialized.FindProperty("<Fear>k__BackingField").intValue = spec.Fear;
        serialized.FindProperty("<Greed>k__BackingField").intValue = spec.Greed;
        serialized.FindProperty("<Mobility>k__BackingField").intValue = spec.Mobility;
        serialized.FindProperty("<AttackSfx>k__BackingField").intValue = (int)spec.Sfx;
        serialized.FindProperty("<DisarmsTraps>k__BackingField").boolValue = spec.DisarmsTraps;
        serialized.FindProperty("<AnimationSet>k__BackingField").objectReferenceValue = set;
        serialized.FindProperty("<IdleSprite>k__BackingField").objectReferenceValue = idle[0];
        serialized.FindProperty("<BoardSprite>k__BackingField").objectReferenceValue = idle[0];
        serialized.FindProperty("<AttackSprite>k__BackingField").objectReferenceValue = attack[attack.Length / 2];
        serialized.FindProperty("<DefeatedSprite>k__BackingField").objectReferenceValue = defeated[defeated.Length - 1];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        return data;
    }

    private static void BuildParty(string asset, string name, EnemyDataSO[] members)
    {
        var party = LoadOrCreate<AdventurerPartySO>($"{PartyFolder}/{asset}.asset");
        var serialized = new SerializedObject(party);
        serialized.FindProperty("<PartyName>k__BackingField").stringValue = name;
        var list = serialized.FindProperty("<Members>k__BackingField");
        list.arraySize = members.Length;
        for (var i = 0; i < members.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = members[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(party);
    }

    private static Enemy LoadEnemyPrefab(string path)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var enemy = prefab != null ? prefab.GetComponent<Enemy>() : null;
        if (enemy == null)
            Debug.LogError($"{nameof(RotatingBossBuilder)}: {path}에서 Enemy를 찾지 못했습니다.");
        return enemy;
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

    private static string FirstAttackFramePath(string setPath)
    {
        var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSetSO>(setPath);
        var frames = set != null ? set.GetFrames(CharacterClip.Attack) : System.Array.Empty<Sprite>();
        return frames.Length > 0 ? AssetDatabase.GetAssetPath(frames[0]) : string.Empty;
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

    /// <summary>
    /// 기준 그림과 같은 크기(PPU)·필터·압축·기준점으로 맞춘다. 팩 그대로(PPU 100)면 캐릭터가 3.5배 작게 나온다.
    /// 발사체는 가운데 기준점(<paramref name="pivot"/>)으로 둔다.
    /// </summary>
    private static List<Sprite> ImportFrames(List<string> paths, string referencePath, SpriteAlignment? pivot)
    {
        var source = (TextureImporter)AssetImporter.GetAtPath(referencePath);
        var settings = new TextureImporterSettings();
        source.ReadTextureSettings(settings);
        if (pivot.HasValue)
        {
            settings.spriteAlignment = (int)pivot.Value;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
        }

        var sprites = new List<Sprite>();
        foreach (var path in paths)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter target)
                continue;

            var current = new TextureImporterSettings();
            target.ReadTextureSettings(current);
            var same = target.textureType == TextureImporterType.Sprite
                       && current.spriteMode == settings.spriteMode
                       && Mathf.Approximately(current.spritePixelsPerUnit, settings.spritePixelsPerUnit)
                       && current.filterMode == settings.filterMode
                       && current.spriteAlignment == settings.spriteAlignment
                       && current.spritePivot == settings.spritePivot
                       && target.textureCompression == source.textureCompression;
            if (!same)
            {
                target.SetTextureSettings(settings);
                target.textureCompression = source.textureCompression;
                target.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites;
    }

    private static void AssignSprites(SerializedObject serialized, string field, List<Sprite> sprites)
    {
        var property = serialized.FindProperty(field);
        property.arraySize = sprites.Count;
        for (var i = 0; i < sprites.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }
}
