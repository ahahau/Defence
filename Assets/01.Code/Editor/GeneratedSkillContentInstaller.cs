using System.IO;
using _01.Code.Skills;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    public static class GeneratedSkillContentInstaller
    {
        private const string SkillFolder = "Assets/03.SO/Skills/Generated";
        private const string UnitFolder = "Assets/04.Prefab/Characters/Generated";

        [MenuItem("Defence/Skills/Install Generated Skills")]
        public static void Install()
        {
            EnsureFolder("Assets/03.SO");
            EnsureFolder("Assets/03.SO/Skills");
            EnsureFolder(SkillFolder);

            var backstab = CreateSkill(
                "Skill_ShadowBackstab",
                "Shadow Backstab",
                "Blink behind the target, deal bonus damage, then make the target take more damage briefly.",
                4.5f,
                false,
                CreateEffect<AssassinBackstabSkillEffectSO>("Effect_ShadowBackstab", e =>
                {
                    Set(e, "behindDistance", 0.7f);
                    Set(e, "flatDamage", 7);
                    Set(e, "attackDamageMultiplier", 1.6f);
                    Set(e, "vulnerableDamageMultiplier", 1.35f);
                    Set(e, "vulnerableDuration", 2.2f);
                }));

            var poisonZone = CreateSkill(
                "Skill_PoisonField",
                "Poison Field",
                "Create a toxic ground zone that slows enemies and deals repeated damage.",
                6f,
                false,
                CreateEffect<GroundZoneSkillEffectSO>("Effect_PoisonField", e =>
                {
                    Set(e, "radius", 2.2f);
                    Set(e, "duration", 3.2f);
                    Set(e, "tickInterval", 0.45f);
                    Set(e, "tickDamage", 1);
                    Set(e, "moveSpeedMultiplier", 0.42f);
                    Set(e, "zoneColor", new Color(0.35f, 0.9f, 0.18f, 0.32f));
                }));

            var frostZone = CreateSkill(
                "Skill_FrostField",
                "Frost Field",
                "Create an icy ground zone that heavily slows enemies and breaks their formation.",
                7f,
                false,
                CreateEffect<GroundZoneSkillEffectSO>("Effect_FrostField", e =>
                {
                    Set(e, "radius", 2.8f);
                    Set(e, "duration", 3.5f);
                    Set(e, "tickInterval", 0.6f);
                    Set(e, "tickDamage", 1);
                    Set(e, "moveSpeedMultiplier", 0.32f);
                    Set(e, "zoneColor", new Color(0.3f, 0.75f, 1f, 0.3f));
                }));

            var shockwave = CreateSkill(
                "Skill_ShieldShockwave",
                "Shield Shockwave",
                "Damage nearby enemies and push them away from the caster.",
                5.5f,
                false,
                CreateEffect<ShockwaveSkillEffectSO>("Effect_ShieldShockwave", e =>
                {
                    Set(e, "radius", 2.35f);
                    Set(e, "knockbackDistance", 1.2f);
                    Set(e, "damage", 4);
                }));

            var mark = CreateSkill(
                "Skill_ExposeWeakness",
                "Expose Weakness",
                "Mark nearby enemies so they briefly move slower and take more follow-up damage.",
                6f,
                false,
                CreateEffect<ApplyCombatStatusSkillEffectSO>("Effect_ExposeWeakness", e =>
                {
                    Set(e, "statusId", "ExposeWeakness");
                    Set(e, "duration", 3f);
                    Set(e, "moveSpeedMultiplier", 0.9f);
                    Set(e, "damageTakenMultiplier", 1.35f);
                    Set(e, "affectAllEnemies", true);
                    Set(e, "radius", 2.8f);
                }));

            var heal = CreateSkill(
                "Skill_RallyHeal",
                "Rally Heal",
                "Restore a nearby ally so support units do more than basic attacking.",
                5f,
                false,
                CreateEffect<HealSkillEffectSO>("Effect_RallyHeal", e =>
                {
                    Set(e, "healAmount", 6);
                    Set(e, "healAllAllies", false);
                }));

            var sanctuary = CreateSkill(
                "Skill_SanctuaryField",
                "Sanctuary Field",
                "Create a blessed zone that heals allies standing inside and reduces damage they take.",
                7.5f,
                false,
                CreateEffect<HealZoneSkillEffectSO>("Effect_SanctuaryField", e =>
                {
                    Set(e, "radius", 2.4f);
                    Set(e, "duration", 4f);
                    Set(e, "tickInterval", 0.8f);
                    Set(e, "tickHeal", 2);
                    Set(e, "damageTakenMultiplier", 0.85f);
                    Set(e, "zoneColor", new Color(0.45f, 0.95f, 0.6f, 0.28f));
                }));

            var meteor = CreateSkill(
                "Skill_MeteorStrike",
                "Meteor Strike",
                "Mark the target's ground, then blast the area for heavy damage and knockback after a short delay.",
                8f,
                false,
                CreateEffect<DelayedBlastZoneSkillEffectSO>("Effect_MeteorStrike", e =>
                {
                    Set(e, "radius", 1.9f);
                    Set(e, "delay", 0.9f);
                    Set(e, "flatDamage", 8);
                    Set(e, "attackDamageMultiplier", 0.6f);
                    Set(e, "knockbackDistance", 0.7f);
                }));

            AssignSkill("Unit_Vanguard.prefab", shockwave);
            AssignSkill("Unit_Guardian.prefab", sanctuary);
            AssignSkill("Unit_Pikeman.prefab", mark);
            AssignSkill("Unit_Arbalist.prefab", poisonZone);
            AssignSkill("Unit_BattleMage.prefab", meteor);
            AssignSkill("Unit_Scout.prefab", backstab);

            AssignSkill("Enemy_Sword.prefab", shockwave);
            AssignSkill("Enemy_Archer.prefab", poisonZone);
            AssignSkill("Enemy_Scout.prefab", backstab);
            AssignSkill("Enemy_Healter.prefab", heal);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated skill content installed.");
        }

        private static SkillDataSO CreateSkill(string assetName, string displayName, string description, float cooldown, bool isUltimate, params SkillEffectSO[] effects)
        {
            var path = $"{SkillFolder}/{assetName}.asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillDataSO>(path);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<SkillDataSO>();
                AssetDatabase.CreateAsset(skill, path);
            }

            Set(skill, "<DisplayName>k__BackingField", displayName);
            Set(skill, "<Description>k__BackingField", description);
            Set(skill, "<Cooldown>k__BackingField", cooldown);
            Set(skill, "<IsUltimate>k__BackingField", isUltimate);
            SetArray(skill, "<Effects>k__BackingField", effects);
            EditorUtility.SetDirty(skill);
            return skill;
        }

        private static T CreateEffect<T>(string assetName, System.Action<T> configure) where T : SkillEffectSO
        {
            var path = $"{SkillFolder}/{assetName}.asset";
            var effect = AssetDatabase.LoadAssetAtPath<T>(path);
            if (effect == null)
            {
                effect = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(effect, path);
            }

            configure?.Invoke(effect);
            EditorUtility.SetDirty(effect);
            return effect;
        }

        private static void AssignSkill(string prefabName, SkillDataSO skill)
        {
            var path = $"{UnitFolder}/{prefabName}";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var caster = root.GetComponent<SkillCaster>();
                if (caster == null)
                    caster = root.AddComponent<SkillCaster>();

                var serialized = new SerializedObject(caster);
                serialized.FindProperty("skill").objectReferenceValue = skill;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Set(UnityEngine.Object target, string propertyName, object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogWarning($"Missing serialized property {propertyName} on {target.name}");
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = (int)value;
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = (bool)value;
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = (float)value;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Color:
                    property.colorValue = (Color)value;
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value as UnityEngine.Object;
                    break;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(UnityEngine.Object target, string propertyName, SkillEffectSO[] effects)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray)
                return;

            property.arraySize = effects != null ? effects.Length : 0;
            for (var i = 0; effects != null && i < effects.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = effects[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
