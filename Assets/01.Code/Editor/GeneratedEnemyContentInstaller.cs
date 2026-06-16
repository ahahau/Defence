#if UNITY_EDITOR
using System;
using _01.Code.BT;
using _01.Code.Enemies;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    /// <summary>
    /// 적 종류별 프리팹을 Enemy.prefab 템플릿에서 생성한다(유닛 생성기와 동일한 방식).
    /// 역할에 맞춰 BattleAgent의 시야/이동속도/위협 등을 차등 설정하고, 각 EnemyDataSO에 프리팹을 연결한다.
    /// 스탯(HP/공격/스프라이트)은 런타임에 Enemy.ApplyData가 데이터에서 적용하므로 여기선 건드리지 않는다.
    /// </summary>
    public static class GeneratedEnemyContentInstaller
    {
        private const string EnemyTemplatePath = "Assets/04.Prefab/Characters/Enemy.prefab";
        private const string EnemyPrefabFolder = "Assets/04.Prefab/Characters/Generated";
        private const string EnemyDataFolder = "Assets/03.SO/Enemies";

        private readonly struct EnemySpec
        {
            public readonly string Id;
            public readonly string DataAsset;
            public readonly BattleRole Role;
            public readonly float SenseRange;
            public readonly float MoveSpeed;
            public readonly float ThreatWeight;
            public readonly float Regen;
            public readonly float SupportRange;
            public readonly int SupportAmount;

            public EnemySpec(string id, string dataAsset, BattleRole role, float senseRange,
                float moveSpeed, float threatWeight, float regen, float supportRange, int supportAmount)
            {
                Id = id;
                DataAsset = dataAsset;
                Role = role;
                SenseRange = senseRange;
                MoveSpeed = moveSpeed;
                ThreatWeight = threatWeight;
                Regen = regen;
                SupportRange = supportRange;
                SupportAmount = supportAmount;
            }
        }

        [MenuItem("Defence/BT/Install Enemy Role Prefabs")]
        public static void Install()
        {
            EnsureFolder(EnemyPrefabFolder);

            // id, dataAsset, role, sense, moveSpeed, threat, regen, supportRange, supportAmount
            var specs = new[]
            {
                new EnemySpec("Scout",   "BasicEnemy",   BattleRole.Melee,   6f, 3.5f,  6f, 0f, 4f,   0),
                new EnemySpec("Archer",  "ArcherEnemy",  BattleRole.Ranged,  8f, 3f,    0f, 0f, 4f,   0),
                new EnemySpec("Healter", "HealterEnemy", BattleRole.Support, 7f, 3f,    0f, 0f, 4.5f, 2),
                new EnemySpec("Sword",   "SwordEnemy",   BattleRole.Tank,    5f, 2.5f, 16f, 2f, 4f,   0),
            };

            var created = 0;
            foreach (var spec in specs)
            {
                var data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>($"{EnemyDataFolder}/{spec.DataAsset}.asset");
                if (data == null)
                {
                    Debug.LogWarning($"Enemy data not found: {spec.DataAsset}.asset");
                    continue;
                }

                var prefab = CreateEnemyPrefab(spec, data);
                if (prefab == null)
                    continue;

                Set(data, "<Prefab>k__BackingField", prefab.GetComponent<Enemy>());
                EditorUtility.SetDirty(data);
                created++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Enemy role prefabs installed: {created}. " +
                      "Next: run 'Defence/BT/Configure Battle Prefabs', assign EnemyBT to each, and add the prefabs to WaveManager's enemy pool if needed.");
        }

        private static GameObject CreateEnemyPrefab(EnemySpec spec, EnemyDataSO data)
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyTemplatePath);
            if (template == null)
            {
                Debug.LogError($"Enemy template not found at {EnemyTemplatePath}");
                return null;
            }

            var path = $"{EnemyPrefabFolder}/Enemy_{spec.Id}.prefab";
            if (!AssetDatabase.CopyAsset(EnemyTemplatePath, path)
                && AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return null;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = $"Enemy_{spec.Id}";

                var enemy = root.GetComponent<Enemy>();
                if (enemy != null)
                    Set(enemy, "data", data);

                var agent = root.GetComponent<BattleAgent>();
                if (agent != null)
                {
                    Set(agent, "role", (int)spec.Role);
                    Set(agent, "senseRange", spec.SenseRange);
                    Set(agent, "moveSpeed", spec.MoveSpeed);
                    Set(agent, "threatWeight", spec.ThreatWeight);
                    Set(agent, "outOfCombatRegenPerSecond", spec.Regen);
                    Set(agent, "supportHealRange", spec.SupportRange);
                    Set(agent, "supportHealAmount", spec.SupportAmount);
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
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

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = value is int i ? i : Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Float:
                    property.floatValue = value is float f ? f : Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.Boolean:
                    property.boolValue = value is bool b && b;
                    break;
                case SerializedPropertyType.String:
                    property.stringValue = value as string ?? string.Empty;
                    break;
                case SerializedPropertyType.Enum:
                    property.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = value as UnityEngine.Object;
                    break;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
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
    }
}
#endif
