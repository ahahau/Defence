#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Buildings;
using _01.Code.Combat;
using _01.Code.Entities;
using _01.Code.StatusEffects;
using _01.Code.Units;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    public static class GeneratedCombatContentInstaller
    {
        private const string UnitFolder = "Assets/03.SO/Units/Generated";
        private const string UnitPrefabFolder = "Assets/04.Prefab/Characters/Generated";
        private const string ArtifactFolder = "Assets/03.SO/Artifacts/GeneratedUnit";
        private const string StatusFolder = "Assets/03.SO/StatusEffects/Generated";
        private const string StatusEffectFolder = "Assets/03.SO/StatusEffects/Generated/Effects";
        private const string TrapPrefabFolder = "Assets/04.Prefab/Buildings/Traps/Generated";
        private const string BuildingFolder = "Assets/03.SO/Buildings/Generated";

        private const string UnitTemplatePath = "Assets/04.Prefab/Characters/Unit.prefab";
        private const string BladeTrapTemplatePath = "Assets/04.Prefab/Buildings/Traps/Trap_Blade.prefab";
        private const string SnareTrapTemplatePath = "Assets/04.Prefab/Buildings/Traps/Trap_Snare.prefab";
        private const string CrusherTrapTemplatePath = "Assets/04.Prefab/Buildings/Traps/Trap_Crusher.prefab";

        [MenuItem("Tools/Defence/Install Generated Combat Content")]
        public static void Install()
        {
            EnsureFolders();

            var unitSprite = LoadFirstSprite("Assets/05.Graphs/Player");
            var boardSprite = unitSprite != null ? unitSprite : LoadFirstSprite("Assets/05.Graphs");
            var trapStatus = CreateStatusEffects();
            CreateUnitSet(unitSprite, boardSprite);
            CreateArtifacts();
            CreateTrapSet(trapStatus);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated combat content installed.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder(UnitFolder);
            EnsureFolder(UnitPrefabFolder);
            EnsureFolder(ArtifactFolder);
            EnsureFolder($"{ArtifactFolder}/Effects");
            EnsureFolder(StatusFolder);
            EnsureFolder(StatusEffectFolder);
            EnsureFolder(TrapPrefabFolder);
            EnsureFolder(BuildingFolder);
        }

        private static void CreateUnitSet(Sprite unitSprite, Sprite boardSprite)
        {
            var specs = new[]
            {
                new UnitSpec("Scout", "정찰병", EntityGrade.Grade1, 18, 1, false, 1, 1, 8, 2, 0.85f),
                new UnitSpec("Pikeman", "창병", EntityGrade.Grade2, 28, 2, false, 2, 1, 14, 3, 1.1f),
                new UnitSpec("Arbalist", "석궁병", EntityGrade.Grade3, 42, 2, false, 2, 2, 11, 5, 1.35f),
                new UnitSpec("Guardian", "수호병", EntityGrade.Grade3, 48, 3, false, 3, 1, 24, 2, 1.45f),
                new UnitSpec("BattleMage", "전투 마도사", EntityGrade.Grade4, 64, 4, false, 3, 2, 16, 6, 1.6f),
                new UnitSpec("Vanguard", "선봉대", EntityGrade.Grade5, 82, 5, false, 4, 2, 28, 7, 1.25f),
            };

            foreach (var spec in specs)
            {
                var prefab = CreateUnitPrefab(spec, unitSprite);
                var data = LoadOrCreate<UnitDataSO>($"{UnitFolder}/{spec.Id}UnitData.asset");
                Set(data, "<Grade>k__BackingField", spec.Grade);
                Set(data, "<BoardSprite>k__BackingField", boardSprite);
                Set(data, "<Sprite>k__BackingField", unitSprite);
                Set(data, "<Prefab>k__BackingField", prefab != null ? prefab.GetComponent<Unit>() : null);
                Set(data, "<Name>k__BackingField", spec.DisplayName);
                Set(data, "<Cost>k__BackingField", spec.Cost);
                Set(data, "<MagicCost>k__BackingField", spec.MagicCost);
                Set(data, "<Locked>k__BackingField", spec.Locked);
                Set(data, "<BaseDanger>k__BackingField", spec.BaseDanger);
                Set(data, "<DangerIncreaseOnCombat>k__BackingField", spec.DangerIncreaseOnCombat);
                EditorUtility.SetDirty(data);

                if (prefab == null)
                    Debug.LogWarning($"Unit prefab was not created for {spec.Id}.");
            }
        }

        private static GameObject CreateUnitPrefab(UnitSpec spec, Sprite unitSprite)
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(UnitTemplatePath);
            if (template == null)
                return null;

            var path = $"{UnitPrefabFolder}/Unit_{spec.Id}.prefab";
            if (!AssetDatabase.CopyAsset(UnitTemplatePath, path) && AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return null;

            var root = PrefabUtility.LoadPrefabContents(path);
            root.name = $"Unit_{spec.Id}";
            var health = root.GetComponent<Health>();
            var combatant = root.GetComponent<Combatant>();
            var spriteRenderer = root.GetComponentInChildren<SpriteRenderer>();

            if (health != null)
                Set(health, "maxHealth", spec.MaxHealth);
            if (combatant != null)
            {
                Set(combatant, "attackDamage", spec.AttackDamage);
                Set(combatant, "attackInterval", spec.AttackInterval);
            }
            if (spriteRenderer != null && unitSprite != null)
                spriteRenderer.sprite = unitSprite;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void CreateArtifacts()
        {
            var lowHealth = LoadOrCreateEffect<LowHealthDamageMultiplierEffectSO>("LowHealth_Execution");
            Set(lowHealth, "triggerHealthRatio", 0.45f);
            Set(lowHealth, "damageMultiplier", 1.55f);
            EditorUtility.SetDirty(lowHealth);

            var loneWolf = LoadOrCreateEffect<NoAdjacentEnemyDamageMultiplierEffectSO>("NoAdjacent_LoneWolf");
            Set(loneWolf, "damageMultiplier", 1.35f);
            EditorUtility.SetDirty(loneWolf);

            var specs = new[]
            {
                new ArtifactSpec("IronRations", "철분 군량", "고용 유닛 최대 체력 +8", ArtifactTarget.HiredUnitsOnly, 0, 1f, 8, 1f, new Color(0.72f, 0.85f, 0.58f, 1f)),
                new ArtifactSpec("VeteranBanner", "베테랑 군기", "고용 유닛 공격력 +2", ArtifactTarget.HiredUnitsOnly, 2, 1f, 0, 1f, new Color(0.95f, 0.42f, 0.32f, 1f)),
                new ArtifactSpec("SharpeningKit", "야전 숫돌", "고용 유닛 공격력 18% 증가", ArtifactTarget.HiredUnitsOnly, 0, 1.18f, 0, 1f, new Color(0.90f, 0.72f, 0.36f, 1f)),
                new ArtifactSpec("WarDrum", "진군 북", "모든 유닛 공격 속도 12% 증가", ArtifactTarget.AllUnits, 0, 1f, 0, 0.88f, new Color(0.92f, 0.28f, 0.24f, 1f)),
                new ArtifactSpec("TowerShield", "탑 방패", "고용 유닛 최대 체력 +14, 공격 속도 8% 감소", ArtifactTarget.HiredUnitsOnly, 0, 1f, 14, 1.08f, new Color(0.42f, 0.62f, 0.88f, 1f)),
                new ArtifactSpec("SkirmisherBoots", "척후병 장화", "고용 유닛 공격 속도 18% 증가, 최대 체력 -3", ArtifactTarget.HiredUnitsOnly, 0, 1f, -3, 0.82f, new Color(0.34f, 0.86f, 0.78f, 1f)),
                new ArtifactSpec("CaptainSigil", "대장의 인장", "플레이어 유닛 공격력 +3, 최대 체력 +10", ArtifactTarget.PlayerOnly, 3, 1f, 10, 1f, new Color(0.98f, 0.82f, 0.30f, 1f)),
                new ArtifactSpec("GlassDagger", "유리 단검", "고용 유닛 공격력 35% 증가, 최대 체력 -8", ArtifactTarget.HiredUnitsOnly, 0, 1.35f, -8, 1f, new Color(0.68f, 0.96f, 1f, 1f)),
                new ArtifactSpec("FieldMedicSatchel", "야전 의무낭", "모든 유닛 최대 체력 +10", ArtifactTarget.AllUnits, 0, 1f, 10, 1f, new Color(0.55f, 0.92f, 0.66f, 1f)),
                new ArtifactSpec("ExecutionOath", "처형 서약", "고용 유닛 공격력 +1. 체력이 낮을 때 피해량 증가", ArtifactTarget.HiredUnitsOnly, 1, 1f, 0, 1f, new Color(0.86f, 0.18f, 0.30f, 1f), lowHealth),
                new ArtifactSpec("LoneWolfCharm", "고립 늑대 부적", "고용 유닛이 인접 적 없이 싸울 때 피해량 증가", ArtifactTarget.HiredUnitsOnly, 0, 1f, 0, 1f, new Color(0.70f, 0.70f, 0.96f, 1f), loneWolf),
                new ArtifactSpec("QuartermasterLedger", "병참 장부", "모든 유닛 공격력 +1, 최대 체력 +5", ArtifactTarget.AllUnits, 1, 1f, 5, 1f, new Color(0.66f, 0.50f, 0.34f, 1f)),
            };

            foreach (var spec in specs)
            {
                var artifact = LoadOrCreate<ArtifactDataSO>($"{ArtifactFolder}/{spec.Id}Artifact.asset");
                Set(artifact, "<DisplayName>k__BackingField", spec.DisplayName);
                Set(artifact, "<Description>k__BackingField", spec.Description);
                Set(artifact, "<IconColor>k__BackingField", spec.Color);
                Set(artifact, "<Target>k__BackingField", spec.Target);
                Set(artifact, "<AttackDamageBonus>k__BackingField", spec.AttackDamageBonus);
                Set(artifact, "<AttackDamageMultiplier>k__BackingField", spec.AttackDamageMultiplier);
                Set(artifact, "<MaxHealthBonus>k__BackingField", spec.MaxHealthBonus);
                Set(artifact, "<AttackIntervalMultiplier>k__BackingField", spec.AttackIntervalMultiplier);
                Set(artifact, "<Effects>k__BackingField", spec.Effects);
                EditorUtility.SetDirty(artifact);
            }
        }

        private static Dictionary<string, StatusEffectDataSO> CreateStatusEffects()
        {
            var armorBreak = LoadOrCreate<TrapDamageTakenStatusEffectSO>($"{StatusEffectFolder}/ArmorBreak_TrapDamageTaken.asset");
            Set(armorBreak, "multiplier", 1.65f);
            EditorUtility.SetDirty(armorBreak);

            var slow = LoadOrCreate<AttackIntervalStatusEffectSO>($"{StatusEffectFolder}/Snared_AttackInterval.asset");
            Set(slow, "multiplier", 1.35f);
            EditorUtility.SetDirty(slow);

            var fragile = CreateStatus("ArmorBreakStatusEffect", "방어 파괴", "트랩에게 받는 피해가 크게 증가합니다.", 2, 1f, 1.2f, armorBreak);
            var snared = CreateStatus("SnaredStatusEffect", "속박", "공격 간격이 증가합니다.", 2, 1.15f, 1f, slow);
            var exposed = CreateStatus("ExposedStatusEffect", "노출", "짧은 시간 동안 공격과 트랩 피해에 취약해집니다.", 1, 1.2f, 1.5f);

            return new Dictionary<string, StatusEffectDataSO>
            {
                { "ArmorBreak", fragile },
                { "Snared", snared },
                { "Exposed", exposed },
            };
        }

        private static StatusEffectDataSO CreateStatus(
            string id,
            string displayName,
            string description,
            int duration,
            float attackIntervalMultiplier,
            float trapDamageTakenMultiplier,
            params StatusEffectSO[] effects)
        {
            var status = LoadOrCreate<StatusEffectDataSO>($"{StatusFolder}/{id}.asset");
            Set(status, "displayName", displayName);
            Set(status, "description", description);
            Set(status, "durationNodeVisits", duration);
            Set(status, "attackIntervalMultiplier", attackIntervalMultiplier);
            Set(status, "trapDamageTakenMultiplier", trapDamageTakenMultiplier);
            Set(status, "effects", effects);
            EditorUtility.SetDirty(status);
            return status;
        }

        private static void CreateTrapSet(IReadOnlyDictionary<string, StatusEffectDataSO> statuses)
        {
            var trapBoardSprite = LoadFirstSprite("Assets/05.Graphs/Trap");
            var specs = new[]
            {
                new TrapSpec("Caltrop", "마름쇠 트랩", EntityGrade.Grade1, 12, 1, CrusherTrapTemplatePath, 0.55f, 3, 0.35f, 0, 1, statuses["Snared"]),
                new TrapSpec("ArmorBreaker", "갑옷분쇄 트랩", EntityGrade.Grade3, 32, 3, BladeTrapTemplatePath, 0.38f, 8, 0.45f, 1, 3, statuses["ArmorBreak"]),
                new TrapSpec("AmbushNet", "매복 그물", EntityGrade.Grade2, 22, 2, SnareTrapTemplatePath, 0.65f, 2, 0.60f, 0, 2, statuses["Snared"]),
                new TrapSpec("ExecutionSaw", "처형 톱날", EntityGrade.Grade4, 46, 4, BladeTrapTemplatePath, 0.28f, 13, 0.25f, 3, 4, statuses["Exposed"]),
            };

            foreach (var spec in specs)
            {
                var prefab = CreateTrapPrefab(spec);
                var data = LoadOrCreate<BuildingDataSO>($"{BuildingFolder}/{spec.Id}TrapBuildingData.asset");
                var sprite = trapBoardSprite != null
                    ? trapBoardSprite
                    : prefab != null ? prefab.GetComponentInChildren<SpriteRenderer>()?.sprite : null;
                Set(data, "<Grade>k__BackingField", spec.Grade);
                Set(data, "<BoardSprite>k__BackingField", sprite);
                Set(data, "<DisplayName>k__BackingField", spec.DisplayName);
                Set(data, "<Cost>k__BackingField", spec.Cost);
                Set(data, "<Prefab>k__BackingField", prefab != null ? prefab.GetComponent<Building>() : null);
                Set(data, "<Unique>k__BackingField", false);
                Set(data, "<Locked>k__BackingField", false);
                Set(data, "<BaseDanger>k__BackingField", spec.BaseDanger);
                Set(data, "<Category>k__BackingField", InstallCategory.Trap);
                EditorUtility.SetDirty(data);
            }
        }

        private static GameObject CreateTrapPrefab(TrapSpec spec)
        {
            var templatePath = AssetDatabase.LoadAssetAtPath<GameObject>(spec.TemplatePath) != null
                ? spec.TemplatePath
                : BladeTrapTemplatePath;
            var path = $"{TrapPrefabFolder}/Trap_{spec.Id}.prefab";
            if (!AssetDatabase.CopyAsset(templatePath, path) && AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return null;

            var root = PrefabUtility.LoadPrefabContents(path);
            root.name = $"Trap_{spec.Id}";
            var trap = root.GetComponent<Trap>();
            if (trap != null)
            {
                Set(trap, "triggerChance", spec.TriggerChance);
                Set(trap, "damage", spec.Damage);
                Set(trap, "injuryChance", spec.InjuryChance);
                Set(trap, "bonusDamage", spec.BonusDamage);
                Set(trap, "injuryStatusEffect", spec.StatusEffect);
                Set(trap, "<DangerIncreaseOnTrigger>k__BackingField", spec.DangerIncreaseOnTrigger);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
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

        private static T LoadOrCreateEffect<T>(string id) where T : ArtifactEffectSO
        {
            return LoadOrCreate<T>($"{ArtifactFolder}/Effects/{id}.asset");
        }

        private static void Set(UnityEngine.Object target, string propertyPath, object value)
        {
            if (target == null)
                return;

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                Debug.LogWarning($"Missing serialized property '{propertyPath}' on {target.name}.");
                return;
            }

            Assign(property, value);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = value is int intValue ? intValue : 0;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = value is bool boolValue && boolValue;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = value is float floatValue ? floatValue : 0f;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value as string ?? string.Empty;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = value is Color colorValue ? colorValue : Color.white;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value as UnityEngine.Object;
                    break;
                case SerializedPropertyType.Enum:
                    property.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Generic when property.isArray:
                    var objects = value as UnityEngine.Object[] ?? new UnityEngine.Object[] { };
                    property.arraySize = objects.Length;
                    for (var i = 0; i < objects.Length; i++)
                        property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
                    break;
            }
        }

        private static Sprite LoadFirstSprite(string folder)
        {
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null)
                    return sprite;

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite nestedSprite)
                        return nestedSprite;
                }
            }

            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parts = folder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private readonly struct UnitSpec
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly EntityGrade Grade;
            public readonly int Cost;
            public readonly int MagicCost;
            public readonly bool Locked;
            public readonly int BaseDanger;
            public readonly int DangerIncreaseOnCombat;
            public readonly int MaxHealth;
            public readonly int AttackDamage;
            public readonly float AttackInterval;

            public UnitSpec(string id, string displayName, EntityGrade grade, int cost, int magicCost, bool locked, int baseDanger, int dangerIncreaseOnCombat, int maxHealth, int attackDamage, float attackInterval)
            {
                Id = id;
                DisplayName = displayName;
                Grade = grade;
                Cost = cost;
                MagicCost = magicCost;
                Locked = locked;
                BaseDanger = baseDanger;
                DangerIncreaseOnCombat = dangerIncreaseOnCombat;
                MaxHealth = maxHealth;
                AttackDamage = attackDamage;
                AttackInterval = attackInterval;
            }
        }

        private readonly struct ArtifactSpec
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Description;
            public readonly ArtifactTarget Target;
            public readonly int AttackDamageBonus;
            public readonly float AttackDamageMultiplier;
            public readonly int MaxHealthBonus;
            public readonly float AttackIntervalMultiplier;
            public readonly Color Color;
            public readonly ArtifactEffectSO[] Effects;

            public ArtifactSpec(string id, string displayName, string description, ArtifactTarget target, int attackDamageBonus, float attackDamageMultiplier, int maxHealthBonus, float attackIntervalMultiplier, Color color, params ArtifactEffectSO[] effects)
            {
                Id = id;
                DisplayName = displayName;
                Description = description;
                Target = target;
                AttackDamageBonus = attackDamageBonus;
                AttackDamageMultiplier = attackDamageMultiplier;
                MaxHealthBonus = maxHealthBonus;
                AttackIntervalMultiplier = attackIntervalMultiplier;
                Color = color;
                Effects = effects;
            }
        }

        private readonly struct TrapSpec
        {
            public readonly string Id;
            public readonly string DisplayName;
            public readonly EntityGrade Grade;
            public readonly int Cost;
            public readonly int BaseDanger;
            public readonly string TemplatePath;
            public readonly float TriggerChance;
            public readonly int Damage;
            public readonly float InjuryChance;
            public readonly int BonusDamage;
            public readonly int DangerIncreaseOnTrigger;
            public readonly StatusEffectDataSO StatusEffect;

            public TrapSpec(string id, string displayName, EntityGrade grade, int cost, int baseDanger, string templatePath, float triggerChance, int damage, float injuryChance, int bonusDamage, int dangerIncreaseOnTrigger, StatusEffectDataSO statusEffect)
            {
                Id = id;
                DisplayName = displayName;
                Grade = grade;
                Cost = cost;
                BaseDanger = baseDanger;
                TemplatePath = templatePath;
                TriggerChance = triggerChance;
                Damage = damage;
                InjuryChance = injuryChance;
                BonusDamage = bonusDamage;
                DangerIncreaseOnTrigger = dangerIncreaseOnTrigger;
                StatusEffect = statusEffect;
            }
        }
    }
}
#endif
