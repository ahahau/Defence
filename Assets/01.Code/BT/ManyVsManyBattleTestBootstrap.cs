using System.Collections.Generic;
using System.Reflection;
using _01.Code.Combat;
using _01.Code.Enemies;
using _01.Code.Units;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace _01.Code.BT
{
    /// <summary>Registers placed test agents into one battlefield when the scene starts.</summary>
    public class ManyVsManyBattleTestBootstrap : MonoBehaviour
    {
        [SerializeField] private NodeBattlefield battlefield;
        [SerializeField] private BattleAgent[] agents;
        [Tooltip("Placed Unit_* and Enemy_* prefab agents are used first. If empty, prefabs are spawned at runtime.")]
        [SerializeField] private bool spawnPrefabsWhenNoPlacedAgents = true;
        [Tooltip("For preview stability, disable Unity BehaviorGraphAgent and drive BattleAgent directly.")]
        [SerializeField] private bool forceAutoDriveForPreview = true;
        [SerializeField] private GameObject[] playerPrefabs;
        [SerializeField] private GameObject[] enemyPrefabs;
        [SerializeField, Min(1)] private int simpleAgentsPerTeam = 6;
        [SerializeField] private float playerX = -2.6f;
        [SerializeField] private float enemyX = 2.6f;
        [SerializeField] private float rowSpacing = 0.9f;
        [SerializeField] private float registrationDuration = 1f;

        private float _registerUntil;

        private void Awake()
        {
            EnsureBattlefieldVisual();
        }

        private void Start()
        {
            LoadPrefabsInEditorIfNeeded();

            if ((agents == null || agents.Length == 0) && spawnPrefabsWhenNoPlacedAgents)
                SpawnPrefabAgents();

            if (agents == null || agents.Length == 0)
                SpawnSimpleAgents();

            if (forceAutoDriveForPreview)
                PrepareAgentsForStablePreview();

            PlaceAgents();
            RegisterAgents();
            _registerUntil = Time.time + Mathf.Max(0f, registrationDuration);
            Debug.Log($"Many-vs-many node battle test started. Agents: {(agents != null ? agents.Length : 0)}");
        }

        private void PrepareAgentsForStablePreview()
        {
            if (agents == null) return;

            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (agent == null) continue;

                foreach (var behaviour in agent.GetComponents<MonoBehaviour>())
                {
                    if (behaviour != null && behaviour.GetType().FullName == "Unity.Behavior.BehaviorGraphAgent")
                        behaviour.enabled = false;
                }

                var team = agent.GetComponent<Enemy>() != null ? BattleTeam.Enemy : BattleTeam.Player;
                agent.Configure(team, agent.Role, true);
            }
        }

        private void SpawnAgentsIfNeeded()
        {
            if (agents != null && agents.Length > 0)
                return;

            if (spawnPrefabsWhenNoPlacedAgents)
                SpawnPrefabAgents();
            else
                SpawnSimpleAgents();
        }

        private void SpawnPrefabAgents()
        {
            var spawned = new List<BattleAgent>();
            SpawnTeam(playerPrefabs, "Unit Team", spawned);
            SpawnTeam(enemyPrefabs, "Enemy Team", spawned);

            if (spawned.Count > 0)
                agents = spawned.ToArray();
        }

        private void SpawnSimpleAgents()
        {
            var spawned = new List<BattleAgent>();
            var playerRoot = new GameObject("Unit Team");
            var enemyRoot = new GameObject("Enemy Team");

            for (var i = 0; i < simpleAgentsPerTeam; i++)
            {
                spawned.Add(CreateSimpleAgent(
                    $"Player_{i + 1}",
                    BattleTeam.Player,
                    RoleForIndex(i),
                    new Vector3(playerX, CenteredRowY(i, simpleAgentsPerTeam), 0f),
                    new Color(0.2f, 0.95f, 0.35f, 1f),
                    playerRoot.transform));

                spawned.Add(CreateSimpleAgent(
                    $"Enemy_{i + 1}",
                    BattleTeam.Enemy,
                    RoleForIndex(i),
                    new Vector3(enemyX, CenteredRowY(i, simpleAgentsPerTeam), 0f),
                    new Color(1f, 0.25f, 0.25f, 1f),
                    enemyRoot.transform));
            }

            agents = spawned.ToArray();
        }

        private void SpawnTeam(GameObject[] prefabs, string rootName, List<BattleAgent> spawned)
        {
            if (prefabs == null || prefabs.Length == 0) return;

            var root = new GameObject(rootName);
            for (var i = 0; i < prefabs.Length; i++)
            {
                var prefab = prefabs[i];
                if (prefab == null) continue;

                var instance = Instantiate(prefab, root.transform);
                instance.name = $"{prefab.name}_{i + 1}";
                var agent = instance.GetComponent<BattleAgent>();
                if (agent != null)
                    spawned.Add(agent);
            }
        }

        private BattleAgent CreateSimpleAgent(
            string agentName,
            BattleTeam team,
            BattleRole role,
            Vector3 position,
            Color color,
            Transform parent)
        {
            var go = new GameObject(agentName);
            go.transform.SetParent(parent);
            go.transform.position = position;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = role == BattleRole.Tank
                ? new Vector3(0.65f, 0.65f, 1f)
                : role == BattleRole.Ranged || role == BattleRole.Support
                    ? new Vector3(0.42f, 0.42f, 1f)
                    : new Vector3(0.5f, 0.5f, 1f);

            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSprite;
            renderer.color = color;
            renderer.sortingOrder = team == BattleTeam.Player ? 10 : 11;

            var health = go.AddComponent<Health>();
            health.SetMaxHealth(role == BattleRole.Tank ? 56 : 36, true);

            var combatant = go.AddComponent<Combatant>();
            SetPrivateField(combatant, "health", health);
            combatant.SetAttackDamage(role == BattleRole.Ranged ? 3 : role == BattleRole.Support ? 1 : 2);
            combatant.SetAttackInterval(role == BattleRole.Ranged ? 0.8f : 0.65f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.28f;

            var agent = go.AddComponent<BattleAgent>();
            SetPrivateField(agent, "combatant", combatant);
            SetPrivateField(agent, "body", visual.transform);
            agent.Configure(team, role, true);

            var simpleVisual = go.AddComponent<SimpleBattleTestVisual>();
            simpleVisual.Initialize(health, renderer, color);

            return agent;
        }

        private static BattleRole RoleForIndex(int index)
        {
            return index switch
            {
                0 => BattleRole.Tank,
                1 => BattleRole.Melee,
                2 => BattleRole.Melee,
                3 => BattleRole.Ranged,
                4 => BattleRole.Ranged,
                _ => BattleRole.Support
            };
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private void Update()
        {
            if (Time.time <= _registerUntil)
                RegisterAgents();
        }

        private void PlaceAgents()
        {
            if (agents == null) return;

            var playerIndex = 0;
            var enemyIndex = 0;
            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (agent == null) continue;

                var index = agent.Team == BattleTeam.Player ? playerIndex++ : enemyIndex++;
                var x = agent.Team == BattleTeam.Player ? playerX : enemyX;
                var y = CenteredRowY(index, CountTeam(agent.Team));
                agent.transform.position = new Vector3(x, y, 0f);
            }
        }

        private void RegisterAgents()
        {
            if (battlefield == null || agents == null) return;

            for (var i = 0; i < agents.Length; i++)
            {
                var agent = agents[i];
                if (agent != null && agent.isActiveAndEnabled)
                    battlefield.TryEnter(agent);
            }
        }

        private void EnsureBattlefieldVisual()
        {
            if (battlefield == null) return;
            if (battlefield.GetComponent<LineRenderer>() != null) return;

            var ring = battlefield.gameObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = 0.04f;
            ring.positionCount = 96;
            ring.material = new Material(Shader.Find("Sprites/Default"));
            ring.startColor = new Color(0.25f, 0.85f, 1f, 0.8f);
            ring.endColor = ring.startColor;

            var radius = battlefield.ArenaRadius;
            for (var i = 0; i < ring.positionCount; i++)
            {
                var angle = Mathf.PI * 2f * i / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        private void OnDrawGizmos()
        {
            var center = battlefield != null ? battlefield.transform.position : transform.position;
            var radius = battlefield != null ? battlefield.ArenaRadius : 5.5f;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.color = new Color(0.2f, 0.9f, 0.35f, 0.8f);
            Gizmos.DrawLine(new Vector3(playerX, -2.7f, 0f), new Vector3(playerX, 2.7f, 0f));
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.8f);
            Gizmos.DrawLine(new Vector3(enemyX, -2.7f, 0f), new Vector3(enemyX, 2.7f, 0f));
        }

        private void LoadPrefabsInEditorIfNeeded()
        {
#if UNITY_EDITOR
            if (playerPrefabs == null || playerPrefabs.Length == 0 || playerPrefabs[0] == null)
                playerPrefabs = LoadPrefabs(PlayerPrefabPaths);
            if (enemyPrefabs == null || enemyPrefabs.Length == 0 || enemyPrefabs[0] == null)
                enemyPrefabs = LoadPrefabs(EnemyPrefabPaths);
#endif
        }

#if UNITY_EDITOR
        private static readonly string[] PlayerPrefabPaths =
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

        private static GameObject[] LoadPrefabs(string[] paths)
        {
            var prefabs = new GameObject[paths.Length];
            for (var i = 0; i < paths.Length; i++)
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            return prefabs;
        }
#endif

        private int CountTeam(BattleTeam team)
        {
            if (agents == null) return 0;

            var count = 0;
            for (var i = 0; i < agents.Length; i++)
            {
                if (agents[i] != null && agents[i].Team == team)
                    count++;
            }
            return count;
        }

        private float CenteredRowY(int index, int count)
        {
            if (count <= 1) return 0f;
            return (index - (count - 1) * 0.5f) * rowSpacing;
        }

        private static Sprite _squareSprite;

        private static Sprite SquareSprite
        {
            get
            {
                if (_squareSprite == null)
                {
                    var tex = Texture2D.whiteTexture;
                    _squareSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
                }
                return _squareSprite;
            }
        }
    }

    public class SimpleBattleTestVisual : MonoBehaviour
    {
        private Health _health;
        private SpriteRenderer _renderer;
        private Color _aliveColor;

        public void Initialize(Health health, SpriteRenderer spriteRenderer, Color aliveColor)
        {
            _health = health;
            _renderer = spriteRenderer;
            _aliveColor = aliveColor;
            if (_health != null)
                _health.Changed += OnHealthChanged;
            OnHealthChanged(_health != null ? _health.CurrentRatio : 1f);
        }

        private void OnDestroy()
        {
            if (_health != null)
                _health.Changed -= OnHealthChanged;
        }

        private void OnHealthChanged(float ratio)
        {
            if (_renderer == null) return;

            if (ratio <= 0f)
            {
                _renderer.color = new Color(0.18f, 0.18f, 0.18f, 0.65f);
                transform.localScale = Vector3.one * 0.75f;
                return;
            }

            _renderer.color = Color.Lerp(Color.black, _aliveColor, Mathf.Clamp01(0.35f + ratio * 0.65f));
            transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1f, ratio);
        }
    }
}
