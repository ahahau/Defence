using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace _01.Code.MapCreateSystem
{
    [ExecuteAlways]
    public class DungeonGraphController : MonoBehaviour
    {
        private readonly Vector2Int[] directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };

        [Header("Layout")]
        [SerializeField]
        private float gridSpacing = 2.4f;

        [SerializeField]
        private float nodeSize = 1f;

        [Header("Prefabs")]
        [SerializeField]
        private Node nodePrefab;

        [SerializeField]
        private EdgeLine edgeLinePrefab;

        [Header("Build")]
        [SerializeField]
        private DungeonNodeType selectedType = DungeonNodeType.Corridor;

        [SerializeField]
        private int buildGoldCost = 10;

        [Header("Main Unit")]
        [SerializeField]
        private MainUnit mainUnitPrefab;

        [SerializeField]
        private GameEventChannelSO gameStateEventChannel;

        [SerializeField]
        private GameEventChannelSO artifactEventChannel;

        [Header("Events")]
        [SerializeField] private GameEventChannelSO costEventChannel;

        [SerializeField] private GameEventChannelSO nodeEventChannel;

        [Header("Input")]
        [SerializeField]
        private InputDataSO inputDataSO;

        [SerializeField]
        private LayerMask nodeClickMask = Physics2D.DefaultRaycastLayers;

        [Header("UI Blocking")]
        [SerializeField]
        private RectTransform nodePanelBlockRect;

        private DungeonGraph graph;
        private DungeonGraphView view;
        private readonly Dictionary<Collider2D, Node> lockedNodeByCollider = new();
        private readonly Dictionary<Collider2D, Node> unlockedNodeByCollider = new();
        private readonly List<DungeonNode> buildParentCandidates = new();
        private const string UnitsRootName = "Units";
        private Node lastBuiltNodeView;
        private int lastBuiltFrame = -1;
        private bool hasPendingMouseInput;
        private bool hasPendingRightMouseInput;
        private Transform unitsRoot;

        public bool HasLockedNodesVisible { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            costEventChannel.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            inputDataSO.OnMouseInputEvent += HandleMouseInput;
        }

        

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            costEventChannel.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            inputDataSO.OnMouseInputEvent -= HandleMouseInput;
        }

        private void Awake()
        {
            RebuildInitialGraph();
            ShowLockedNodes();
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            if (!hasPendingMouseInput)
            {
                if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                    hasPendingRightMouseInput = true;

                if (!hasPendingRightMouseInput)
                    return;
            }

            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                hasPendingRightMouseInput = true;

            if (hasPendingRightMouseInput)
            {
                hasPendingRightMouseInput = false;
                ProcessRightMouseInput();
                hasPendingMouseInput = false;
                return;
            }

            if (hasPendingMouseInput)
            {
                hasPendingMouseInput = false;
                ProcessMouseInput();
            }
        }

        [ContextMenu("Rebuild Initial Graph")]
        public void RebuildInitialGraph()
        {
            graph = new DungeonGraph();
            view = new DungeonGraphView(transform, nodePrefab, edgeLinePrefab, gridSpacing, nodeSize);
            view.ClearAll();
            lockedNodeByCollider.Clear();
            unlockedNodeByCollider.Clear();
            ClearUnitsRoot();

            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            var entranceView = view.CreateNode(entrance);
            RegisterUnlockedNode(entranceView);
            CreateMainUnit(entranceView);

            var treasury = graph.AddNode(DungeonNodeType.Treasury, Vector2Int.right);
            graph.Connect(entrance, treasury);
            var treasuryView = view.CreateNode(treasury);
            RegisterUnlockedNode(treasuryView);
            view.CreateEdge(entrance.GridPosition, treasury.GridPosition);

            HasLockedNodesVisible = false;
        }

        private void CreateMainUnit(Node entranceNode)
        {
            if (!Application.isPlaying)
                return;

            var spawnPosition = entranceNode.UnitPosition.position;

            MainUnit mainUnit = Instantiate(mainUnitPrefab, spawnPosition, Quaternion.identity);
            mainUnit.transform.SetParent(GetOrCreateUnitsRoot(), true);
            mainUnit.transform.position = spawnPosition;
            mainUnit.transform.localScale = Vector3.one;
            mainUnit.name = "Player";
            mainUnit.Initialize(null);
            mainUnit.InitializeMainUnit(gameStateEventChannel);

            entranceNode.AssignUnit(null, mainUnit);
            artifactEventChannel.RaiseEvent(new UnitArtifactApplyRequestedEvent(mainUnit));
        }

        private Transform GetOrCreateUnitsRoot()
        {
            if (unitsRoot != null)
                return unitsRoot;

            var existingRoot = transform.Find(UnitsRootName);
            if (existingRoot != null)
            {
                unitsRoot = existingRoot;
                return unitsRoot;
            }

            var rootObject = new GameObject(UnitsRootName);
            unitsRoot = rootObject.transform;
            unitsRoot.SetParent(transform);
            unitsRoot.localPosition = Vector3.zero;
            unitsRoot.localRotation = Quaternion.identity;
            unitsRoot.localScale = Vector3.one;
            return unitsRoot;
        }

        private void ClearUnitsRoot()
        {
            var existingRoot = transform.Find(UnitsRootName);
            if (existingRoot == null)
            {
                unitsRoot = null;
                return;
            }

            if (Application.isPlaying)
            {
                existingRoot.name = $"{UnitsRootName}_Destroying";
                existingRoot.SetParent(null);
                Destroy(existingRoot.gameObject);
            }
            else
            {
                DestroyImmediate(existingRoot.gameObject);
            }

            unitsRoot = null;
        }

        [ContextMenu("Show Locked Nodes")]
        public void ShowLockedNodes()
        {
            HasLockedNodesVisible = true;
            RefreshLockedNodes();
        }
        
        [ContextMenu("Hide Locked Nodes")]
        public void HideLockedNodes()
        {
            HasLockedNodesVisible = false;
            lockedNodeByCollider.Clear();
            view.ClearLockedNodes();
        }

        public void TryBuildAt(Node lockedNode)
        {
            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            costEventChannel.RaiseEvent(new BuildCostRequestedEvent(lockedNode, buildGoldCost));
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            var lockedNode = evt.Node;
            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;
            var type = ResolveBuildType(lockedNode);
            var node = graph.AddNode(type, lockedNode.GridPosition);
            var nodeView = view.CreateNode(node);

            lastBuiltNodeView = nodeView;
            lastBuiltFrame = Time.frameCount;
            RegisterUnlockedNode(nodeView);
            ConnectAdjacentNodes(node, lockedNode.FromNode);

            if (HasLockedNodesVisible)
                RefreshLockedNodes();
        }

        public void SelectBuildType(DungeonNodeType type)
        {
            if (type != DungeonNodeType.Entrance)
                selectedType = type;
        }

        private DungeonNodeType ResolveBuildType(Node lockedNode)
        {
            if (lockedNode.FromNode.Type == DungeonNodeType.Entrance && graph.Nodes.Count == 1)
                return DungeonNodeType.Corridor;

            return selectedType;
        }

        private void ConnectAdjacentNodes(DungeonNode node, DungeonNode preferredFirstNode)
        {
            TryConnectNodes(preferredFirstNode, node);

            foreach (var direction in directions)
            {
                var adjacentPosition = node.GridPosition + direction;
                if (!graph.TryGetNodeAt(adjacentPosition, out var adjacentNode))
                    continue;

                if (adjacentNode == preferredFirstNode)
                    continue;

                TryConnectNodes(adjacentNode, node);
            }
        }

        private void TryConnectNodes(DungeonNode fromNode, DungeonNode toNode)
        {
            if (!graph.Connect(fromNode, toNode))
                return;

            view.CreateEdge(fromNode.GridPosition, toNode.GridPosition);
        }

        public void RegisterLockedNode(Node lockedNode)
        {
            lockedNodeByCollider.Add(lockedNode.ClickCollider, lockedNode);
        }

        public void RegisterUnlockedNode(Node unlockedNode)
        {
            unlockedNodeByCollider.Add(unlockedNode.ClickCollider, unlockedNode);
        }

        private void RefreshLockedNodes()
        {
            lockedNodeByCollider.Clear();
            view.ClearLockedNodes();

            var usedLockedNodePositions = new HashSet<Vector2Int>();
            foreach (var node in graph.Nodes)
            {
                foreach (var direction in directions)
                {
                    var position = node.GridPosition + direction;
                    if (graph.IsOccupied(position) || !usedLockedNodePositions.Add(position))
                        continue;

                    if (!TryResolveBuildParent(position, out var parentNode))
                        continue;

                    view.CreateLockedNode(this, parentNode, position, position - parentNode.GridPosition);
                }
            }
        }

        private bool TryResolveBuildParent(Vector2Int position, out DungeonNode parentNode)
        {
            parentNode = null;
            foreach (var direction in directions)
            {
                var adjacentPosition = position + direction;
                if (!graph.TryGetNodeAt(adjacentPosition, out var adjacentNode))
                    continue;

                if (adjacentNode.FreePorts <= 0)
                    continue;

                if (parentNode == null || adjacentNode.FreePorts > parentNode.FreePorts)
                    parentNode = adjacentNode;
            }

            if (parentNode == null)
                return false;

            return true;
        }

        private void HandleMouseInput()
        {
            hasPendingMouseInput = true;
        }

        private void ProcessMouseInput()
        {
            if (IsPointerOverUi() || IsPointerOverNodePanel())
                return;

            Vector2 worldPosition = inputDataSO.SceneToWorldPoint();
            var clickedCollider = Physics2D.OverlapPoint(worldPosition, nodeClickMask);

            if (clickedCollider == null)
                return;

            if (!lockedNodeByCollider.TryGetValue(clickedCollider, out var lockedNode))
            {
                if (!unlockedNodeByCollider.TryGetValue(clickedCollider, out var unlockedNode))
                    return;

                nodeEventChannel.RaiseEvent(new NodeCameraFocusStartedEvent(unlockedNode));
                nodeEventChannel.RaiseEvent(new UnlockedNodeClickedEvent(unlockedNode));
                return;
            }

            TryBuildAt(lockedNode);
        }

        private void ProcessRightMouseInput()
        {
            if (IsPointerOverUi())
                return;

            Vector2 worldPosition = inputDataSO.SceneToWorldPoint();
            var clickedCollider = Physics2D.OverlapPoint(worldPosition, nodeClickMask);

            if (clickedCollider == null)
                return;

            if (!unlockedNodeByCollider.TryGetValue(clickedCollider, out var unlockedNode))
                return;

            if (!unlockedNode.HasAssignedUnit)
                return;

            nodeEventChannel.RaiseEvent(new UnitStatusRequestedEvent(unlockedNode));
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private bool IsPointerOverNodePanel()
        {
            if (nodePanelBlockRect == null || !nodePanelBlockRect.gameObject.activeInHierarchy)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(
                nodePanelBlockRect,
                inputDataSO.ReadScreenMousePosition(),
                null);
        }

    }
}
