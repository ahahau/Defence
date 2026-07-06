using System;
using System.IO;
using _01.Code.BT;
using _01.Code.Combat;
using _01.Code.Enemies;
using _01.Code.MapCreateSystem;
using _01.Code.StatusEffects;
using _01.Code.Units;
using Unity.Behavior;
using UnityEditor;
using UnityEngine;

namespace _01.Code.Editor
{
    public static class BattleBehaviorPrefabInstaller
    {
        private const string CharacterFolder = "Assets/04.Prefab/Characters";
        private const string NodePrefabPath = "Assets/04.Prefab/Map/Node.prefab";
        private const string SessionSetupKey = "Defence.BT.PrefabSetup.20260715.StructuralDependencies";

        [InitializeOnLoadMethod]
        private static void ScheduleInitialSetup()
        {
            if (SessionState.GetBool(SessionSetupKey, false))
                return;

            SessionState.SetBool(SessionSetupKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    ConfigureBattlePrefabs();
            };
        }

        [MenuItem("Defence/BT/Configure Battle Prefabs")]
        public static void ConfigureBattlePrefabs()
        {
            var unitGraph = FindGraphByName("UnitBT");
            var enemyGraph = FindGraphByName("EnemyBT");
            if (unitGraph == null || enemyGraph == null)
                throw new InvalidOperationException("UnitBT and EnemyBT assets must both exist before configuring character prefabs.");
            var configured = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { CharacterFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (ConfigureCharacterPrefab(path, unitGraph, enemyGraph))
                    configured++;
            }

            ConfigureNodePrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Battle prefab setup complete. Characters: {configured}. " +
                      $"UnitBT: {(unitGraph != null ? "assigned" : "MISSING")}, " +
                      $"EnemyBT: {(enemyGraph != null ? "assigned" : "MISSING")}. " +
                      "Auto Drive disabled where a graph was assigned.");
        }

        private static bool ConfigureCharacterPrefab(string path, BehaviorGraph unitGraph, BehaviorGraph enemyGraph)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var enemy = root.GetComponentInChildren<Enemy>(true);
                var unit = root.GetComponentInChildren<Unit>(true);
                var owner = enemy != null ? enemy.gameObject : unit != null ? unit.gameObject : null;
                if (owner == null)
                    return false;

                var battleAgent = owner.GetComponent<BattleAgent>();
                if (battleAgent == null)
                    battleAgent = owner.AddComponent<BattleAgent>();

                var team = enemy != null ? BattleTeam.Enemy : BattleTeam.Player;
                var graph = enemy != null ? enemyGraph : unitGraph;
                var role = ResolveRole(path);
                battleAgent.Configure(team, role, graph == null);

                var rigidbody = owner.GetComponent<Rigidbody2D>();
                if (rigidbody == null)
                    rigidbody = owner.AddComponent<Rigidbody2D>();
                rigidbody.bodyType = RigidbodyType2D.Kinematic;
                rigidbody.simulated = true;
                rigidbody.gravityScale = 0f;
                rigidbody.freezeRotation = true;

                if (owner.GetComponent<Collider2D>() == null)
                {
                    var collider = owner.AddComponent<CircleCollider2D>();
                    collider.radius = 0.4f;
                }

                if (owner.GetComponent<CombatStatusController>() == null)
                    owner.AddComponent<CombatStatusController>();

                if (enemy != null)
                {
                    if (owner.GetComponent<EnemyStatusController>() == null)
                        owner.AddComponent<EnemyStatusController>();
                    if (owner.GetComponent<EnemyClickTarget>() == null)
                        owner.AddComponent<EnemyClickTarget>();
                }
                else if (owner.GetComponent<UnitClickTarget>() == null)
                {
                    owner.AddComponent<UnitClickTarget>();
                }

                var behaviorAgent = owner.GetComponent<BehaviorGraphAgent>();
                if (behaviorAgent == null)
                    behaviorAgent = owner.AddComponent<BehaviorGraphAgent>();
                behaviorAgent.enabled = true;
                if (graph != null)
                    behaviorAgent.Graph = graph;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureNodePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(NodePrefabPath);
            try
            {
                var node = root.GetComponentInChildren<_01.Code.MapCreateSystem.Node>(true);
                if (node == null)
                    throw new InvalidOperationException($"Node component not found in {NodePrefabPath}");

                var battlefield = node.GetComponent<NodeBattlefield>();
                if (battlefield == null)
                    battlefield = node.gameObject.AddComponent<NodeBattlefield>();

                var serialized = new SerializedObject(battlefield);
                serialized.FindProperty("maxPerTeam").intValue = 3;
                serialized.FindProperty("arenaRadius").floatValue = 4f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var collider = node.GetComponent<Collider2D>();
                if (collider == null)
                    collider = node.gameObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;

                if (node.GetComponent<NodeTrapGrid>() == null)
                    node.gameObject.AddComponent<NodeTrapGrid>();

                PrefabUtility.SaveAsPrefabAsset(root, NodePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static BattleRole ResolveRole(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);

            // 전열 탱커(높은 HP/방어) — 적 어그로를 끌어 후열 보호.
            if (name.Contains("Guardian", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Vanguard", StringComparison.OrdinalIgnoreCase))
                return BattleRole.Tank;

            // 후열 원거리 딜러.
            if (name.Contains("Arbalist", StringComparison.OrdinalIgnoreCase)
                || name.Contains("BattleMage", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Scout", StringComparison.OrdinalIgnoreCase))
                return BattleRole.Ranged;

            return BattleRole.Melee;
        }

        private static BehaviorGraph FindGraphByName(string graphName)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:BehaviorGraph"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.Equals(Path.GetFileNameWithoutExtension(path), graphName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var graph = AssetDatabase.LoadAssetAtPath<BehaviorGraph>(path);
                if (graph != null)
                    return graph;
            }

            return null;
        }

    }
}
