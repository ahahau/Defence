using System.Collections.Generic;
using _01.Code.Core;
using _01.Code.Events;
using UnityEngine;

namespace _01.Code.MapCreateSystem
{
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
        [field: SerializeField]
        public float GridSpacing { get; private set; } = 2.4f;

        [field: SerializeField]
        public float NodeSize { get; private set; } = 1f;

        [Header("Prefabs")]
        [field: SerializeField]
        public Node NodePrefab { get; private set; }

        [field: SerializeField]
        public EdgeLine EdgeLinePrefab { get; private set; }

        [Header("Build")]
        [field: SerializeField]
        public DungeonNodeType SelectedType { get; private set; } = DungeonNodeType.Corridor;

        [field: SerializeField]
        public int BuildGoldCost { get; private set; } = 10;

        [Header("Events")]
        [field: SerializeField]
        public GameEventChannelSO EventChannel { get; private set; }

        [Header("Random Links")]
        [field: SerializeField]
        public bool UseRandomAdjacentLinks { get; private set; } = true;

        [field: SerializeField, Range(0f, 1f)]
        public float AdjacentLinkChance { get; private set; } = 0.35f;

        [field: SerializeField]
        public int MaxRandomAdjacentLinks { get; private set; } = 1;
        
        [Header("Input")]
        [field: SerializeField]
        public InputDataSO InputDataSO { get; private set; }

        [field: SerializeField]
        public Camera InputCamera { get; private set; }

        [field: SerializeField]
        public LayerMask NodeClickMask { get; private set; } = Physics2D.DefaultRaycastLayers;

        [Header("UI Blocking")]
        [field: SerializeField]
        public RectTransform NodePanelBlockRect { get; private set; }

        private DungeonGraph graph;
        private DungeonGraphView view;
        private readonly Dictionary<Collider2D, Node> lockedNodeByCollider = new();
        private readonly Dictionary<Collider2D, Node> unlockedNodeByCollider = new();
        private readonly List<DungeonNode> buildParentCandidates = new();
        private readonly List<DungeonNode> randomAdjacentCandidates = new();
        private Node lastBuiltNodeView;
        private int lastBuiltFrame = -1;

        public bool HasLockedNodesVisible { get; private set; }

        private void OnEnable()
        {
            EventChannel.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            EventChannel.AddListener<UnlockedNodeClickedEvent>(HandleUnlockedNodeClicked);
            InputDataSO.OnMouseInputEvent += HandleMouseInput;
        }

        

        private void OnDisable()
        {
            EventChannel.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            EventChannel.RemoveListener<UnlockedNodeClickedEvent>(HandleUnlockedNodeClicked);
            InputDataSO.OnMouseInputEvent -= HandleMouseInput;
        }

        private void Awake()
        {
            RebuildInitialGraph();

            if (Application.isPlaying)
                ShowLockedNodes();
        }

        [ContextMenu("Rebuild Initial Graph")]
        public void RebuildInitialGraph()
        {
            graph = new DungeonGraph();
            view = new DungeonGraphView(transform, NodePrefab, EdgeLinePrefab, GridSpacing, NodeSize);
            view.ClearAll();
            lockedNodeByCollider.Clear();
            unlockedNodeByCollider.Clear();

            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            RegisterUnlockedNode(view.CreateNode(entrance));
            HasLockedNodesVisible = false;
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

            EventChannel.RaiseEvent(new BuildCostRequestedEvent(lockedNode, BuildGoldCost));
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
                SelectedType = type;
        }

        private DungeonNodeType ResolveBuildType(Node lockedNode)
        {
            if (lockedNode.FromNode.Type == DungeonNodeType.Entrance && graph.Nodes.Count == 1)
                return DungeonNodeType.Corridor;

            return SelectedType;
        }

        private void ConnectAdjacentNodes(DungeonNode node, DungeonNode preferredFirstNode)
        {
            TryConnectNodes(preferredFirstNode, node);

            if (!UseRandomAdjacentLinks || MaxRandomAdjacentLinks <= 0)
                return;

            var randomLinkCount = 0;
            randomAdjacentCandidates.Clear();
            foreach (var direction in directions)
            {
                var adjacentPosition = node.GridPosition + direction;
                if (!graph.TryGetNodeAt(adjacentPosition, out var adjacentNode))
                    continue;

                if (adjacentNode == preferredFirstNode)
                    continue;

                if (randomLinkCount >= MaxRandomAdjacentLinks)
                    return;

                if (!CanRandomConnect(node, adjacentNode))
                    continue;

                randomAdjacentCandidates.Add(adjacentNode);
            }

            while (randomAdjacentCandidates.Count > 0 && randomLinkCount < MaxRandomAdjacentLinks)
            {
                var index = Random.Range(0, randomAdjacentCandidates.Count);
                var adjacentNode = randomAdjacentCandidates[index];
                randomAdjacentCandidates.RemoveAt(index);

                if (Random.value > AdjacentLinkChance)
                    continue;

                TryConnectNodes(adjacentNode, node);
                randomLinkCount++;
            }
        }

        private bool CanRandomConnect(DungeonNode node, DungeonNode adjacentNode)
        {
            if (node.FreePorts <= 0 || adjacentNode.FreePorts <= 0)
                return false;

            if (node.Type == DungeonNodeType.Boss || node.Type == DungeonNodeType.Treasury)
                return false;

            if (adjacentNode.Type == DungeonNodeType.Boss || adjacentNode.Type == DungeonNodeType.Treasury)
                return false;

            return true;
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
            if (IsPointerOverNodePanel())
                return;

            Vector2 worldPosition = InputDataSO.SceneToWorldPoint(InputCamera);
            var clickedCollider = Physics2D.OverlapPoint(worldPosition, NodeClickMask);

            if (clickedCollider == null)
                return;

            if (!lockedNodeByCollider.TryGetValue(clickedCollider, out var lockedNode))
            {
                if (!unlockedNodeByCollider.TryGetValue(clickedCollider, out var unlockedNode))
                    return;

                EventChannel.RaiseEvent(new UnlockedNodeClickedEvent(unlockedNode));
                return;
            }

            TryBuildAt(lockedNode);
        }

        private bool IsPointerOverNodePanel()
        {
            if (!NodePanelBlockRect.gameObject.activeInHierarchy)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(
                NodePanelBlockRect,
                InputDataSO.ReadScreenMousePosition(),
                null);
        }

        private void HandleUnlockedNodeClicked(UnlockedNodeClickedEvent evt)
        {
            if (evt.Node == lastBuiltNodeView && Time.frameCount <= lastBuiltFrame + 1)
                return;

            if (evt.Node.HasAssignedUnit)
                return;

            EventChannel.RaiseEvent(new ShowNodePanelEvent(evt.Node));
        }
    }
}
