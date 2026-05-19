using System.Collections.Generic;
using _01.Code.Artifacts;
using _01.Code.Core;
using _01.Code.Events;
using _01.Code.UI;
using _01.Code.Units;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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

        [Header("Scene Managers")]
        [SerializeField]
        private DungeonNodeManager nodeManager;

        [SerializeField]
        private DungeonEdgeManager edgeManager;

        [SerializeField]
        private Transform unitsRoot;

        
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

        [Header("Build Warning")]
        [SerializeField]
        private BuildConfirmPanelView buildConfirmPanelPrefab;

        [SerializeField]
        private Transform buildConfirmPanelParent;

        [Header("Input")]
        [SerializeField]
        private InputDataSO inputDataSO;

        [SerializeField]
        private LayerMask nodeClickMask = Physics2D.DefaultRaycastLayers;

        [Header("UI Blocking")]
        [SerializeField]
        private RectTransform nodePanelBlockRect;

        private DungeonGraph graph;
        private readonly Dictionary<Collider2D, Node> lockedNodeByCollider = new();
        private readonly Dictionary<Collider2D, Node> unlockedNodeByCollider = new();
        private readonly List<DungeonNode> buildParentCandidates = new();
        private Node lastBuiltNodeView;
        private BuildConfirmPanelView buildConfirmPanel;
        private int lastBuiltFrame = -1;
        private bool hasPendingMouseInput;
        private bool hasPendingRightMouseInput;

        public bool HasLockedNodesVisible { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            costEventChannel.AddListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel.AddListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            inputDataSO.OnMouseInputEvent += HandleMouseInput;
        }

        

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            costEventChannel.RemoveListener<BuildCostPaidEvent>(HandleBuildCostPaid);
            costEventChannel.RemoveListener<BuildCostRejectedEvent>(HandleBuildCostRejected);
            inputDataSO.OnMouseInputEvent -= HandleMouseInput;
            nodeManager?.ClearAll();
            edgeManager?.ClearAll();
            ClearUnitsRoot();
        }

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

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
            if (!Application.isPlaying)
                return;

            graph = new DungeonGraph();
            if (nodeManager == null || edgeManager == null)
            {
                Debug.LogError("DungeonGraphController requires preconfigured node and edge managers before play starts.", this);
                return;
            }

            nodeManager.ClearAll();
            edgeManager.ClearAll();
            lockedNodeByCollider.Clear();
            unlockedNodeByCollider.Clear();
            ClearUnitsRoot();

            var entrance = graph.AddNode(DungeonNodeType.Entrance, Vector2Int.zero);
            var entranceView = nodeManager.CreateNode(entrance);
            RegisterUnlockedNode(entranceView);
            CreateMainUnit(entranceView);

            HasLockedNodesVisible = false;
        }

        private void CreateMainUnit(Node entranceNode)
        {
            if (!Application.isPlaying)
                return;

            var spawnPosition = entranceNode.UnitPosition.position;
            if (unitsRoot == null)
            {
                Debug.LogError("DungeonGraphController requires a preconfigured Units root before play starts.", this);
                return;
            }

            MainUnit mainUnit = Instantiate(mainUnitPrefab, spawnPosition, Quaternion.identity);
            mainUnit.transform.SetParent(unitsRoot, true);
            mainUnit.transform.position = spawnPosition;
            mainUnit.transform.localScale = Vector3.one;
            mainUnit.name = "Player";
            mainUnit.Initialize(null);
            mainUnit.InitializeMainUnit(gameStateEventChannel);

            entranceNode.AssignUnit(null, mainUnit);
            artifactEventChannel.RaiseEvent(new UnitArtifactApplyRequestedEvent(mainUnit));
        }

        private void ClearUnitsRoot()
        {
            if (unitsRoot == null)
                return;

            if (Application.isPlaying)
            {
                ClearChildren(unitsRoot);
            }
            else
            {
                ClearChildrenImmediate(unitsRoot);
            }
        }

        private void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Destroy(root.GetChild(i).gameObject);
        }

        private void ClearChildrenImmediate(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                DestroyImmediate(root.GetChild(i).gameObject);
        }

        [ContextMenu("Show Locked Nodes")]
        public void ShowLockedNodes()
        {
            if (!Application.isPlaying || graph == null || nodeManager == null)
                return;

            HasLockedNodesVisible = true;
            RefreshLockedNodes();
        }
        
        [ContextMenu("Hide Locked Nodes")]
        public void HideLockedNodes()
        {
            HasLockedNodesVisible = false;
            lockedNodeByCollider.Clear();
            nodeManager?.ClearLockedNodes();
        }

        public void TryBuildAt(Node lockedNode)
        {
            if (lockedNode == null)
                return;

            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            ShowBuildConfirmPanel(lockedNode);
        }

        private void RequestBuildCost(Node lockedNode)
        {
            if (lockedNode == null)
                return;

            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            costEventChannel.RaiseEvent(new BuildCostRequestedEvent(lockedNode, buildGoldCost));
        }

        private void HandleBuildCostPaid(BuildCostPaidEvent evt)
        {
            var lockedNode = evt.Node;
            if (graph.IsOccupied(lockedNode.GridPosition) || lockedNode.FromNode.FreePorts <= 0)
                return;

            var buildPosition = lockedNode.GridPosition;
            var buildParent = lockedNode.FromNode;
            var type = ResolveBuildType(lockedNode);

            HideLockedNodeView(lockedNode);
            nodeManager.ClearLockedNodeAt(buildPosition);

            var node = graph.AddNode(type, buildPosition);
            var nodeView = nodeManager.CreateNode(node);

            lastBuiltNodeView = nodeView;
            lastBuiltFrame = Time.frameCount;
            RegisterUnlockedNode(nodeView);
            ConnectAdjacentNodes(node, buildParent);

            if (HasLockedNodesVisible)
                RefreshLockedNodes();
        }

        private void HandleBuildCostRejected(BuildCostRejectedEvent evt)
        {
            if (evt.Node == null || graph.IsOccupied(evt.Node.GridPosition))
                return;

            EnsureBuildConfirmPanel().ShowNotEnoughGold(evt.GoldAmount, evt.CurrentGold);
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

            edgeManager.CreateEdge(fromNode.GridPosition, toNode.GridPosition);
        }

        private void HideLockedNodeView(Node lockedNode)
        {
            if (lockedNode == null)
                return;

            lockedNodeByCollider.Remove(lockedNode.ClickCollider);
            lockedNode.gameObject.SetActive(false);
            Destroy(lockedNode.gameObject);
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
            nodeManager.ClearLockedNodes();

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

                    var lockedNode = nodeManager.CreateLockedNode(parentNode, position, position - parentNode.GridPosition);
                    lockedNode.SetBuildCost(buildGoldCost);
                    RegisterLockedNode(lockedNode);
                }
            }
        }

        private void ShowBuildConfirmPanel(Node lockedNode)
        {
            EnsureBuildConfirmPanel().Show(buildGoldCost, () => RequestBuildCost(lockedNode));
        }

        private BuildConfirmPanelView EnsureBuildConfirmPanel()
        {
            if (buildConfirmPanel != null)
                return buildConfirmPanel;

            var parent = ResolveBuildConfirmPanelParent();
            if (buildConfirmPanelPrefab != null)
            {
                buildConfirmPanel = Instantiate(buildConfirmPanelPrefab, parent);
                return buildConfirmPanel;
            }

            buildConfirmPanel = BuildConfirmPanelView.CreateRuntime(parent);
            return buildConfirmPanel;
        }

        private Transform ResolveBuildConfirmPanelParent()
        {
            if (buildConfirmPanelParent != null)
                return buildConfirmPanelParent;

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                return canvas.transform;

            var canvasObject = new GameObject("BuildConfirmCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var runtimeCanvas = canvasObject.GetComponent<Canvas>();
            runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            runtimeCanvas.overrideSorting = true;
            runtimeCanvas.sortingOrder = short.MaxValue;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject.transform;
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
