using System.Collections.Generic;
using _01.Code.BT;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _01.Code.Editor
{
    public static class ManyVsManyBattleTestSceneBuilder
    {
        private const string ScenePath = "Assets/00.Scenes/ManyVsManyBattleTest.unity";
        private const float ArenaRadius = 5.5f;
        private const string AutoRefreshSessionKey = "Defence.ManyVsManyBattleTest.AutoRefresh.20260701.UnitPrefabs";

        private static readonly string[] UnitPrefabPaths =
        {
            "Assets/04.Prefab/Characters/Generated/Unit_Vanguard.prefab",
            "Assets/04.Prefab/Characters/Generated/Unit_Guardian.prefab",
            "Assets/04.Prefab/Characters/Generated/Unit_Pikeman.prefab",
            "Assets/04.Prefab/Characters/Generated/Unit_Arbalist.prefab",
            "Assets/04.Prefab/Characters/Generated/Unit_BattleMage.prefab",
            "Assets/04.Prefab/Characters/Generated/Unit_Scout.prefab"
        };

        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/04.Prefab/Characters/Generated/Enemy_Sword.prefab",
            "Assets/04.Prefab/Characters/Generated/Enemy_Archer.prefab",
            "Assets/04.Prefab/Characters/Generated/Enemy_Scout.prefab",
            "Assets/04.Prefab/Characters/Generated/Enemy_Healter.prefab",
            "Assets/04.Prefab/Characters/Generated/Enemy_Sword.prefab",
            "Assets/04.Prefab/Characters/Generated/Enemy_Archer.prefab"
        };

        [MenuItem("Defence/BT/Create Many Vs Many Test Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PopulateScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created many-vs-many node battle test scene: {ScenePath}");
        }

        [MenuItem("Defence/BT/Rebuild Open Many Vs Many Test Scene")]
        public static void RebuildOpenScene()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ClearScene();
            PopulateScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Rebuilt open many-vs-many node battle test scene: {ScenePath}");
        }

        [InitializeOnLoadMethod]
        private static void AutoRefreshOpenTestScene()
        {
            if (SessionState.GetBool(AutoRefreshSessionKey, false))
                return;

            SessionState.SetBool(AutoRefreshSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                var activeScene = EditorSceneManager.GetActiveScene();
                if (activeScene.path != ScenePath)
                    return;

                RebuildOpenScene();
            };
        }

        private static void PopulateScene()
        {
            CreateCamera();
            CreateArena(out var battlefield);
            var agents = new List<BattleAgent>();
            InstantiateTeam(UnitPrefabPaths, "Unit Team", -2.6f, agents);
            InstantiateTeam(EnemyPrefabPaths, "Enemy Team", 2.6f, agents);
            CreateBootstrap(battlefield, agents);
        }

        private static void ClearScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (var i = roots.Length - 1; i >= 0; i--)
                Object.DestroyImmediate(roots[i]);
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.tag = "MainCamera";
        }

        private static void CreateArena(out NodeBattlefield battlefield)
        {
            var arena = new GameObject("Many Vs Many Battlefield");
            arena.transform.position = Vector3.zero;
            var collider = arena.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = ArenaRadius;

            battlefield = arena.AddComponent<NodeBattlefield>();
            var serialized = new SerializedObject(battlefield);
            serialized.FindProperty("maxPerTeam").intValue = 8;
            serialized.FindProperty("arenaRadius").floatValue = ArenaRadius;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var ring = arena.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = 0.04f;
            ring.positionCount = 96;
            ring.material = new Material(Shader.Find("Sprites/Default"));
            ring.startColor = new Color(0.25f, 0.85f, 1f, 0.75f);
            ring.endColor = ring.startColor;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = Mathf.PI * 2f * i / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * ArenaRadius, Mathf.Sin(angle) * ArenaRadius, 0f));
            }
        }

        private static void InstantiateTeam(string[] prefabPaths, string rootName, float x, List<BattleAgent> agents)
        {
            var root = new GameObject(rootName);
            root.transform.position = Vector3.zero;
            for (var i = 0; i < prefabPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPaths[i]);
                if (prefab == null)
                {
                    Debug.LogWarning($"Missing prefab for BT test scene: {prefabPaths[i]}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = $"{prefab.name}_{i + 1}";
                instance.transform.SetParent(root.transform);
                instance.transform.position = new Vector3(x, CenteredRowY(i, prefabPaths.Length), 0f);

                var agent = instance.GetComponent<BattleAgent>();
                if (agent != null)
                    agents.Add(agent);

                var graphAgent = instance.GetComponent("BehaviorGraphAgent") as Behaviour;
                if (graphAgent != null)
                    graphAgent.enabled = false;
            }
        }

        private static void CreateBootstrap(NodeBattlefield battlefield, List<BattleAgent> agents)
        {
            var bootstrapObject = new GameObject("Many Vs Many Test Bootstrap");
            var bootstrap = bootstrapObject.AddComponent<ManyVsManyBattleTestBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("battlefield").objectReferenceValue = battlefield;
            serialized.FindProperty("spawnPrefabsWhenNoPlacedAgents").boolValue = true;
            serialized.FindProperty("forceAutoDriveForPreview").boolValue = true;

            var agentsProperty = serialized.FindProperty("agents");
            agentsProperty.arraySize = agents.Count;
            for (var i = 0; i < agents.Count; i++)
                agentsProperty.GetArrayElementAtIndex(i).objectReferenceValue = agents[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static float CenteredRowY(int index, int count)
        {
            if (count <= 1) return 0f;
            return (index - (count - 1) * 0.5f) * 0.9f;
        }
    }
}
